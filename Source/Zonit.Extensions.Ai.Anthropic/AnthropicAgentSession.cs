using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Unicode;
using Microsoft.Extensions.Logging;
using Zonit.Extensions;

namespace Zonit.Extensions.Ai.Anthropic;

/// <summary>
/// Stateful agent session for Anthropic Messages API. Client-side message
/// history is maintained across turns.
/// </summary>
[RequiresUnreferencedCode("JSON serialization requires types that cannot be statically analyzed.")]
[RequiresDynamicCode("JSON serialization requires runtime code generation.")]
internal sealed class AnthropicAgentSession : IAgentSession
{
    private readonly HttpClient _httpClient;
    private readonly AgentSessionContext _context;
    private readonly ILogger _logger;

    // Accumulating conversation history.
    private readonly List<object> _messages = new();
    private int _turnIndex;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        WriteIndented = false,
        Encoder = JavaScriptEncoder.Create(UnicodeRanges.All),
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public AnthropicAgentSession(HttpClient httpClient, AgentSessionContext context, ILogger logger)
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

        if (_turnIndex == 1)
        {
            // Seed any pre-existing chat[] history first (from IAiProvider.ChatAsync),
            // then either continue with the prompt's user message OR — if the chat
            // already ended on a User turn — let it be the initial user message.
            var seeded = AppendInitialChatHistory();
            if (!seeded)
                AppendInitialUserMessage();
        }
        else
        {
            AppendToolResultsMessage(toolResults ?? Array.Empty<ToolResult>());
        }

        var request = BuildRequest();
        var payload = JsonSerializer.Serialize(request, JsonOptions);
        _logger.LogDebug("Anthropic agent turn {Turn} payload: {Payload}", _turnIndex, payload);

