using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Zonit.Extensions;

namespace Zonit.Extensions.Ai.X;

/// <summary>
/// Stateful agent session for xAI Grok models exposed via the OpenAI-compatible
/// Responses API at <c>https://api.x.ai/v1/responses</c>. Mirrors
/// <c>OpenAiAgentSession</c> with X-specific request shape (no
/// <c>previous_response_id</c> threading — X does not yet support server-side
/// state, so we send the full message history every turn).
/// </summary>
[UnconditionalSuppressMessage("Trimming", "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code", Justification = "Internal pipeline routes user TResponse through source-generated JsonTypeInfo<T>; the [DAM(PublicProperties)] propagation on TResponse preserves required members. Reflection fallback only fires when the source generator is disabled.")]
[UnconditionalSuppressMessage("AOT", "IL3050:Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling.", Justification = "Internal pipeline routes user TResponse through source-generated JsonTypeInfo<T>; reflection paths only fire when the source generator is disabled.")]
internal sealed class XAgentSession : IAgentSession
{
    private readonly HttpClient _httpClient;
    private readonly AgentSessionContext _context;
    private readonly ILogger _logger;

    // Full input array (X has no previous_response_id chaining).
    private readonly List<XInputItem> _input = new();
    private int _turnIndex;

    public XAgentSession(HttpClient httpClient, AgentSessionContext context, ILogger logger)
    {
        _httpClient = httpClient;
        _context = context;
        _logger = logger;
    }

    [RequiresUnreferencedCode("JsonSchemaGenerator.Generate uses reflection over the response type.")]
    [RequiresDynamicCode("JsonSchemaGenerator.Generate uses reflection over the response type.")]
    public async Task<AgentTurn> RunTurnAsync(
        IReadOnlyList<ToolResult>? toolResults,
        CancellationToken cancellationToken)
    {
        _turnIndex++;
        var sw = Stopwatch.StartNew();

        if (_turnIndex == 1)
        {
            // Seed any pre-existing chat[] history from IAiProvider.ChatAsync.
            var seeded = SeedInitialChat();
            if (!seeded)
                AppendInitialUserMessage();
        }
        else
        {
            AppendToolResults(toolResults ?? Array.Empty<ToolResult>());
        }

        var request = BuildRequest();
        var payload = JsonSerializer.Serialize(request, XJsonContext.Default.XResponsesRequest);
        _logger.LogDebug("X agent turn {Turn} payload: {Payload}", _turnIndex, payload);

        using var content = new StringContent(payload, Encoding.UTF8, "application/json");
        using var response = await _httpClient.PostAsync("/v1/responses", content, cancellationToken).ConfigureAwait(false);
        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

        sw.Stop();

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("X agent error: {Status} - {Body}", response.StatusCode, body);
            throw new HttpRequestException($"X API failed: {response.StatusCode}: {body}");
        }

