using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Zonit.Extensions;

namespace Zonit.Extensions.Ai.Anthropic;

/// <summary>
/// Stateful agent session for Anthropic Messages API. Client-side message
/// history is maintained across turns.
/// </summary>
[UnconditionalSuppressMessage("Trimming", "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code", Justification = "Internal pipeline routes user TResponse through source-generated JsonTypeInfo<T>; the [DAM(PublicProperties)] propagation on TResponse preserves required members. Reflection fallback only fires when the source generator is disabled.")]
[UnconditionalSuppressMessage("AOT", "IL3050:Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling.", Justification = "Internal pipeline routes user TResponse through source-generated JsonTypeInfo<T>; reflection paths only fire when the source generator is disabled.")]
internal sealed class AnthropicAgentSession : IAgentSession
{
    private readonly HttpClient _httpClient;
    private readonly AgentSessionContext _context;
    private readonly ILogger _logger;

    // Accumulating conversation history.
    private readonly List<AnthropicMessageItem> _messages = new();
    private int _turnIndex;

    public AnthropicAgentSession(HttpClient httpClient, AgentSessionContext context, ILogger logger)
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

        if (_turnIndex == 1)
        {
            var seeded = AppendInitialChatHistory();
            if (!seeded)
                AppendInitialUserMessage();
        }
        else
        {
            AppendToolResultsMessage(toolResults ?? Array.Empty<ToolResult>());
        }

        var request = BuildRequest();
        var payload = JsonSerializer.Serialize(request, AnthropicJsonContext.Default.AnthropicMessagesRequest);
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

