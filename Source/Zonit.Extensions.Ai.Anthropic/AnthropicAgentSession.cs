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
        // Streaming is mandatory for the agent loop. Non-streaming responses on
        // long agent turns (80+ tools, extended thinking, growing context) leave
        // the HTTP/2 connection idle for minutes, which routers and proxies
        // silently drop — the client then waits the full AttemptTimeout for a
        // response that will never arrive. Anthropic's own docs require
        // streaming for any request that may take longer than ~10 minutes:
        // periodic `ping` events keep TCP alive and content deltas arrive
        // incrementally, eliminating the idle-connection failure mode.
        request.Stream = true;

        var payload = JsonSerializer.Serialize(request, AnthropicJsonContext.Default.AnthropicMessagesRequest);
        _logger.LogDebug("Anthropic agent turn {Turn} payload: {Payload}", _turnIndex, payload);

        var sw = Stopwatch.StartNew();
        var aggregated = await SendStreamingAsync(payload, cancellationToken).ConfigureAwait(false);
        sw.Stop();

        return BuildTurn(aggregated, sw.Elapsed);
    }

    [RequiresUnreferencedCode("JsonSchemaGenerator.Generate uses reflection over the response type.")]
    [RequiresDynamicCode("JsonSchemaGenerator.Generate uses reflection over the response type.")]
    private async Task<AggregatedTurn> SendStreamingAsync(string payload, CancellationToken cancellationToken)
    {
        using var requestMessage = new HttpRequestMessage(HttpMethod.Post, "/v1/messages")
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json"),
        };

        using var response = await _httpClient.SendAsync(
            requestMessage,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            var errBody = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            _logger.LogError("Anthropic agent error: {Status} - {Body}", response.StatusCode, errBody);
            throw new HttpRequestException($"Anthropic API failed: {response.StatusCode}: {errBody}");
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var reader = new StreamReader(stream, Encoding.UTF8);

        var agg = new AggregatedTurn();
        var blocks = new SortedDictionary<int, BlockAccumulator>();

        while (await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false) is { } line)
        {
            // SSE frames are `event: <name>\ndata: <json>\n\n`. We rely on the
            // `type` field inside the JSON payload, so the `event:` lines and
            // blank separators are ignored.
            if (line.Length == 0 || !line.StartsWith("data: ", StringComparison.Ordinal)) continue;
            var data = line[6..];
            if (data == "[DONE]") break;

            using var doc = JsonDocument.Parse(data);
            var root = doc.RootElement;
            if (!root.TryGetProperty("type", out var typeEl)) continue;
            var eventType = typeEl.GetString();

            switch (eventType)
            {
                case "message_start":
                    HandleMessageStart(root, agg);
                    break;
                case "content_block_start":
                    HandleBlockStart(root, blocks);
                    break;
                case "content_block_delta":
                    HandleBlockDelta(root, blocks);
                    break;
                case "message_delta":
                    HandleMessageDelta(root, agg);
                    break;
                // Other event types (content_block_stop, message_stop, ping)
                // require no action — ping in particular is the keep-alive
                // signal that prevents the idle-drop scenario.
            }
        }

        FinalizeBlocks(blocks, agg);
        return agg;
    }

    private static void HandleMessageStart(JsonElement root, AggregatedTurn agg)
    {
        if (!root.TryGetProperty("message", out var msg)) return;
        if (msg.TryGetProperty("id", out var idEl)) agg.RequestId = idEl.GetString();
        if (!msg.TryGetProperty("usage", out var u)) return;
        if (u.TryGetProperty("input_tokens", out var it)) agg.InputTokens = it.GetInt32();
        if (u.TryGetProperty("output_tokens", out var ot)) agg.OutputTokens = ot.GetInt32();
        if (u.TryGetProperty("cache_read_input_tokens", out var cr)) agg.CacheReadTokens = cr.GetInt32();
        if (u.TryGetProperty("cache_creation_input_tokens", out var cw)) agg.CacheWriteTokens = cw.GetInt32();
    }

    private static void HandleBlockStart(JsonElement root, SortedDictionary<int, BlockAccumulator> blocks)
    {
        if (!root.TryGetProperty("index", out var idxEl)) return;
        if (!root.TryGetProperty("content_block", out var cbEl)) return;
        var idx = idxEl.GetInt32();
        var type = cbEl.GetProperty("type").GetString() ?? "";

        var acc = new BlockAccumulator { Type = type };
        switch (type)
        {
            case "text":
                if (cbEl.TryGetProperty("text", out var t)) acc.Text.Append(t.GetString());
                break;
            case "tool_use":
                acc.ToolId = cbEl.GetProperty("id").GetString();
                acc.ToolName = cbEl.GetProperty("name").GetString();
                break;
            case "thinking":
                if (cbEl.TryGetProperty("thinking", out var th)) acc.Text.Append(th.GetString());
                break;
        }
        blocks[idx] = acc;
    }

    private static void HandleBlockDelta(JsonElement root, SortedDictionary<int, BlockAccumulator> blocks)
    {
        if (!root.TryGetProperty("index", out var idxEl)) return;
        if (!root.TryGetProperty("delta", out var dEl)) return;
        if (!blocks.TryGetValue(idxEl.GetInt32(), out var acc)) return;

        var deltaType = dEl.GetProperty("type").GetString();
        switch (deltaType)
        {
            case "text_delta":
                if (dEl.TryGetProperty("text", out var dt)) acc.Text.Append(dt.GetString());
                break;
            case "input_json_delta":
                if (dEl.TryGetProperty("partial_json", out var pj)) acc.ToolInputJson.Append(pj.GetString());
                break;
            case "thinking_delta":
                if (dEl.TryGetProperty("thinking", out var dth)) acc.Text.Append(dth.GetString());
                break;
            case "signature_delta":
                if (dEl.TryGetProperty("signature", out var sig)) acc.Signature = sig.GetString();
                break;
        }
    }

    private static void HandleMessageDelta(JsonElement root, AggregatedTurn agg)
    {
        if (!root.TryGetProperty("usage", out var u)) return;
        // message_delta carries the running totals; output_tokens in particular
        // converges to its final value here. Cache fields are usually fixed at
        // message_start but we accept later updates defensively.
        if (u.TryGetProperty("output_tokens", out var ot)) agg.OutputTokens = ot.GetInt32();
        if (u.TryGetProperty("input_tokens", out var it)) agg.InputTokens = it.GetInt32();
        if (u.TryGetProperty("cache_read_input_tokens", out var cr)) agg.CacheReadTokens = cr.GetInt32();
        if (u.TryGetProperty("cache_creation_input_tokens", out var cw)) agg.CacheWriteTokens = cw.GetInt32();
    }

    private static void FinalizeBlocks(SortedDictionary<int, BlockAccumulator> blocks, AggregatedTurn agg)
    {
        foreach (var (_, acc) in blocks)
        {
            switch (acc.Type)
            {
                case "text":
                {
                    var text = acc.Text.ToString();
                    agg.FinalText.Append(text);
                    agg.AssistantContent.Add(new AnthropicContentBlock { Type = "text", Text = text });
                    break;
                }
                case "tool_use":
                {
                    // Anthropic streams the tool input as concatenated JSON
                    // fragments via input_json_delta. An empty tool call is
                    // represented by zero deltas — default to an empty object.
                    var json = acc.ToolInputJson.Length == 0 ? "{}" : acc.ToolInputJson.ToString();
                    using var inputDoc = JsonDocument.Parse(json);
                    var input = inputDoc.RootElement.Clone();
                    var id = acc.ToolId ?? string.Empty;
                    var name = acc.ToolName ?? string.Empty;
                    agg.ToolCalls.Add(new PendingToolCall { Id = id, Name = name, Arguments = input });
                    agg.AssistantContent.Add(new AnthropicContentBlock
                    {
                        Type = "tool_use",
                        Id = id,
                        Name = name,
                        Input = input,
                    });
                    break;
                }
                case "thinking":
                    agg.AssistantContent.Add(new AnthropicContentBlock
                    {
                        Type = "thinking",
                        Thinking = acc.Text.ToString(),
                        Signature = acc.Signature,
                    });
                    break;
            }
        }
    }

    private AgentTurn BuildTurn(AggregatedTurn agg, TimeSpan duration)
    {
        _messages.Add(new AnthropicMessageItem { Role = "assistant", Content = agg.AssistantContent });

        var (inputCost, outputCost) = AiCostCalculator.CalculateCosts(_context.Llm, new TokenUsage
        {
            InputTokens = agg.InputTokens,
            OutputTokens = agg.OutputTokens,
            CachedTokens = agg.CacheReadTokens,
            CacheWriteTokens = agg.CacheWriteTokens,
        });

        var usage = new TokenUsage
        {
            InputTokens = agg.InputTokens,
            OutputTokens = agg.OutputTokens,
            CachedTokens = agg.CacheReadTokens,
            CacheWriteTokens = agg.CacheWriteTokens,
            InputCost = inputCost,
            OutputCost = outputCost,
        };

        return new AgentTurn
        {
            ToolCalls = agg.ToolCalls,
            FinalText = agg.ToolCalls.Count == 0 ? agg.FinalText.ToString() : null,
            Usage = usage,
            Duration = duration,
            RequestId = agg.RequestId,
        };
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

        var request = new AnthropicMessagesRequest
        {
            Model = llm.Name,
            MaxTokens = llm.MaxTokens,
            Messages = _messages,
        };

        var requiredMinTokens = AnthropicProvider.ApplyThinking(llm, request);
        if (request.MaxTokens < requiredMinTokens)
            request.MaxTokens = requiredMinTokens;

        // When the agent run was seeded from chat[], prompt.Text becomes the system
        // instruction (per IAiProvider.ChatAsync semantics). For standalone agent runs
        // (no chat seed), AppendInitialUserMessage already added prompt.Text as the
        // initial user turn — don't duplicate it as system.
        if (_context.InitialChat is { Count: > 0 } && !string.IsNullOrEmpty(prompt.Text))
            request.System = new List<AnthropicContentBlock> { new() { Type = "text", Text = prompt.Text } };

        if (llm is AnthropicBase anth)
        {
            if (anth.TopP < 1.0) request.TopP = anth.TopP;
            else if (anth.Temperature < 1.0) request.Temperature = anth.Temperature;
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

        AnthropicProvider.ApplyCaching(llm, request);
        return request;
    }

    public ValueTask DisposeAsync()
    {
        _messages.Clear();
        return ValueTask.CompletedTask;
    }

    /// <summary>Per-block streaming accumulator (text / tool_use input JSON / thinking + signature).</summary>
    private sealed class BlockAccumulator
    {
        public string Type { get; set; } = "";
        public StringBuilder Text { get; } = new();
        public StringBuilder ToolInputJson { get; } = new();
        public string? ToolId { get; set; }
        public string? ToolName { get; set; }
        public string? Signature { get; set; }
    }

    /// <summary>Aggregated state assembled from one stream of SSE events.</summary>
    private sealed class AggregatedTurn
    {
        public string? RequestId { get; set; }
        public int InputTokens { get; set; }
        public int OutputTokens { get; set; }
        public int CacheReadTokens { get; set; }
        public int CacheWriteTokens { get; set; }
        public List<AnthropicContentBlock> AssistantContent { get; } = new();
        public List<PendingToolCall> ToolCalls { get; } = new();
        public StringBuilder FinalText { get; } = new();
    }
}
