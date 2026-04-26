using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Zonit.Extensions;

namespace Zonit.Extensions.Ai.OpenAi;

/// <summary>
/// Stateful agent session for OpenAI Responses API.
/// </summary>
/// <remarks>
/// Uses <c>previous_response_id</c> to chain iterations — only the user prompt,
/// tool schemas and structured-output format are sent in the first call; every
/// subsequent call sends only the new tool results (function_call_output items).
/// This mirrors OpenAI's recommended pattern for tool-calling agents.
/// </remarks>
[UnconditionalSuppressMessage("Trimming", "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code", Justification = "Internal pipeline routes user TResponse through source-generated JsonTypeInfo<T>; the [DAM(PublicProperties)] propagation on TResponse preserves required members. Reflection fallback only fires when the source generator is disabled.")]
[UnconditionalSuppressMessage("AOT", "IL3050:Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling.", Justification = "Internal pipeline routes user TResponse through source-generated JsonTypeInfo<T>; reflection paths only fire when the source generator is disabled.")]
internal sealed class OpenAiAgentSession : IAgentSession
{
    private readonly HttpClient _httpClient;
    private readonly AgentSessionContext _context;
    private readonly ILogger _logger;

    private string? _previousResponseId;
    private int _turnIndex;

    // Cache of call_id → tool name; used to build function_call_output items.
    private readonly Dictionary<string, string> _pendingCalls = new(StringComparer.Ordinal);

    public OpenAiAgentSession(HttpClient httpClient, AgentSessionContext context, ILogger logger)
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

        var request = _turnIndex == 1
            ? BuildInitialRequest()
            : BuildContinuationRequest(toolResults ?? Array.Empty<ToolResult>());

        var payload = JsonSerializer.Serialize(request, OpenAiJsonContext.Default.OpenAiResponsesRequest);
        _logger.LogDebug("OpenAI agent turn {Turn} payload: {Payload}", _turnIndex, payload);

        using var content = new StringContent(payload, Encoding.UTF8, "application/json");
        using var response = await _httpClient.PostAsync("/v1/responses", content, cancellationToken).ConfigureAwait(false);
        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