        var sw = Stopwatch.StartNew();
        using var content = new StringContent(payload, Encoding.UTF8, "application/json");
        using var response = await _httpClient.PostAsync("/v1/messages", content, cancellationToken).ConfigureAwait(false);
        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        sw.Stop();

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("Anthropic agent error: {Status} - {Body}", response.StatusCode, body);
            throw new HttpRequestException($"Anthropic API failed: {response.StatusCode}: {body}");
        }

        return ParseResponse(body, sw.Elapsed);
    }

    private void AppendInitialUserMessage()
    {
        var prompt = _context.Prompt;
        var userContent = new List<object>();

        // Images / docs (if any) come first so Claude can reference them in the text.
        if (prompt.Files is not null)
        {
            foreach (var file in prompt.Files.Where(f => f.IsImage))
            {
                userContent.Add(new
                {
                    type = "image",
                    source = new
                    {
                        type = "base64",
                        media_type = file.MediaType.Value,
                        data = file.Base64,
                    },
                });
            }

            foreach (var file in prompt.Files.Where(f => f.IsDocument && f.MediaType == Asset.MimeType.ApplicationPdf))
            {
                userContent.Add(new
                {
                    type = "document",
                    source = new
                    {
                        type = "base64",
                        media_type = file.MediaType.Value,
                        data = file.Base64,
                    },
                });
            }
        }

        var text = prompt.Text;
        if (_context.ResponseType is not null)
        {
            // Append JSON-schema guidance to the user message — Anthropic doesn't
            // have native structured outputs in agent mode, so we steer Claude
            // toward the expected shape.
            var schema = JsonSchemaGenerator.Generate(_context.ResponseType);
            text += "\n\nWhen you are ready to produce the final answer (after all tool calls), "
                 + "respond with a SINGLE JSON object (no markdown fences) matching this schema:\n"
                 + schema.ToString();
        }

        userContent.Add(new { type = "text", text });
        _messages.Add(new { role = "user", content = userContent });
    }

    /// <summary>
    /// Materializes <see cref="AgentSessionContext.InitialChat"/> into the Anthropic
    /// message history. Returns <c>true</c> when the seeded history already ends
    /// on a <see cref="User"/> turn — meaning the model can reply immediately
    /// without us appending the prompt's own initial user message.
    /// </summary>
    private bool AppendInitialChatHistory()
    {
        var chat = _context.InitialChat;
        if (chat is null || chat.Count == 0) return false;

        // Anthropic requires the conversation to start with user — drop any leading assistant turns.
        var i = 0;
        while (i < chat.Count && chat[i] is Assistant) i++;

        var sessionFilesAttached = false;
        var promptFiles = _context.Prompt.Files;

        for (; i < chat.Count; i++)
        {
            switch (chat[i])
            {
                case User u:
                {
                    var userContent = new List<object>();
                    if (!sessionFilesAttached && promptFiles is not null)
                    {
                        AppendFiles(userContent, promptFiles);
                        sessionFilesAttached = true;
                    }
                    if (u.Files is not null) AppendFiles(userContent, u.Files);
                    userContent.Add(new { type = "text", text = u.Text });
                    _messages.Add(new { role = "user", content = userContent });
                    break;
                }
                case Assistant a:
                    _messages.Add(new
                    {
                        role = "assistant",
                        content = new object[] { new { type = "text", text = a.Text } },
                    });
                    break;
                case Tool t:
                    _messages.Add(new
                    {
                        role = "user",
                        content = new object[]
                        {
                            new
                            {
                                type = "tool_result",
                                tool_use_id = t.ToolCallId,
                                content = t.ResultJson,
                                is_error = t.IsError ? true : (bool?)null,
                            },
                        },
                    });
                    break;
            }
        }

        // Did we end on a user turn (or tool_result, which is also user-role)?
        if (_messages.Count == 0) return false;
        return IsUserMessage(_messages[^1]);
    }

    private static void AppendFiles(List<object> content, IReadOnlyList<Asset> files)
    {
        foreach (var file in files.Where(f => f.IsImage))
        {
            content.Add(new
            {
                type = "image",
                source = new { type = "base64", media_type = file.MediaType.Value, data = file.Base64 },
            });
        }
        foreach (var file in files.Where(f => f.IsDocument && f.MediaType == Asset.MimeType.ApplicationPdf))
        {
            content.Add(new
            {
                type = "document",
                source = new { type = "base64", media_type = file.MediaType.Value, data = file.Base64 },
            });
        }
    }

    private static bool IsUserMessage(object message)
    {
        var roleProp = message.GetType().GetProperty("role");
        return roleProp?.GetValue(message) as string == "user";
    }

    private void AppendToolResultsMessage(IReadOnlyList<ToolResult> toolResults)
    {
        var content = new List<object>(toolResults.Count);
        foreach (var r in toolResults)
        {
            content.Add(new
            {
                type = "tool_result",
                tool_use_id = r.CallId,
                content = r.Output.GetRawText(),
                is_error = r.IsError ? true : (bool?)null,
            });
        }

        _messages.Add(new { role = "user", content });
    }

    private Dictionary<string, object> BuildRequest()
    {
        var llm = _context.Llm;
        var prompt = _context.Prompt;

        var maxTokens = llm.MaxTokens;
        if (llm is AnthropicBase a && a.ThinkingBudget.HasValue)
        {
            var required = a.ThinkingBudget.Value + 1024;
            if (maxTokens < required) maxTokens = required;
        }

        var request = new Dictionary<string, object>
        {
            ["model"] = llm.Name,
            ["max_tokens"] = maxTokens,
            ["messages"] = _messages,
        };

        if (!string.IsNullOrEmpty(prompt.System))
            request["system"] = prompt.System!;

        if (llm is AnthropicBase anth)
        {
            // Anthropic allows only one of temperature / top_p.
            if (anth.TopP < 1.0) request["top_p"] = anth.TopP;
            else if (anth.Temperature < 1.0) request["temperature"] = anth.Temperature;

            if (anth.ThinkingBudget.HasValue)
            {
                request["thinking"] = new
                {
                    type = "enabled",
                    budget_tokens = anth.ThinkingBudget.Value,
                };
            }
        }

        // Tools: native FunctionTool (from ILlm.Tools) + custom agent tools.
        var tools = new List<object>();
        if (llm.Tools is { Length: > 0 } native)
        {
            foreach (var ft in native.OfType<FunctionTool>())
            {
                tools.Add(new
                {
                    name = ft.Name,
                    description = ft.Description,
                    input_schema = ft.Parameters,
                });
            }
        }
        foreach (var custom in _context.Tools)
        {
            tools.Add(new
            {
                name = custom.Name,
                description = custom.Description,
                input_schema = custom.InputSchema,
            });
        }
        if (tools.Count > 0)
            request["tools"] = tools;

        return request;
    }

    private AgentTurn ParseResponse(string body, TimeSpan duration)
    {
        var root = JsonDocument.Parse(body).RootElement;
        var requestId = root.TryGetProperty("id", out var idEl) ? idEl.GetString() : null;

        var usage = ParseUsage(root);

        var toolCalls = new List<PendingToolCall>();
        var assistantContent = new List<object>();
        var finalTextBuilder = new StringBuilder();

        if (root.TryGetProperty("content", out var content) && content.ValueKind == JsonValueKind.Array)
        {
            foreach (var block in content.EnumerateArray())
            {
                if (!block.TryGetProperty("type", out var typeEl)) continue;
                var type = typeEl.GetString();

                if (type == "tool_use"
                    && block.TryGetProperty("id", out var useId)
                    && block.TryGetProperty("name", out var useName)
                    && block.TryGetProperty("input", out var useInput))
                {
                    var id = useId.GetString()!;
                    var name = useName.GetString()!;
                    var input = useInput.Clone();

                    toolCalls.Add(new PendingToolCall
                    {
                        Id = id,
                        Name = name,
                        Arguments = input,
                    });

                    assistantContent.Add(new
                    {
                        type = "tool_use",
                        id,
                        name,
                        input = JsonDocument.Parse(input.GetRawText()).RootElement,
                    });
                }
                else if (type == "text" && block.TryGetProperty("text", out var textEl))
                {
                    var text = textEl.GetString() ?? string.Empty;
                    finalTextBuilder.Append(text);

                    assistantContent.Add(new { type = "text", text });
                }
                else if (type == "thinking" && block.TryGetProperty("thinking", out var thinkEl))
                {
                    // Preserve thinking block to maintain message consistency for subsequent turns.
                    var signature = block.TryGetProperty("signature", out var sigEl) ? sigEl.GetString() : null;
                    assistantContent.Add(new { type = "thinking", thinking = thinkEl.GetString(), signature });
                }
            }
        }

        // Append assistant turn to history so the next user/tool_result message is correctly ordered.
        _messages.Add(new { role = "assistant", content = assistantContent });

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
        if (!root.TryGetProperty("usage", out var usage))
            return new TokenUsage();

        var inputTokens = usage.TryGetProperty("input_tokens", out var it) ? it.GetInt32() : 0;
        var outputTokens = usage.TryGetProperty("output_tokens", out var ot) ? ot.GetInt32() : 0;
        var cacheRead = usage.TryGetProperty("cache_read_input_tokens", out var cr) ? cr.GetInt32() : 0;
        var cacheWrite = usage.TryGetProperty("cache_creation_input_tokens", out var cw) ? cw.GetInt32() : 0;

        var (inputCost, outputCost) = AiCostCalculator.CalculateCosts(_context.Llm, new TokenUsage
        {
            InputTokens = inputTokens,
            OutputTokens = outputTokens,
            CachedTokens = cacheRead,
            CacheWriteTokens = cacheWrite,
        });

        return new TokenUsage
        {
            InputTokens = inputTokens,
            OutputTokens = outputTokens,
            CachedTokens = cacheRead,
            CacheWriteTokens = cacheWrite,
            InputCost = inputCost,
            OutputCost = outputCost,
        };
    }

    public ValueTask DisposeAsync()
    {
        _messages.Clear();
        return ValueTask.CompletedTask;
    }
}
