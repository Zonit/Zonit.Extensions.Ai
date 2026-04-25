using System.Diagnostics.CodeAnalysis;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Unicode;
using Microsoft.Extensions.Logging;

namespace Zonit.Extensions.Ai;

/// <summary>
/// Minimal JSON-RPC 2.0 client for a single MCP server over HTTP / SSE
/// (Streamable HTTP transport — March 2025 spec).
/// </summary>
/// <remarks>
/// <para>
/// Handles the <c>initialize</c> handshake, <c>tools/list</c> discovery and
/// <c>tools/call</c> invocation. Both <c>application/json</c> and
/// <c>text/event-stream</c> responses are supported — when the server streams,
/// the client waits for the JSON-RPC response message whose <c>id</c> matches
/// the request and ignores intermediate progress notifications.
/// </para>
/// <para>
/// Scoped to the lifetime of one <see cref="Mcp"/> descriptor within a single
/// agent run — <see cref="McpToolFactory"/> creates and caches instances.
/// </para>
/// </remarks>
[RequiresUnreferencedCode("JSON serialization requires types that cannot be statically analyzed.")]
[RequiresDynamicCode("JSON serialization requires runtime code generation.")]
public sealed class McpClient : IAsyncDisposable
{
    private readonly HttpClient _httpClient;
    private readonly Mcp _server;
    private readonly ILogger _logger;

    private long _nextRequestId;
    private string? _sessionId;
    private bool _initialized;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
        Encoder = JavaScriptEncoder.Create(UnicodeRanges.All),
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public McpClient(HttpClient httpClient, Mcp server, ILogger logger)
    {
        _httpClient = httpClient;
        _server = server;
        _logger = logger;

        ConfigureHttpClient();
    }

    /// <summary>Descriptor this client was built for.</summary>
    public Mcp Server => _server;

