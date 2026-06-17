using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace Zonit.Extensions.Ai.Sdk;

/// <summary>
/// A minimal MCP (Model Context Protocol) server over loopback HTTP — the mirror of the
/// framework's <c>McpClient</c>. Exposes per-session <see cref="ITool"/> sets to a local
/// MCP client (the Claude Code CLI). Handles <c>initialize</c>, <c>tools/list</c>,
/// <c>tools/call</c> and <c>ping</c>; replies <c>application/json</c> (no SSE needed —
/// there are no server-initiated messages). Hand-rolled on <see cref="HttpListener"/> +
/// <see cref="Utf8JsonWriter"/>/<see cref="JsonDocument"/>: no ASP.NET Core, no reflection.
/// </summary>
/// <remarks>
/// Security: binds only to <c>127.0.0.1</c> (never reachable off-machine) and requires a
/// per-session bearer token (<c>Authorization: Bearer &lt;token&gt;</c>) that selects which
/// tool set the request may reach. A single listener is shared across agent runs; the
/// token disambiguates sessions.
/// </remarks>
internal sealed class LoopbackMcpServer : IAsyncDisposable, IDisposable
{
    private readonly ILogger _logger;
    private readonly ConcurrentDictionary<string, IReadOnlyList<ITool>> _sessions = new(StringComparer.Ordinal);
    private readonly object _gate = new();

    private static readonly JsonElement EmptyObject = JsonDocument.Parse("{}").RootElement.Clone();

    private HttpListener? _listener;
    private Uri? _mcpUrl;
    private CancellationTokenSource? _cts;
    private Task? _acceptLoop;

    public LoopbackMcpServer(ILogger logger) => _logger = logger;

    /// <summary>Starts the listener on first use and returns the loopback MCP endpoint URL.</summary>
    public Uri EnsureStarted()
    {
        if (_mcpUrl is not null) return _mcpUrl;
        lock (_gate)
        {
            if (_mcpUrl is not null) return _mcpUrl;

            var port = GetFreeLoopbackPort();
            var listener = new HttpListener();
            // Literal loopback IP + explicit port → no admin / URL-ACL needed on Windows.
            listener.Prefixes.Add($"http://127.0.0.1:{port}/");
            listener.Start();

            _listener = listener;
            _cts = new CancellationTokenSource();
            _mcpUrl = new Uri($"http://127.0.0.1:{port}/mcp");
            _acceptLoop = Task.Run(() => AcceptLoopAsync(listener, _cts.Token));

            _logger.LogDebug("Loopback MCP bridge listening on {Url}", _mcpUrl);
            return _mcpUrl;
        }
    }

    /// <summary>Registers a tool set under <paramref name="token"/> (the session's bearer token).</summary>
    public void Register(string token, IReadOnlyList<ITool> tools) => _sessions[token] = tools;

    /// <summary>Revokes a session — its token can no longer reach any tools.</summary>
    public void Unregister(string token) => _sessions.TryRemove(token, out _);

    private static int GetFreeLoopbackPort()
    {
        var probe = new TcpListener(IPAddress.Loopback, 0);
        probe.Start();
        try { return ((IPEndPoint)probe.LocalEndpoint).Port; }
        finally { probe.Stop(); }
    }

