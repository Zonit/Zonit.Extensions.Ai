using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Unicode;
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
[RequiresUnreferencedCode("JSON serialization requires types that cannot be statically analyzed.")]
[RequiresDynamicCode("JSON serialization requires runtime code generation.")]
internal sealed class OpenAiAgentSession : IAgentSession
{
    private readonly HttpClient _httpClient;
    private readonly AgentSessionContext _context;
    private readonly ILogger _logger;

    private string? _previousResponseId;
    private int _turnIndex;

    // Cache of call_id → tool name; used to build function_call_output items.
    private readonly Dictionary<string, string> _pendingCalls = new(StringComparer.Ordinal);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        WriteIndented = false,
        Encoder = JavaScriptEncoder.Create(UnicodeRanges.All),
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public OpenAiAgentSession(HttpClient httpClient, AgentSessionContext context, ILogger logger)
    {
        _httpClient = httpClient;
        _context = context;
        _logger = logger;
    }

    public async Task<AgentTurn> RunTurnAsync(
        IReadOnlyList<ToolResult>? toolResults,
        CancellationToken cancellationToken)
    {
        _turnIndex++;
        var sw = Stopwatch.StartNew();

        var request = _turnIndex == 1
            ? BuildInitialRequest()
            : BuildContinuationRequest(toolResults ?? Array.Empty<ToolResult>());

        var payload = JsonSerializer.Serialize(request, JsonOptions);
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

    private Dictionary<string, object> BuildInitialRequest()
    {
        var llm = _context.Llm;
        var request = new Dictionary<string, object>
        {
            ["model"] = llm.Name,
            ["max_output_tokens"] = llm.MaxTokens,
        };

        var prompt = _context.Prompt;
        if (!string.IsNullOrEmpty(prompt.System))
            request["instructions"] = prompt.System!;

        var input = new List<object>();
        var seededFromChat = SeedInitialChatInto(input, prompt.Files);

        // If the chat history didn't already end on a user turn, append the prompt's
        // own initial user message (this is the classic GenerateAsync flow, plus a
        // fall-through for chat[] sequences that ended on Assistant/Tool messages
        // — though for OpenAI Responses API a Tool result *is* a valid last input).
        if (!seededFromChat)
        {
            var userContent = new List<object> { new { type = "input_text", text = prompt.Text } };

            if (prompt.Files is not null)
            {
                foreach (var file in prompt.Files)
                {
                    if (file.IsImage)
                        userContent.Add(new { type = "input_image", image_url = file.DataUrl });
                    else if (file.IsDocument)
                        userContent.Add(new { type = "input_file", file_data = file.DataUrl, filename = file.OriginalName.Value });
                }
            }

            input.Add(new { role = "user", content = userContent });
        }

        request["input"] = input;

        // Structured output schema.
        if (_context.ResponseType is { } responseType)
        {
            var schema = JsonSchemaGenerator.Generate(responseType);
            request["text"] = new Dictionary<string, object>
            {
                ["format"] = new Dictionary<string, object>
                {
                    ["type"] = "json_schema",
                    ["name"] = "response",
                    ["description"] = JsonSchemaGenerator.GetDescription(responseType) ?? "Response",
                    ["schema"] = schema,
                    ["strict"] = true,
                },
            };
        }

        // Model-specific settings.
        switch (llm)
        {
            case OpenAiChatBase chat:
                if (chat.Temperature < 1.0) request["temperature"] = chat.Temperature;
                if (chat.TopP < 1.0) request["top_p"] = chat.TopP;
                break;

            case OpenAiReasoningBase reasoning:
                var r = new Dictionary<string, object>();
                if (((IReasoningLlm)reasoning).Reason.HasValue)
                    r["effort"] = ((IReasoningLlm)reasoning).Reason!.Value.ToString().ToLowerInvariant();
                if (((IReasoningLlm)reasoning).ReasonSummary.HasValue)
                    r["summary"] = ((IReasoningLlm)reasoning).ReasonSummary!.Value.ToString().ToLowerInvariant();
                if (r.Count > 0) request["reasoning"] = r;

                if (((IReasoningLlm)reasoning).OutputVerbosity.HasValue)
                {
                    if (!request.ContainsKey("text"))
                        request["text"] = new Dictionary<string, object>();
                    if (request["text"] is Dictionary<string, object> textCfg)
                        textCfg["verbosity"] = ((IReasoningLlm)reasoning).OutputVerbosity!.Value.ToString().ToLowerInvariant();
                }
                break;
        }

        if (llm is OpenAiBase openAiBase && openAiBase.StoreLogs)
            request["store"] = true;

        // Tools: native (ILlm.Tools) + custom (context.Tools).
        var tools = new List<object>();
        if (llm.Tools is { Length: > 0 } native)
        {
            foreach (var t in native)
                tools.Add(BuildNativeTool(t));
        }
        foreach (var custom in _context.Tools)
        {
            tools.Add(new
            {
                type = "function",
                name = custom.Name,
                description = custom.Description,
                parameters = custom.InputSchema,
                strict = true,
            });
        }
        if (tools.Count > 0)
            request["tools"] = tools;

        return request;
    }

    private Dictionary<string, object> BuildContinuationRequest(IReadOnlyList<ToolResult> toolResults)
    {
        if (_previousResponseId is null)
            throw new InvalidOperationException("Continuation requested before initial turn completed.");

        var input = new List<object>(toolResults.Count);
        foreach (var result in toolResults)
        {
            input.Add(new
            {
                type = "function_call_output",
                call_id = result.CallId,
                output = result.Output.GetRawText(),
            });
        }

        return new Dictionary<string, object>
        {
            ["model"] = _context.Llm.Name,
            ["previous_response_id"] = _previousResponseId,
            ["input"] = input,
        };
    }

    /// <summary>
    /// Seeds <see cref="AgentSessionContext.InitialChat"/> into the Responses-API
    /// <c>input</c> array. Returns <c>true</c> if the seeded history ends with a
    /// user-role message (or a tool result) so the caller does not need to append
    /// the prompt's own initial user turn.
    /// </summary>
    private bool SeedInitialChatInto(List<object> input, IReadOnlyList<Asset>? promptFiles)
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
                    var userContent = new List<object> { new { type = "input_text", text = u.Text } };
                    if (!sessionFilesAttached && promptFiles is not null)
                    {
                        AppendFiles(userContent, promptFiles);
                        sessionFilesAttached = true;
                    }
                    if (u.Files is not null) AppendFiles(userContent, u.Files);
                    input.Add(new { role = "user", content = userContent });
                    endsOnUserOrTool = true;
                    break;
                }
                case Assistant a:
                    input.Add(new
                    {
                        role = "assistant",
                        content = new object[] { new { type = "output_text", text = a.Text } },
                    });
                    endsOnUserOrTool = false;
                    break;
                case Tool t:
                    input.Add(new
                    {
                        type = "function_call_output",
                        call_id = t.ToolCallId,
                        output = t.ResultJson,
                    });
                    endsOnUserOrTool = true;
                    break;
            }
        }

        return endsOnUserOrTool;
    }

    private static void AppendFiles(List<object> content, IReadOnlyList<Asset> files)
    {
        foreach (var file in files)
        {
            if (file.IsImage)
                content.Add(new { type = "input_image", image_url = file.DataUrl });
            else if (file.IsDocument)
                content.Add(new { type = "input_file", file_data = file.DataUrl, filename = file.OriginalName.Value });
        }
    }

    private static object BuildNativeTool(IToolBase tool) => tool switch
    {
        FunctionTool f => new
        {
            type = "function",
            name = f.Name,
            description = f.Description,
            parameters = f.Parameters,
            strict = f.Strict,
        },
        WebSearchTool w => new
        {
            type = "web_search",
            search_context_size = w.ContextSize.ToString().ToLowerInvariant(),
        },
        CodeInterpreterTool => new { type = "code_interpreter" },
        _ => new { type = "unknown" },
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
