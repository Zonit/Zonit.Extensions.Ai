using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Unicode;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Zonit.Extensions;

namespace Zonit.Extensions.Ai.Google;

/// <summary>
/// Stateful agent session for Google Gemini's <c>generateContent</c> API. The
/// conversation is replayed in full each turn (Gemini has no server-side
/// continuation token equivalent to OpenAI's <c>previous_response_id</c>).
/// </summary>
/// <remarks>
/// Tool calls are received as <c>functionCall</c> parts in the model output;
/// tool results are sent back as user-role <c>functionResponse</c> parts.
/// </remarks>
[RequiresUnreferencedCode("JSON serialization requires types that cannot be statically analyzed.")]
[RequiresDynamicCode("JSON serialization requires runtime code generation.")]
internal sealed class GoogleAgentSession : IAgentSession
{
    private readonly HttpClient _httpClient;
    private readonly AgentSessionContext _context;
    private readonly ILogger _logger;
    private readonly GoogleOptions _options;

    private readonly List<object> _contents = new();
    // Cache of call_id → name; Gemini does not surface tool_call_id in functionCall —
    // the runner manufactures Ids per-call so we map them back to function names here.
    private readonly Dictionary<string, string> _pendingCalls = new(StringComparer.Ordinal);
    private int _turnIndex;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
        Encoder = JavaScriptEncoder.Create(UnicodeRanges.All),
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public GoogleAgentSession(HttpClient httpClient, IOptions<GoogleOptions> options, AgentSessionContext context, ILogger logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _context = context;
        _logger = logger;
    }

    public async Task<AgentTurn> RunTurnAsync(
        IReadOnlyList<ToolResult>? toolResults,
        CancellationToken cancellationToken)
    {
        _turnIndex++;
        var sw = Stopwatch.StartNew();

        if (_turnIndex == 1)
        {
            var seeded = SeedInitialChat();
            if (!seeded) AppendInitialUserMessage();
        }
        else
        {
            AppendToolResultsAsFunctionResponses(toolResults ?? Array.Empty<ToolResult>());
        }

        var request = BuildRequest();
        var payload = JsonSerializer.Serialize(request, JsonOptions);
        _logger.LogDebug("Google agent turn {Turn} payload: {Payload}", _turnIndex, payload);

        var endpoint = $"/v1beta/models/{_context.Llm.Name}:generateContent?key={_options.ApiKey}";

        using var content = new StringContent(payload, Encoding.UTF8, "application/json");
        using var response = await _httpClient.PostAsync(endpoint, content, cancellationToken).ConfigureAwait(false);
        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

        sw.Stop();

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("Google agent error: {Status} - {Body}", response.StatusCode, body);
            throw new HttpRequestException($"Google API failed: {response.StatusCode}: {body}");
        }