    private async Task AcceptLoopAsync(HttpListener listener, CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            HttpListenerContext ctx;
            try
            {
                ctx = await listener.GetContextAsync().ConfigureAwait(false);
            }
            catch (Exception) when (ct.IsCancellationRequested) { break; }
            catch (HttpListenerException) { break; }
            catch (ObjectDisposedException) { break; }
            catch (InvalidOperationException) { break; }

            // Handle concurrently — a tool call may be slow (e.g. nested model calls).
            _ = Task.Run(() => HandleSafeAsync(ctx, ct), CancellationToken.None);
        }
    }

    private async Task HandleSafeAsync(HttpListenerContext ctx, CancellationToken ct)
    {
        try
        {
            await HandleAsync(ctx, ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Loopback MCP bridge request failed.");
            try { ctx.Response.Abort(); } catch { /* best effort */ }
        }
    }

    private async Task HandleAsync(HttpListenerContext ctx, CancellationToken ct)
    {
        var request = ctx.Request;

        // Authenticate: Bearer token selects the tool set.
        var tools = ResolveSession(request);
        if (tools is null)
        {
            await WriteAsync(ctx, 401, contentType: null, body: null, ct).ConfigureAwait(false);
            return;
        }

        // We don't offer a server→client SSE channel; only POST (RPC) and DELETE (end) matter.
        if (string.Equals(request.HttpMethod, "DELETE", StringComparison.OrdinalIgnoreCase))
        {
            await WriteAsync(ctx, 200, contentType: null, body: null, ct).ConfigureAwait(false);
            return;
        }
        if (!string.Equals(request.HttpMethod, "POST", StringComparison.OrdinalIgnoreCase))
        {
            await WriteAsync(ctx, 405, contentType: null, body: null, ct).ConfigureAwait(false);
            return;
        }

        string body;
        using (var reader = new StreamReader(request.InputStream, request.ContentEncoding ?? Encoding.UTF8))
            body = await reader.ReadToEndAsync(ct).ConfigureAwait(false);

        using var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;

        // Single request (the common case). Batch arrays are uncommon from the CLI;
        // handle the first element to stay simple and predictable.
        var requestEl = root.ValueKind == JsonValueKind.Array
            ? (root.GetArrayLength() > 0 ? root[0] : default)
            : root;

        var responseBytes = await HandleRpcAsync(requestEl, tools, ct).ConfigureAwait(false);

        if (responseBytes is null)
        {
            // Notification (no id) — acknowledge with 202, no body.
            await WriteAsync(ctx, 202, contentType: null, body: null, ct).ConfigureAwait(false);
            return;
        }

        await WriteAsync(ctx, 200, "application/json", responseBytes, ct).ConfigureAwait(false);
    }

    private IReadOnlyList<ITool>? ResolveSession(HttpListenerRequest request)
    {
        var auth = request.Headers["Authorization"];
        if (string.IsNullOrEmpty(auth) || !auth.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            return null;

        var token = auth["Bearer ".Length..].Trim();
        return _sessions.TryGetValue(token, out var tools) ? tools : null;
    }

    /// <summary>Dispatches one JSON-RPC request. Returns the response envelope bytes, or <c>null</c> for a notification.</summary>
    private async Task<byte[]?> HandleRpcAsync(JsonElement reqEl, IReadOnlyList<ITool> tools, CancellationToken ct)
    {
        if (reqEl.ValueKind != JsonValueKind.Object || !reqEl.TryGetProperty("method", out var methodEl))
            return null;

        var method = methodEl.GetString();
        var hasId = reqEl.TryGetProperty("id", out var id) && id.ValueKind is JsonValueKind.Number or JsonValueKind.String;
        reqEl.TryGetProperty("params", out var prms);

        // Notifications (no id) carry no response.
        if (!hasId)
            return null;

        switch (method)
        {
            case "initialize":
            {
                var protocolVersion = prms.ValueKind == JsonValueKind.Object
                    && prms.TryGetProperty("protocolVersion", out var pv) && pv.ValueKind == JsonValueKind.String
                        ? pv.GetString()!
                        : "2025-03-26";
                return BuildResponse(id, w => WriteInitializeResult(w, protocolVersion));
            }

            case "ping":
                return BuildResponse(id, static w => { w.WriteStartObject(); w.WriteEndObject(); });

            case "tools/list":
                return BuildResponse(id, w => WriteToolsList(w, tools));

            case "tools/call":
                return await BuildToolCallResponseAsync(id, prms, tools, ct).ConfigureAwait(false);

            default:
                return BuildError(id, -32601, $"Method not found: {method}");
        }
    }

    private async Task<byte[]> BuildToolCallResponseAsync(
        JsonElement id, JsonElement prms, IReadOnlyList<ITool> tools, CancellationToken ct)
    {
        var name = prms.ValueKind == JsonValueKind.Object && prms.TryGetProperty("name", out var n)
            ? n.GetString()
            : null;

        var tool = name is null ? null : tools.FirstOrDefault(t => t.Name == name);
        if (tool is null)
            return BuildError(id, -32602, $"Unknown tool: {name}");

        var args = prms.TryGetProperty("arguments", out var a) && a.ValueKind == JsonValueKind.Object
            ? a
            : EmptyObject;

        try
        {
            var output = await tool.InvokeAsync(args, ct).ConfigureAwait(false);
            var text = output.ValueKind == JsonValueKind.String ? output.GetString() ?? string.Empty : output.GetRawText();
            return BuildResponse(id, w => WriteToolResult(w, text, isError: false));
        }
        catch (Exception ex)
        {
            // MCP convention: tool failures are reported inside the result with isError=true,
            // not as a JSON-RPC protocol error — so the model can see and react to them.
            _logger.LogWarning(ex, "MCP bridge tool '{Tool}' threw.", name);
            return BuildResponse(id, w => WriteToolResult(w, ex.Message, isError: true));
        }
    }

    private static void WriteInitializeResult(Utf8JsonWriter w, string protocolVersion)
    {
        w.WriteStartObject();
        w.WriteString("protocolVersion", protocolVersion);
        w.WritePropertyName("capabilities");
        w.WriteStartObject();
        w.WritePropertyName("tools");
        w.WriteStartObject();
        w.WriteEndObject();
        w.WriteEndObject();
        w.WritePropertyName("serverInfo");
        w.WriteStartObject();
        w.WriteString("name", "zonit-tools");
        w.WriteString("version", "1.0.0");
        w.WriteEndObject();
        w.WriteEndObject();
    }

    private static void WriteToolsList(Utf8JsonWriter w, IReadOnlyList<ITool> tools)
    {
        w.WriteStartObject();
        w.WritePropertyName("tools");
        w.WriteStartArray();
        foreach (var tool in tools)
        {
            w.WriteStartObject();
            w.WriteString("name", tool.Name);
            w.WriteString("description", tool.Description);
            w.WritePropertyName("inputSchema");
            if (tool.InputSchema.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null)
            {
                w.WriteStartObject();
                w.WriteString("type", "object");
                w.WritePropertyName("properties");
                w.WriteStartObject();
                w.WriteEndObject();
                w.WriteEndObject();
            }
            else
            {
                tool.InputSchema.WriteTo(w);
            }
            w.WriteEndObject();
        }
        w.WriteEndArray();
        w.WriteEndObject();
    }

    private static void WriteToolResult(Utf8JsonWriter w, string text, bool isError)
    {
        w.WriteStartObject();
        w.WritePropertyName("content");
        w.WriteStartArray();
        w.WriteStartObject();
        w.WriteString("type", "text");
        w.WriteString("text", text);
        w.WriteEndObject();
        w.WriteEndArray();
        w.WriteBoolean("isError", isError);
        w.WriteEndObject();
    }

    private static byte[] BuildResponse(JsonElement id, Action<Utf8JsonWriter> writeResult)
    {
        using var ms = new MemoryStream();
        using (var w = new Utf8JsonWriter(ms))
        {
            w.WriteStartObject();
            w.WriteString("jsonrpc", "2.0");
            w.WritePropertyName("id");
            id.WriteTo(w);
            w.WritePropertyName("result");
            writeResult(w);
            w.WriteEndObject();
        }
        return ms.ToArray();
    }

    private static byte[] BuildError(JsonElement id, int code, string message)
    {
        using var ms = new MemoryStream();
        using (var w = new Utf8JsonWriter(ms))
        {
            w.WriteStartObject();
            w.WriteString("jsonrpc", "2.0");
            w.WritePropertyName("id");
            id.WriteTo(w);
            w.WritePropertyName("error");
            w.WriteStartObject();
            w.WriteNumber("code", code);
            w.WriteString("message", message);
            w.WriteEndObject();
            w.WriteEndObject();
        }
        return ms.ToArray();
    }

    private static async Task WriteAsync(HttpListenerContext ctx, int status, string? contentType, byte[]? body, CancellationToken ct)
    {
        var response = ctx.Response;
        response.StatusCode = status;
        if (contentType is not null)
        {
            response.ContentType = contentType;
            response.ContentEncoding = Encoding.UTF8;
        }
        if (body is not null)
        {
            response.ContentLength64 = body.Length;
            await response.OutputStream.WriteAsync(body, ct).ConfigureAwait(false);
        }
        response.Close();
    }

    private volatile bool _stopped;

    private void StopListener()
    {
        if (_stopped) return;
        _stopped = true;
        try { _cts?.Cancel(); } catch { /* best effort */ }
        try { _listener?.Stop(); } catch { /* best effort */ }
        try { _listener?.Close(); } catch { /* best effort */ }
    }

    // Both IDisposable and IAsyncDisposable are implemented so the DI container can
    // tear the singleton down whether it disposes synchronously or asynchronously —
    // an async-only singleton throws "Use DisposeAsync" on a synchronous container dispose.
    public void Dispose()
    {
        StopListener();
        _cts?.Dispose();
    }

    public async ValueTask DisposeAsync()
    {
        StopListener();
        if (_acceptLoop is not null)
        {
            try { await _acceptLoop.ConfigureAwait(false); } catch { /* drained */ }
        }
        _cts?.Dispose();
    }
}