        sw.Stop();

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("OpenAI agent error: {Status} - {Body}", response.StatusCode, body);
            throw new HttpRequestException($"OpenAI API failed: {response.StatusCode}: {body}");
        }

        return ParseResponse(body, sw.Elapsed);
    }

    [RequiresUnreferencedCode("JsonSchemaGenerator.Generate uses reflection over the response type.")]
    [RequiresDynamicCode("JsonSchemaGenerator.Generate uses reflection over the response type.")]
    private OpenAiResponsesRequest BuildInitialRequest()
    {
        var llm = _context.Llm;
        var request = new OpenAiResponsesRequest
        {
            Model = llm.Name,
            MaxOutputTokens = llm.MaxTokens,
        };

        var prompt = _context.Prompt;
        if (!string.IsNullOrEmpty(prompt.System))
            request.Instructions = prompt.System!;

        var input = new List<OpenAiInputItem>();
        var seededFromChat = SeedInitialChatInto(input, prompt.Files);

        if (!seededFromChat)
        {
            var userContent = new List<OpenAiContentPart>
            {
                new() { Type = "input_text", Text = prompt.Text }
            };

            if (prompt.Files is not null)
            {
                foreach (var file in prompt.Files)
                {
                    if (file.IsImage)
                        userContent.Add(new OpenAiContentPart { Type = "input_image", ImageUrl = file.DataUrl });
                    else if (file.IsDocument)
                        userContent.Add(new OpenAiContentPart { Type = "input_file", FileData = file.DataUrl, Filename = file.OriginalName.Value });
                }
            }

            input.Add(new OpenAiInputItem { Role = "user", Content = userContent });
        }

        request.Input = input;

        if (_context.ResponseType is { } responseType)
        {
            request.Text = new OpenAiTextConfig
            {
                Format = new OpenAiTextFormat
                {
                    Type = "json_schema",
                    Name = "response",
                    Description = JsonSchemaGenerator.GetDescription(responseType) ?? "Response",
                    Schema = JsonSchemaGenerator.Generate(responseType),
                    Strict = true,
                },
            };
        }

        switch (llm)
        {
            case OpenAiChatBase chat:
                if (chat.Temperature < 1.0) request.Temperature = chat.Temperature;
                if (chat.TopP < 1.0) request.TopP = chat.TopP;
                break;

            case OpenAiReasoningBase reasoning:
                var r = new OpenAiReasoningConfig();
                var hasReasoning = false;
                if (((IReasoningLlm)reasoning).Reason.HasValue)
                {
                    r.Effort = ((IReasoningLlm)reasoning).Reason!.Value.ToString().ToLowerInvariant();
                    hasReasoning = true;
                }
                if (((IReasoningLlm)reasoning).ReasonSummary.HasValue)
                {
                    r.Summary = ((IReasoningLlm)reasoning).ReasonSummary!.Value.ToString().ToLowerInvariant();
                    hasReasoning = true;
                }
                if (hasReasoning) request.Reasoning = r;

                if (((IReasoningLlm)reasoning).OutputVerbosity.HasValue)
                {
                    request.Text ??= new OpenAiTextConfig();
                    request.Text.Verbosity = ((IReasoningLlm)reasoning).OutputVerbosity!.Value.ToString().ToLowerInvariant();
                }
                break;
        }

        if (llm is OpenAiBase openAiBase && openAiBase.StoreLogs)
            request.Store = true;

        var tools = new List<OpenAiToolItem>();
        if (llm.Tools is { Length: > 0 } native)
        {
            foreach (var t in native)
                tools.Add(BuildNativeTool(t));
        }
        foreach (var custom in _context.Tools)
        {
            tools.Add(new OpenAiToolItem
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

    private OpenAiResponsesRequest BuildContinuationRequest(IReadOnlyList<ToolResult> toolResults)
    {
        if (_previousResponseId is null)
            throw new InvalidOperationException("Continuation requested before initial turn completed.");

        var input = new List<OpenAiInputItem>(toolResults.Count);
        foreach (var result in toolResults)
        {
            input.Add(new OpenAiInputItem
            {
                Type = "function_call_output",
                CallId = result.CallId,
                Output = result.Output.GetRawText(),
            });
        }

        return new OpenAiResponsesRequest
        {
            Model = _context.Llm.Name,
            PreviousResponseId = _previousResponseId,
            Input = input,
        };
    }

    /// <summary>
    /// Seeds <see cref="AgentSessionContext.InitialChat"/> into the Responses-API
    /// <c>input</c> array. Returns <c>true</c> if the seeded history ends with a
    /// user-role message (or a tool result) so the caller does not need to append
    /// the prompt's own initial user turn.
    /// </summary>
    private bool SeedInitialChatInto(List<OpenAiInputItem> input, IReadOnlyList<Asset>? promptFiles)
    {
        var chat = _context.InitialChat;
        if (chat is null || chat.Count == 0) return false;

        var sessionFilesAttached = false;
        var endsOnUserOrTool = false;

        foreach (var msg in chat)
        {
            switch (msg)
            {
                case User u:
                {
                    var userContent = new List<OpenAiContentPart>
                    {
                        new() { Type = "input_text", Text = u.Text }
                    };
                    if (!sessionFilesAttached && promptFiles is not null)
                    {
                        AppendFiles(userContent, promptFiles);
                        sessionFilesAttached = true;
                    }
                    if (u.Files is not null) AppendFiles(userContent, u.Files);
                    input.Add(new OpenAiInputItem { Role = "user", Content = userContent });
                    endsOnUserOrTool = true;
                    break;
                }
                case Assistant a:
                    input.Add(new OpenAiInputItem
                    {
                        Role = "assistant",
                        Content = new List<OpenAiContentPart> { new() { Type = "output_text", Text = a.Text } },
                    });
                    endsOnUserOrTool = false;
                    break;
                case Tool t:
                    input.Add(new OpenAiInputItem
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

    private static void AppendFiles(List<OpenAiContentPart> content, IReadOnlyList<Asset> files)
    {
        foreach (var file in files)
        {
            if (file.IsImage)
                content.Add(new OpenAiContentPart { Type = "input_image", ImageUrl = file.DataUrl });
            else if (file.IsDocument)
                content.Add(new OpenAiContentPart { Type = "input_file", FileData = file.DataUrl, Filename = file.OriginalName.Value });
        }
    }

    private static OpenAiToolItem BuildNativeTool(IToolBase tool) => tool switch
    {
        FunctionTool f => new OpenAiToolItem
        {
            Type = "function",
            Name = f.Name,
            Description = f.Description,
            Parameters = f.Parameters,
            Strict = f.Strict,
        },
        WebSearchTool w => new OpenAiToolItem
        {
            Type = "web_search",
            SearchContextSize = w.ContextSize.ToString().ToLowerInvariant(),
        },
        CodeInterpreterTool => new OpenAiToolItem { Type = "code_interpreter" },
        _ => new OpenAiToolItem { Type = "unknown" },
    };

    private AgentTurn ParseResponse(string body, TimeSpan duration)
    {
        var root = JsonDocument.Parse(body).RootElement;

        _previousResponseId = root.TryGetProperty("id", out var idEl) ? idEl.GetString() : null;

        var usage = ParseUsage(root);
        var requestId = _previousResponseId;

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

                    // arguments may be a JSON string containing JSON — parse defensively.
                    JsonElement argsElement = argsEl.ValueKind == JsonValueKind.String
                        ? JsonDocument.Parse(argsEl.GetString() ?? "{}").RootElement
                        : argsEl;

                    _pendingCalls[callId] = name;
                    toolCalls.Add(new PendingToolCall
                    {
                        Id = callId,
                        Name = name,
                        Arguments = argsElement.Clone(),
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

        var inputTokens = usage.TryGetProperty("input_tokens", out var it) ? it.GetInt32() : 0;
        var outputTokens = usage.TryGetProperty("output_tokens", out var ot) ? ot.GetInt32() : 0;

        var cached = 0;
        if (usage.TryGetProperty("input_tokens_details", out var inDetails)
            && inDetails.TryGetProperty("cached_tokens", out var ct))
            cached = ct.GetInt32();

        var reasoning = 0;
        if (usage.TryGetProperty("output_tokens_details", out var outDetails)
            && outDetails.TryGetProperty("reasoning_tokens", out var rt))
            reasoning = rt.GetInt32();

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

    public ValueTask DisposeAsync()
    {
        _pendingCalls.Clear();
        return ValueTask.CompletedTask;
    }
}