    [RequiresUnreferencedCode("JsonSchemaGenerator.Generate uses reflection over the response type.")]
    [RequiresDynamicCode("JsonSchemaGenerator.Generate uses reflection over the response type.")]
    private void AppendInitialUserMessage()
    {
        var prompt = _context.Prompt;
        var userContent = new List<AnthropicContentBlock>();

        if (prompt.Files is not null)
            AppendFiles(userContent, prompt.Files);

        var text = prompt.Text;
        if (_context.ResponseType is not null)
        {
            var schema = JsonSchemaGenerator.Generate(_context.ResponseType);
            text += "\n\nWhen you are ready to produce the final answer (after all tool calls), "
                 + "respond with a SINGLE JSON object (no markdown fences) matching this schema:\n"
                 + schema.ToString();
        }

        userContent.Add(new AnthropicContentBlock { Type = "text", Text = text });
        _messages.Add(new AnthropicMessageItem { Role = "user", Content = userContent });
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
                    var userContent = new List<AnthropicContentBlock>();
                    if (!sessionFilesAttached && promptFiles is not null)
                    {
                        AppendFiles(userContent, promptFiles);
                        sessionFilesAttached = true;
                    }
                    if (u.Files is not null) AppendFiles(userContent, u.Files);
                    userContent.Add(new AnthropicContentBlock { Type = "text", Text = u.Text });
                    _messages.Add(new AnthropicMessageItem { Role = "user", Content = userContent });
                    break;
                }
                case Assistant a:
                    _messages.Add(new AnthropicMessageItem
                    {
                        Role = "assistant",
                        Content = new List<AnthropicContentBlock> { new() { Type = "text", Text = a.Text } },
                    });
                    break;
                case Tool t:
                    _messages.Add(new AnthropicMessageItem
                    {
                        Role = "user",
                        Content = new List<AnthropicContentBlock>
                        {
                            new()
                            {
                                Type = "tool_result",
                                ToolUseId = t.ToolCallId,
                                Content = t.ResultJson,
                                IsError = t.IsError ? true : null,
                            },
                        },
                    });
                    break;
            }
        }

        if (_messages.Count == 0) return false;
        return _messages[^1].Role == "user";
    }

    private static void AppendFiles(List<AnthropicContentBlock> content, IReadOnlyList<Asset> files)
    {
        foreach (var file in files.Where(f => f.IsImage))
        {
            content.Add(new AnthropicContentBlock
            {
                Type = "image",
                Source = new AnthropicSource { Type = "base64", MediaType = file.MediaType.Value, Data = file.Base64 },
            });
        }
        foreach (var file in files.Where(f => f.IsDocument && f.MediaType == Asset.MimeType.ApplicationPdf))
        {
            content.Add(new AnthropicContentBlock
            {
                Type = "document",
                Source = new AnthropicSource { Type = "base64", MediaType = file.MediaType.Value, Data = file.Base64 },
            });
        }
    }

    private void AppendToolResultsMessage(IReadOnlyList<ToolResult> toolResults)
    {
        var content = new List<AnthropicContentBlock>(toolResults.Count);
        foreach (var r in toolResults)
        {
            content.Add(new AnthropicContentBlock
            {
                Type = "tool_result",
                ToolUseId = r.CallId,
                Content = r.Output.GetRawText(),
                IsError = r.IsError ? true : null,
            });
        }

        _messages.Add(new AnthropicMessageItem { Role = "user", Content = content });
    }

    private AnthropicMessagesRequest BuildRequest()
    {
        var llm = _context.Llm;
        var prompt = _context.Prompt;

        var maxTokens = llm.MaxTokens;
        if (llm is AnthropicBase a && a.ThinkingBudget.HasValue)
        {
            var required = a.ThinkingBudget.Value + 1024;
            if (maxTokens < required) maxTokens = required;
        }

        var request = new AnthropicMessagesRequest
        {
            Model = llm.Name,
            MaxTokens = maxTokens,
            Messages = _messages,
        };

        if (!string.IsNullOrEmpty(prompt.System))
            request.System = prompt.System!;

        if (llm is AnthropicBase anth)
        {
            if (anth.TopP < 1.0) request.TopP = anth.TopP;
            else if (anth.Temperature < 1.0) request.Temperature = anth.Temperature;

            if (anth.ThinkingBudget.HasValue)
            {
                request.Thinking = new AnthropicThinking
                {
                    Type = "enabled",
                    BudgetTokens = anth.ThinkingBudget.Value,
                };
            }
        }

        var tools = new List<AnthropicTool>();
        if (llm.Tools is { Length: > 0 } native)
        {
            foreach (var ft in native.OfType<FunctionTool>())
            {
                tools.Add(new AnthropicTool
                {
                    Name = ft.Name,
                    Description = ft.Description,
                    InputSchema = ft.Parameters,
                });
            }
        }
        foreach (var custom in _context.Tools)
        {
            tools.Add(new AnthropicTool
            {
                Name = custom.Name,
                Description = custom.Description,
                InputSchema = custom.InputSchema,
            });
        }
        if (tools.Count > 0)
            request.Tools = tools;

        return request;
    }

    private AgentTurn ParseResponse(string body, TimeSpan duration)
    {
        var root = JsonDocument.Parse(body).RootElement;
        var requestId = root.TryGetProperty("id", out var idEl) ? idEl.GetString() : null;

        var usage = ParseUsage(root);

        var toolCalls = new List<PendingToolCall>();
        var assistantContent = new List<AnthropicContentBlock>();
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

                    assistantContent.Add(new AnthropicContentBlock
                    {
                        Type = "tool_use",
                        Id = id,
                        Name = name,
                        Input = input,
                    });
                }
                else if (type == "text" && block.TryGetProperty("text", out var textEl))
                {
                    var text = textEl.GetString() ?? string.Empty;
                    finalTextBuilder.Append(text);
                    assistantContent.Add(new AnthropicContentBlock { Type = "text", Text = text });
                }
                else if (type == "thinking" && block.TryGetProperty("thinking", out var thinkEl))
                {
                    var signature = block.TryGetProperty("signature", out var sigEl) ? sigEl.GetString() : null;
                    assistantContent.Add(new AnthropicContentBlock { Type = "thinking", Thinking = thinkEl.GetString(), Signature = signature });
                }
            }
        }

        _messages.Add(new AnthropicMessageItem { Role = "assistant", Content = assistantContent });

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
