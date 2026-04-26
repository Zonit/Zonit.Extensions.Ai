using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Text.Json;
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
[UnconditionalSuppressMessage("Trimming", "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code", Justification = "Internal pipeline routes user TResponse through source-generated JsonTypeInfo<T>; the [DAM(PublicProperties)] propagation on TResponse preserves required members. Reflection fallback only fires when the source generator is disabled.")]
[UnconditionalSuppressMessage("AOT", "IL3050:Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling.", Justification = "Internal pipeline routes user TResponse through source-generated JsonTypeInfo<T>; reflection paths only fire when the source generator is disabled.")]
internal sealed class GoogleAgentSession : IAgentSession
{
    private readonly HttpClient _httpClient;
    private readonly AgentSessionContext _context;
    private readonly ILogger _logger;
    private readonly GoogleOptions _options;

    private readonly List<GeminiRequestContent> _contents = new();
    // Cache of call_id → name; Gemini does not surface tool_call_id in functionCall —
    // the runner manufactures Ids per-call so we map them back to function names here.
    private readonly Dictionary<string, string> _pendingCalls = new(StringComparer.Ordinal);
    private int _turnIndex;

    public GoogleAgentSession(HttpClient httpClient, IOptions<GoogleOptions> options, AgentSessionContext context, ILogger logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
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
            var seeded = SeedInitialChat();
            if (!seeded) AppendInitialUserMessage();
        }
        else
        {
            AppendToolResultsAsFunctionResponses(toolResults ?? Array.Empty<ToolResult>());
        }

        var request = BuildRequest();
        var payload = JsonSerializer.Serialize(request, GoogleJsonContext.Default.GeminiRequest);
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
                    var parts = new List<GeminiPartItem>();
                    if (!sessionFilesAttached && promptFiles is not null)
                    {
                        AppendInlineFiles(parts, promptFiles);
                        sessionFilesAttached = true;
                    }
                    if (u.Files is not null) AppendInlineFiles(parts, u.Files);
                    parts.Add(new GeminiPartItem { Text = u.Text });
                    _contents.Add(new GeminiRequestContent { Role = "user", Parts = parts });
                    endsOnUserOrTool = true;
                    break;
                }
                case Assistant a:
                    _contents.Add(new GeminiRequestContent
                    {
                        Role = "model",
                        Parts = new List<GeminiPartItem> { new() { Text = a.Text } },
                    });
                    endsOnUserOrTool = false;
                    break;
                case Tool t:
                    _contents.Add(new GeminiRequestContent
                    {
                        Role = "user",
                        Parts = new List<GeminiPartItem>
                        {
                            new()
                            {
                                FunctionResponse = new GeminiFunctionResponse
                                {
                                    Name = t.Name,
                                    Response = JsonSerializer.Deserialize(t.ResultJson, GoogleJsonContext.Default.JsonElement),
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

    [RequiresUnreferencedCode("JsonSchemaGenerator.Generate uses reflection over the response type.")]
    [RequiresDynamicCode("JsonSchemaGenerator.Generate uses reflection over the response type.")]
    private void AppendInitialUserMessage()
    {
        var prompt = _context.Prompt;
        var parts = new List<GeminiPartItem>();
        if (prompt.Files is not null) AppendInlineFiles(parts, prompt.Files);

        var text = prompt.Text;
        if (_context.ResponseType is { } responseType)
        {
            var schema = JsonSchemaGenerator.Generate(responseType);
            text += "\n\nWhen finished, respond with a SINGLE JSON object (no markdown fences) matching:\n"
                 + schema.ToString();
        }
        parts.Add(new GeminiPartItem { Text = text });
        _contents.Add(new GeminiRequestContent { Role = "user", Parts = parts });
    }

    private void AppendToolResultsAsFunctionResponses(IReadOnlyList<ToolResult> toolResults)
    {
        var parts = new List<GeminiPartItem>(toolResults.Count);
        foreach (var r in toolResults)
        {
            var name = _pendingCalls.TryGetValue(r.CallId, out var n) ? n : r.CallId;
            parts.Add(new GeminiPartItem
            {
                FunctionResponse = new GeminiFunctionResponse
                {
                    Name = name,
                    Response = JsonSerializer.Deserialize(r.Output.GetRawText(), GoogleJsonContext.Default.JsonElement),
                },
            });
        }
        _contents.Add(new GeminiRequestContent { Role = "user", Parts = parts });
    }

    private static void AppendInlineFiles(List<GeminiPartItem> parts, IReadOnlyList<Asset> files)
    {
        foreach (var file in files.Where(f => f.IsImage))
            parts.Add(new GeminiPartItem { InlineData = new GeminiInlineData { MimeType = file.MediaType.Value, Data = file.Base64 } });
        foreach (var file in files.Where(f => f.IsDocument && f.MediaType == Asset.MimeType.ApplicationPdf))
            parts.Add(new GeminiPartItem { InlineData = new GeminiInlineData { MimeType = file.MediaType.Value, Data = file.Base64 } });
    }

    private GeminiRequest BuildRequest()
    {
        var llm = _context.Llm;
        var prompt = _context.Prompt;

        var config = new GeminiGenerationConfig { MaxOutputTokens = llm.MaxTokens };
        if (llm is GoogleBase g)
        {
            if (g.Temperature < 1.0) config.Temperature = g.Temperature;
            if (g.TopP < 1.0) config.TopP = g.TopP;
        }

        var request = new GeminiRequest
        {
            Contents = _contents,
            GenerationConfig = config,
        };

        if (!string.IsNullOrEmpty(prompt.System))
        {
            request.SystemInstruction = new GeminiSystemInstruction
            {
                Parts = new List<GeminiPartItem> { new() { Text = prompt.System! } },
            };
        }

        var declarations = new List<GeminiFunctionDeclaration>();
        if (llm.Tools is { Length: > 0 } native)
        {
            foreach (var t in native.OfType<FunctionTool>())
                declarations.Add(new GeminiFunctionDeclaration { Name = t.Name, Description = t.Description, Parameters = t.Parameters });
        }
        foreach (var custom in _context.Tools)
        {
            declarations.Add(new GeminiFunctionDeclaration
            {
                Name = custom.Name,
                Description = custom.Description,
                Parameters = custom.InputSchema,
            });
        }
        if (declarations.Count > 0)
            request.Tools = new List<GeminiToolGroup> { new() { FunctionDeclarations = declarations } };

        return request;
    }

    private AgentTurn ParseResponse(string body, TimeSpan duration)
    {
        var root = JsonDocument.Parse(body).RootElement;
        var requestId = root.TryGetProperty("responseId", out var idEl) ? idEl.GetString() : null;

        var toolCalls = new List<PendingToolCall>();
        var modelParts = new List<GeminiPartItem>();
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

                        var callId = $"call_{toolCalls.Count}_{name}";
                        _pendingCalls[callId] = name;

                        toolCalls.Add(new PendingToolCall
                        {
                            Id = callId,
                            Name = name,
                            Arguments = args,
                        });

                        modelParts.Add(new GeminiPartItem
                        {
                            FunctionCall = new GeminiFunctionCall { Name = name, Args = args },
                        });
                    }
                    else if (part.TryGetProperty("text", out var text))
                    {
                        var s = text.GetString() ?? string.Empty;
                        finalTextBuilder.Append(s);
                        modelParts.Add(new GeminiPartItem { Text = s });
                    }
                }
            }
        }

        if (modelParts.Count > 0)
            _contents.Add(new GeminiRequestContent { Role = "model", Parts = modelParts });

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