    private void ConfigureHttpClient()
    {
        if (_httpClient.BaseAddress is null)
            _httpClient.BaseAddress = new Uri(_server.Url);

        // MCP Streamable HTTP transport: client should accept both JSON and SSE.
        if (!_httpClient.DefaultRequestHeaders.Accept.Any(h => h.MediaType == "application/json"))
            _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        if (!_httpClient.DefaultRequestHeaders.Accept.Any(h => h.MediaType == "text/event-stream"))
            _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));

        if (!string.IsNullOrEmpty(_server.Token)
            && _httpClient.DefaultRequestHeaders.Authorization is null)
        {
            _httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", _server.Token);
        }
    }

    /// <summary>
    /// Performs the MCP <c>initialize</c> handshake. Idempotent — safe to call
    /// multiple times; the real handshake happens exactly once per instance.
    /// </summary>
    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        if (_initialized) return;

        var result = await SendAsync("initialize", new Dictionary<string, object?>
        {
            ["protocolVersion"] = "2025-03-26",
            ["capabilities"] = new Dictionary<string, object?>
            {
                ["tools"] = new Dictionary<string, object?>(),
            },
            ["clientInfo"] = new Dictionary<string, object?>
            {
                ["name"] = "Zonit.Extensions.Ai",
                ["version"] = "1.0.0",
            },
        }, cancellationToken).ConfigureAwait(false);

        _initialized = true;

        // Per spec: send initialized notification (no id, no response expected).
        await SendNotificationAsync("notifications/initialized", null, cancellationToken).ConfigureAwait(false);

        _logger.LogDebug("MCP [{Server}] initialized: {Result}", _server.Name, result.GetRawText());
    }

    /// <summary>
    /// Enumerates the tools the server exposes.
    /// </summary>
    public async Task<IReadOnlyList<McpToolDescriptor>> ListToolsAsync(CancellationToken cancellationToken)
    {
        await InitializeAsync(cancellationToken).ConfigureAwait(false);

        var result = await SendAsync("tools/list", null, cancellationToken).ConfigureAwait(false);

        var tools = new List<McpToolDescriptor>();
        if (result.TryGetProperty("tools", out var toolsArr) && toolsArr.ValueKind == JsonValueKind.Array)
        {
            foreach (var t in toolsArr.EnumerateArray())
            {
                var name = t.TryGetProperty("name", out var n) ? n.GetString() : null;
                if (string.IsNullOrEmpty(name)) continue;

                var description = t.TryGetProperty("description", out var d) ? d.GetString() ?? string.Empty : string.Empty;
                var schema = t.TryGetProperty("inputSchema", out var s)
                    ? s.Clone()
                    : JsonDocument.Parse("{\"type\":\"object\",\"properties\":{}}").RootElement.Clone();

                tools.Add(new McpToolDescriptor(name!, description, schema));
            }
        }

        return tools;
    }

    /// <summary>
    /// Invokes a tool on the server.
    /// </summary>
    public async Task<JsonElement> CallToolAsync(string toolName, JsonElement arguments, CancellationToken cancellationToken)
    {
        await InitializeAsync(cancellationToken).ConfigureAwait(false);

        var parameters = new Dictionary<string, object?>
        {
            ["name"] = toolName,
            ["arguments"] = arguments.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null
                ? new Dictionary<string, object>()
                : (object)JsonDocument.Parse(arguments.GetRawText()).RootElement,
        };

        var result = await SendAsync("tools/call", parameters, cancellationToken).ConfigureAwait(false);

        // MCP tools/call returns { content: [...], isError?: bool }
        // Flatten content[] text parts into a single JSON payload so downstream
        // can feed it back to the model as a structured tool result.
        if (result.TryGetProperty("content", out var contentArr) && contentArr.ValueKind == JsonValueKind.Array)
        {
            var parts = new List<object>();
            foreach (var c in contentArr.EnumerateArray())
            {
                if (c.TryGetProperty("type", out var typeEl) && typeEl.GetString() == "text"
                    && c.TryGetProperty("text", out var textEl))
                {
                    parts.Add(new { type = "text", text = textEl.GetString() });
                }
                else
                {
                    parts.Add(JsonDocument.Parse(c.GetRawText()).RootElement);
                }
            }

            var isError = result.TryGetProperty("isError", out var errEl) && errEl.GetBoolean();

            var json = JsonSerializer.Serialize(new { content = parts, isError }, JsonOptions);
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.Clone();
        }

        return result;
    }

    private async Task<JsonElement> SendAsync(string method, object? parameters, CancellationToken cancellationToken)
    {
        var id = Interlocked.Increment(ref _nextRequestId);

        var request = new Dictionary<string, object?>
        {
            ["jsonrpc"] = "2.0",
            ["id"] = id,
            ["method"] = method,
        };
        if (parameters is not null) request["params"] = parameters;

        var body = JsonSerializer.Serialize(request, JsonOptions);
        using var content = new StringContent(body, Encoding.UTF8, "application/json");

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "")
        {
            Content = content,
        };
        if (!string.IsNullOrEmpty(_sessionId))
            httpRequest.Headers.Add("Mcp-Session-Id", _sessionId!);

        _logger.LogDebug("MCP [{Server}] → {Method} #{Id}: {Body}", _server.Name, method, id, body);

        using var response = await _httpClient.SendAsync(
                httpRequest, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);

        // Capture session id if the server assigned one (first handshake).
        if (response.Headers.TryGetValues("Mcp-Session-Id", out var sessionValues))
            _sessionId = sessionValues.FirstOrDefault() ?? _sessionId;

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            _logger.LogError("MCP [{Server}] error {Status}: {Body}", _server.Name, response.StatusCode, errorBody);
            throw new HttpRequestException($"MCP '{_server.Name}' failed: {response.StatusCode}: {errorBody}");
        }

        return await ReadJsonRpcResponseAsync(response, id, cancellationToken).ConfigureAwait(false);
    }

    private async Task SendNotificationAsync(string method, object? parameters, CancellationToken cancellationToken)
    {
        var request = new Dictionary<string, object?>
        {
            ["jsonrpc"] = "2.0",
            ["method"] = method,
        };
        if (parameters is not null) request["params"] = parameters;

        var body = JsonSerializer.Serialize(request, JsonOptions);
        using var content = new StringContent(body, Encoding.UTF8, "application/json");

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "")
        {
            Content = content,
        };
        if (!string.IsNullOrEmpty(_sessionId))
            httpRequest.Headers.Add("Mcp-Session-Id", _sessionId!);

        using var response = await _httpClient.SendAsync(httpRequest, cancellationToken).ConfigureAwait(false);
        // Notifications: server returns 202 Accepted; no body to read.
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("MCP [{Server}] notification {Method} returned {Status}",
                _server.Name, method, response.StatusCode);
        }
    }

    /// <summary>
    /// Reads the JSON-RPC response from either a plain JSON body or an SSE stream,
    /// ignoring progress notifications until the one with matching <paramref name="expectedId"/> arrives.
    /// </summary>
    private async Task<JsonElement> ReadJsonRpcResponseAsync(
        HttpResponseMessage response, long expectedId, CancellationToken cancellationToken)
    {
        var contentType = response.Content.Headers.ContentType?.MediaType;

        if (string.Equals(contentType, "text/event-stream", StringComparison.OrdinalIgnoreCase))
        {
            return await ReadSseResponseAsync(response, expectedId, cancellationToken).ConfigureAwait(false);
        }

        var bodyText = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        return ParseJsonRpcPayload(bodyText, expectedId);
    }

    private async Task<JsonElement> ReadSseResponseAsync(
        HttpResponseMessage response, long expectedId, CancellationToken cancellationToken)
    {
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var reader = new StreamReader(stream, Encoding.UTF8);

        var dataBuilder = new StringBuilder();

        while (!cancellationToken.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
            if (line is null) break;

            if (line.Length == 0)
            {
                // End of an SSE event — flush the accumulated data.
                if (dataBuilder.Length == 0) continue;
                var payload = dataBuilder.ToString();
                dataBuilder.Clear();

                if (TryMatchResponse(payload, expectedId, out var matched))
                    return matched;

                continue;
            }

            if (line.StartsWith("data:", StringComparison.Ordinal))
            {
                var dataChunk = line.Length > 5 ? line[5..].TrimStart() : string.Empty;
                if (dataBuilder.Length > 0) dataBuilder.Append('\n');
                dataBuilder.Append(dataChunk);
            }
            // Other fields (event:, id:, retry:) are ignored — we only care about data.
        }

        // Flush if stream ended without a trailing blank line.
        if (dataBuilder.Length > 0 && TryMatchResponse(dataBuilder.ToString(), expectedId, out var last))
            return last;

        throw new InvalidOperationException(
            $"MCP SSE stream ended without a JSON-RPC response for request id {expectedId}.");
    }

    private static bool TryMatchResponse(string payload, long expectedId, out JsonElement matched)
    {
        matched = default;
        if (string.IsNullOrWhiteSpace(payload)) return false;

        try
        {
            var root = JsonDocument.Parse(payload).RootElement.Clone();

            // Batch response support: [{..},{..}]
            if (root.ValueKind == JsonValueKind.Array)
            {
                foreach (var el in root.EnumerateArray())
                {
                    if (TryExtractById(el, expectedId, out matched))
                        return true;
                }
                return false;
            }

            return TryExtractById(root, expectedId, out matched);
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static bool TryExtractById(JsonElement el, long expectedId, out JsonElement matched)
    {
        matched = default;
        if (el.ValueKind != JsonValueKind.Object) return false;
        if (!el.TryGetProperty("id", out var idEl)) return false;

        long? id = idEl.ValueKind switch
        {
            JsonValueKind.Number => idEl.TryGetInt64(out var n) ? n : null,
            JsonValueKind.String => long.TryParse(idEl.GetString(), out var n) ? n : null,
            _ => null,
        };
        if (id != expectedId) return false;

        if (el.TryGetProperty("error", out var errorEl))
        {
            var code = errorEl.TryGetProperty("code", out var cEl) ? cEl.GetInt32() : -32603;
            var message = errorEl.TryGetProperty("message", out var mEl) ? mEl.GetString() : "MCP error";
            throw new McpProtocolException(code, message ?? "MCP error");
        }

        if (el.TryGetProperty("result", out var result))
        {
            matched = result.Clone();
            return true;
        }

        return false;
    }

    private static JsonElement ParseJsonRpcPayload(string body, long expectedId)
    {
        if (TryMatchResponse(body, expectedId, out var matched))
            return matched;

        throw new InvalidOperationException(
            $"MCP response did not contain a JSON-RPC message with id {expectedId}. Body: {body}");
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        // Optional: notify server we are done. Silently swallow errors.
        if (_initialized && !string.IsNullOrEmpty(_sessionId))
        {
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Delete, "");
                request.Headers.Add("Mcp-Session-Id", _sessionId);
                using var _ = await _httpClient.SendAsync(request, CancellationToken.None).ConfigureAwait(false);
            }
            catch
            {
                // ignored — best effort
            }
        }
    }
}

/// <summary>
/// Result of <see cref="McpClient.ListToolsAsync"/> for a single tool.
/// </summary>
/// <param name="Name">Tool name as exposed by the MCP server.</param>
/// <param name="Description">Human-readable description.</param>
/// <param name="InputSchema">JSON Schema describing the tool's parameters.</param>
public sealed record McpToolDescriptor(string Name, string Description, JsonElement InputSchema);

/// <summary>
/// Thrown when an MCP server returns a JSON-RPC error envelope.
/// </summary>
public sealed class McpProtocolException : Exception
{
    /// <summary>JSON-RPC error code.</summary>
    public int Code { get; }

    /// <inheritdoc/>
    public McpProtocolException(int code, string message) : base(message)
    {
        Code = code;
    }
}