        return ParseResponse(body, sw.Elapsed);
    }

    private bool SeedInitialChat()
    {
        var chat = _context.InitialChat;
        if (chat is null || chat.Count == 0) return false;

        var sessionFilesAttached = false;
        var endsOnUserOrTool = false;
        var promptFiles = _context.Prompt.Files;

        foreach (var msg in chat)
        {
            switch (msg)
            {
                case User u:
                {
                    var userContent = new List<XContentPart>
                    {
                        new() { Type = "input_text", Text = u.Text }
                    };
                    if (!sessionFilesAttached && promptFiles is not null)
                    {
                        AppendFiles(userContent, promptFiles);
                        sessionFilesAttached = true;
                    }
                    if (u.Files is not null) AppendFiles(userContent, u.Files);
                    _input.Add(new XInputItem { Role = "user", Content = userContent });
                    endsOnUserOrTool = true;
                    break;
                }
                case Assistant a:
                    _input.Add(new XInputItem
                    {
                        Role = "assistant",
                        Content = new List<XContentPart> { new() { Type = "output_text", Text = a.Text } },
                    });
                    endsOnUserOrTool = false;
                    break;
                case Tool t:
                    _input.Add(new XInputItem
                    {
                        Type = "function_call_output",
                        CallId = t.ToolCallId,
                        Output = t.ResultJson,
                    });
                    endsOnUserOrTool = true;
                    break;
            }
        }

        return endsOnUserOrTool;
    }

    private void AppendInitialUserMessage()
    {
        var prompt = _context.Prompt;
        var userContent = new List<XContentPart>
        {
            new() { Type = "input_text", Text = prompt.Text }
        };

        if (prompt.Files is not null) AppendFiles(userContent, prompt.Files);

        _input.Add(new XInputItem { Role = "user", Content = userContent });
    }

    private void AppendToolResults(IReadOnlyList<ToolResult> toolResults)
    {
        foreach (var r in toolResults)
        {
            _input.Add(new XInputItem
            {
                Type = "function_call_output",
                CallId = r.CallId,
                Output = r.Output.GetRawText(),
            });
        }
    }

    private static void AppendFiles(List<XContentPart> content, IReadOnlyList<Asset> files)
    {
        // X currently supports image inputs only via Responses API.
        foreach (var file in files.Where(f => f.IsImage))
            content.Add(new XContentPart { Type = "input_image", ImageUrl = file.DataUrl });
    }

    [RequiresUnreferencedCode("JsonSchemaGenerator.Generate uses reflection over the response type.")]
    [RequiresDynamicCode("JsonSchemaGenerator.Generate uses reflection over the response type.")]
    private XResponsesRequest BuildRequest()
    {
        var llm = _context.Llm;
        var prompt = _context.Prompt;

        var request = new XResponsesRequest
        {
            Model = llm.Name,
            MaxOutputTokens = llm.MaxTokens,
            Input = _input,
        };

        if (!string.IsNullOrEmpty(prompt.System))
            request.Instructions = prompt.System!;

        if (llm is XChatBase chatLlm)
        {
            if (chatLlm.Temperature < 1.0) request.Temperature = chatLlm.Temperature;
            if (chatLlm.TopP < 1.0) request.TopP = chatLlm.TopP;
        }

        if (llm is XReasoningBase { Reason: not null } reasoning)
            request.ReasoningEffort = reasoning.Reason.Value.ToString().ToLowerInvariant();

        if (_context.ResponseType is { } responseType)
        {
            request.ResponseFormat = new XResponseFormat
            {
                Type = "json_schema",
                JsonSchema = new XJsonSchemaSpec
                {
                    Name = "response",
                    Schema = JsonSchemaGenerator.Generate(responseType),
                    Strict = true,
                },
            };
        }

        var tools = new List<XTool>();
        if (llm.Tools is { Length: > 0 } native)
        {
            foreach (var t in native)
                tools.Add(BuildNativeTool(t));
        }
        foreach (var custom in _context.Tools)
        {
            tools.Add(new XTool
            {
                Type = "function",
                Name = custom.Name,
                Description = custom.Description,
                Parameters = custom.InputSchema,
                Strict = true,
            });
        }
        if (tools.Count > 0)
            request.Tools = tools;

        return request;
    }

    private static XTool BuildNativeTool(IToolBase tool) => tool switch
    {
        FunctionTool f => new XTool
        {
            Type = "function",
            Name = f.Name,
            Description = f.Description,
            Parameters = f.Parameters,
            Strict = f.Strict,
        },
        WebSearchTool w => new XTool
        {
            Type = "web_search",
        },
        _ => new XTool { Type = "unknown" },
    };

    private AgentTurn ParseResponse(string body, TimeSpan duration)
    {
        var root = JsonDocument.Parse(body).RootElement;
        var requestId = root.TryGetProperty("id", out var idEl) ? idEl.GetString() : null;

        var usage = ParseUsage(root);
        var toolCalls = new List<PendingToolCall>();
        string? finalText = null;

        if (root.TryGetProperty("output", out var output) && output.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in output.EnumerateArray())
            {
                if (!item.TryGetProperty("type", out var typeEl)) continue;
                var type = typeEl.GetString();

                if (type == "function_call"
                    && item.TryGetProperty("call_id", out var callIdEl)
                    && item.TryGetProperty("name", out var nameEl)
                    && item.TryGetProperty("arguments", out var argsEl))
                {
                    var callId = callIdEl.GetString()!;
                    var name = nameEl.GetString()!;

                    JsonElement argsElement = argsEl.ValueKind == JsonValueKind.String
                        ? JsonDocument.Parse(argsEl.GetString() ?? "{}").RootElement
                        : argsEl;

                    toolCalls.Add(new PendingToolCall
                    {
                        Id = callId,
                        Name = name,
                        Arguments = argsElement.Clone(),
                    });

                    // Re-add the function_call to history so the next turn references it correctly.
                    _input.Add(new XInputItem
                    {
                        Type = "function_call",
                        CallId = callId,
                        Output = argsElement.GetRawText(),
                    });
                }
                else if (type == "message"
                    && item.TryGetProperty("content", out var contentArr)
                    && contentArr.ValueKind == JsonValueKind.Array)
                {
                    foreach (var part in contentArr.EnumerateArray())
                    {
                        if (part.TryGetProperty("type", out var partType)
                            && partType.GetString() == "output_text"
                            && part.TryGetProperty("text", out var textEl))
                        {
                            finalText = textEl.GetString();
                        }
                    }

                    // Append assistant message to history.
                    if (finalText != null)
                    {
                        _input.Add(new XInputItem
                        {
                            Role = "assistant",
                            Content = new List<XContentPart> { new() { Type = "output_text", Text = finalText } },
                        });
                    }
                }
            }
        }

        return new AgentTurn
        {
            ToolCalls = toolCalls,
            FinalText = toolCalls.Count == 0 ? finalText : null,
            Usage = usage,
            Duration = duration,
            RequestId = requestId,
        };
    }

    private TokenUsage ParseUsage(JsonElement root)
    {
        if (!root.TryGetProperty("usage", out var usage))
            return new TokenUsage();

        // X Responses API uses input_tokens/output_tokens; Chat Completions returns prompt_tokens/completion_tokens.
        var inputTokens = TryInt(usage, "input_tokens") ?? TryInt(usage, "prompt_tokens") ?? 0;
        var outputTokens = TryInt(usage, "output_tokens") ?? TryInt(usage, "completion_tokens") ?? 0;

        var cached = 0;
        if (usage.TryGetProperty("input_tokens_details", out var inDetails)
            && inDetails.TryGetProperty("cached_tokens", out var ct))
            cached = ct.GetInt32();
        else if (usage.TryGetProperty("prompt_tokens_details", out var pDetails)
            && pDetails.TryGetProperty("cached_tokens", out var pct))
            cached = pct.GetInt32();

        var reasoning = 0;
        if (usage.TryGetProperty("output_tokens_details", out var outDetails)
            && outDetails.TryGetProperty("reasoning_tokens", out var rt))
            reasoning = rt.GetInt32();
        else if (usage.TryGetProperty("completion_tokens_details", out var cDetails)
            && cDetails.TryGetProperty("reasoning_tokens", out var crt))
            reasoning = crt.GetInt32();

        var (inputCost, outputCost) = AiCostCalculator.CalculateCosts(_context.Llm, new TokenUsage
        {
            InputTokens = inputTokens,
            OutputTokens = outputTokens,
            CachedTokens = cached,
        });

        return new TokenUsage
        {
            InputTokens = inputTokens,
            OutputTokens = outputTokens,
            CachedTokens = cached,
            ReasoningTokens = reasoning,
            InputCost = inputCost,
            OutputCost = outputCost,
        };
    }

    private static int? TryInt(JsonElement obj, string property)
        => obj.TryGetProperty(property, out var el) && el.ValueKind == JsonValueKind.Number ? el.GetInt32() : null;

    public ValueTask DisposeAsync()
    {
        _input.Clear();
        return ValueTask.CompletedTask;
    }
}