        return ParseResponse(body, sw.Elapsed);
    }

    private bool SeedInitialChat()
    {
        var chat = _context.InitialChat;
        if (chat is null || chat.Count == 0) return false;

        var sessionFilesAttached = false;
        var promptFiles = _context.Prompt.Files;
        var endsOnUserOrTool = false;

        foreach (var msg in chat)
        {
            switch (msg)
            {
                case User u:
                {
                    var parts = new List<object>();
                    if (!sessionFilesAttached && promptFiles is not null)
                    {
                        AppendInlineFiles(parts, promptFiles);
                        sessionFilesAttached = true;
                    }
                    if (u.Files is not null) AppendInlineFiles(parts, u.Files);
                    parts.Add(new { text = u.Text });
                    _contents.Add(new { role = "user", parts });
                    endsOnUserOrTool = true;
                    break;
                }
                case Assistant a:
                    _contents.Add(new
                    {
                        role = "model",
                        parts = new object[] { new { text = a.Text } },
                    });
                    endsOnUserOrTool = false;
                    break;
                case Tool t:
                    _contents.Add(new
                    {
                        role = "user",
                        parts = new object[]
                        {
                            new
                            {
                                functionResponse = new
                                {
                                    name = t.Name,
                                    response = JsonSerializer.Deserialize<JsonElement>(t.ResultJson),
                                },
                            },
                        },
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
        var parts = new List<object>();
        if (prompt.Files is not null) AppendInlineFiles(parts, prompt.Files);

        var text = prompt.Text;
        if (_context.ResponseType is { } responseType)
        {
            // Gemini does NOT mix function-calling tools with native structured outputs.
            // Steer with a schema hint in the user text instead.
            var schema = JsonSchemaGenerator.Generate(responseType);
            text += "\n\nWhen finished, respond with a SINGLE JSON object (no markdown fences) matching:\n"
                 + schema.ToString();
        }
        parts.Add(new { text });
        _contents.Add(new { role = "user", parts });
    }

    private void AppendToolResultsAsFunctionResponses(IReadOnlyList<ToolResult> toolResults)
    {
        var parts = new List<object>(toolResults.Count);
        foreach (var r in toolResults)
        {
            var name = _pendingCalls.TryGetValue(r.CallId, out var n) ? n : r.CallId;
            parts.Add(new
            {
                functionResponse = new
                {
                    name,
                    response = JsonSerializer.Deserialize<JsonElement>(r.Output.GetRawText()),
                },
            });
        }
        _contents.Add(new { role = "user", parts });
    }

    private static void AppendInlineFiles(List<object> parts, IReadOnlyList<Asset> files)
    {
        foreach (var file in files.Where(f => f.IsImage))
            parts.Add(new { inlineData = new { mimeType = file.MediaType.Value, data = file.Base64 } });
        foreach (var file in files.Where(f => f.IsDocument && f.MediaType == Asset.MimeType.ApplicationPdf))
            parts.Add(new { inlineData = new { mimeType = file.MediaType.Value, data = file.Base64 } });
    }

    private Dictionary<string, object> BuildRequest()
    {
        var llm = _context.Llm;
        var prompt = _context.Prompt;

        var config = new Dictionary<string, object> { ["maxOutputTokens"] = llm.MaxTokens };
        if (llm is GoogleBase g)
        {
            if (g.Temperature < 1.0) config["temperature"] = g.Temperature;
            if (g.TopP < 1.0) config["topP"] = g.TopP;
        }

        var request = new Dictionary<string, object>
        {
            ["contents"] = _contents,
            ["generationConfig"] = config,
        };

        if (!string.IsNullOrEmpty(prompt.System))
            request["systemInstruction"] = new { parts = new[] { new { text = prompt.System! } } };

        // Tools: native FunctionTool from llm.Tools + custom agent tools.
        var declarations = new List<object>();
        if (llm.Tools is { Length: > 0 } native)
        {
            foreach (var t in native.OfType<FunctionTool>())
                declarations.Add(new { name = t.Name, description = t.Description, parameters = t.Parameters });
        }
        foreach (var custom in _context.Tools)
        {
            declarations.Add(new
            {
                name = custom.Name,
                description = custom.Description,
                parameters = custom.InputSchema,
            });
        }
        if (declarations.Count > 0)
            request["tools"] = new[] { new { functionDeclarations = declarations } };

        return request;
    }

    private AgentTurn ParseResponse(string body, TimeSpan duration)
    {
        var root = JsonDocument.Parse(body).RootElement;
        var requestId = root.TryGetProperty("responseId", out var idEl) ? idEl.GetString() : null;

        var toolCalls = new List<PendingToolCall>();
        var modelParts = new List<object>();
        var finalTextBuilder = new StringBuilder();

        if (root.TryGetProperty("candidates", out var cands) && cands.ValueKind == JsonValueKind.Array)
        {
            var first = cands.EnumerateArray().FirstOrDefault();
            if (first.ValueKind == JsonValueKind.Object
                && first.TryGetProperty("content", out var contentEl)
                && contentEl.TryGetProperty("parts", out var partsEl)
                && partsEl.ValueKind == JsonValueKind.Array)
            {
                foreach (var part in partsEl.EnumerateArray())
                {
                    if (part.TryGetProperty("functionCall", out var fc))
                    {
                        var name = fc.TryGetProperty("name", out var n) ? n.GetString()! : string.Empty;
                        var args = fc.TryGetProperty("args", out var a)
                            ? a.Clone()
                            : JsonDocument.Parse("{}").RootElement.Clone();

                        // Manufacture a stable call_id since Gemini doesn't supply one
                        // (tool-name + ordinal collision is acceptable: the runner pairs
                        // results with calls in-order).
                        var callId = $"call_{toolCalls.Count}_{name}";
                        _pendingCalls[callId] = name;

                        toolCalls.Add(new PendingToolCall
                        {
                            Id = callId,
                            Name = name,
                            Arguments = args,
                        });

                        modelParts.Add(new { functionCall = new { name, args = JsonSerializer.Deserialize<JsonElement>(args.GetRawText()) } });
                    }
                    else if (part.TryGetProperty("text", out var text))
                    {
                        var s = text.GetString() ?? string.Empty;
                        finalTextBuilder.Append(s);
                        modelParts.Add(new { text = s });
                    }
                }
            }
        }

        // Mirror the model turn into history so the next functionResponse alternates correctly.
        if (modelParts.Count > 0)
            _contents.Add(new { role = "model", parts = modelParts });

        var usage = ParseUsage(root);

        return new AgentTurn
        {
            ToolCalls = toolCalls,
            FinalText = toolCalls.Count == 0 ? finalTextBuilder.ToString() : null,
            Usage = usage,
            Duration = duration,
            RequestId = requestId,
        };
    }

    private TokenUsage ParseUsage(JsonElement root)
    {
        if (!root.TryGetProperty("usageMetadata", out var u))
            return new TokenUsage();

        var inputTokens = u.TryGetProperty("promptTokenCount", out var pt) ? pt.GetInt32() : 0;
        var outputTokens = u.TryGetProperty("candidatesTokenCount", out var ct) ? ct.GetInt32() : 0;
        var reasoningTokens = u.TryGetProperty("thoughtsTokenCount", out var tt) ? tt.GetInt32() : 0;

        var (inputCost, outputCost) = AiCostCalculator.CalculateCosts(_context.Llm, new TokenUsage
        {
            InputTokens = inputTokens,
            OutputTokens = outputTokens + reasoningTokens,
        });

        return new TokenUsage
        {
            InputTokens = inputTokens,
            OutputTokens = outputTokens,
            ReasoningTokens = reasoningTokens,
            InputCost = inputCost,
            OutputCost = outputCost,
        };
    }

    public ValueTask DisposeAsync()
    {
        _contents.Clear();
        _pendingCalls.Clear();
        return ValueTask.CompletedTask;
    }
}
