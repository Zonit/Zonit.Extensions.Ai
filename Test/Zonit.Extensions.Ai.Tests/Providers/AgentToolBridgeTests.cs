using System.Net;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using Zonit.Extensions.Ai;
using Zonit.Extensions.Ai.Sdk;

namespace Zonit.Extensions.Ai.Tests.Providers;

/// <summary>
/// End-to-end tests for the loopback MCP tool bridge — drives the real
/// <see cref="AgentToolBridge"/>/<c>LoopbackMcpServer</c> over HTTP exactly as a local
/// MCP client (claude -p) would, with no real <c>claude</c> binary involved.
/// </summary>
public class AgentToolBridgeTests
{
    [Fact]
    public async Task Bridge_Initialize_ListTools_CallTool_RoundTrips()
    {
        await using var bridge = new AgentToolBridge(NullLogger<AgentToolBridge>.Instance);
        var session = await bridge.StartAsync([new EchoTool()], CancellationToken.None);
        using var http = new HttpClient();

        // initialize → echoes the requested protocol version + advertises serverInfo.
        var init = await RpcAsync(http, session.Url, session.AuthToken,
            """{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2025-06-18"}}""");
        init.status.Should().Be(200);
        init.json!.RootElement.GetProperty("result").GetProperty("protocolVersion").GetString().Should().Be("2025-06-18");
        init.json.RootElement.GetProperty("result").GetProperty("serverInfo").GetProperty("name").GetString().Should().NotBeNullOrEmpty();

        // tools/list → exposes the registered tool.
        var list = await RpcAsync(http, session.Url, session.AuthToken,
            """{"jsonrpc":"2.0","id":2,"method":"tools/list"}""");
        list.status.Should().Be(200);
        var tools = list.json!.RootElement.GetProperty("result").GetProperty("tools").EnumerateArray().ToList();
        tools.Should().ContainSingle();
        tools[0].GetProperty("name").GetString().Should().Be("echo");
        tools[0].GetProperty("inputSchema").GetProperty("type").GetString().Should().Be("object");

        // tools/call → executes ITool.InvokeAsync and returns its output as text content.
        var call = await RpcAsync(http, session.Url, session.AuthToken,
            """{"jsonrpc":"2.0","id":3,"method":"tools/call","params":{"name":"echo","arguments":{"value":"hi"}}}""");
        call.status.Should().Be(200);
        var result = call.json!.RootElement.GetProperty("result");
        result.GetProperty("isError").GetBoolean().Should().BeFalse();
        result.GetProperty("content")[0].GetProperty("text").GetString().Should().Be("echo:hi");

        init.json.Dispose();
        list.json.Dispose();
        call.json.Dispose();
    }

    [Fact]
    public async Task Bridge_RejectsMissingOrWrongToken_With401()
    {
        await using var bridge = new AgentToolBridge(NullLogger<AgentToolBridge>.Instance);
        var session = await bridge.StartAsync([new EchoTool()], CancellationToken.None);
        using var http = new HttpClient();

        var noToken = await RpcAsync(http, session.Url, token: null,
            """{"jsonrpc":"2.0","id":1,"method":"tools/list"}""");
        noToken.status.Should().Be(401);

        var wrongToken = await RpcAsync(http, session.Url, token: "not-the-token",
            """{"jsonrpc":"2.0","id":1,"method":"tools/list"}""");
        wrongToken.status.Should().Be(401);
    }

    [Fact]
    public async Task Bridge_AfterSessionDisposed_TokenIsRevoked()
    {
        await using var bridge = new AgentToolBridge(NullLogger<AgentToolBridge>.Instance);
        var session = await bridge.StartAsync([new EchoTool()], CancellationToken.None);
        var url = session.Url;
        var token = session.AuthToken;
        using var http = new HttpClient();

        (await RpcAsync(http, url, token, """{"jsonrpc":"2.0","id":1,"method":"tools/list"}""")).status.Should().Be(200);

        await session.DisposeAsync();

        (await RpcAsync(http, url, token, """{"jsonrpc":"2.0","id":2,"method":"tools/list"}""")).status.Should().Be(401);
    }

    [Fact]
    public async Task Bridge_ScopedTool_BoundWithContext_ReceivesItOnCall()
    {
        // A scoped tool (ToolBase<TScope,…>) needs server context the model never sees. The CLI calls
        // tools over the bridge through the context-less ITool path, so AgentToolContextBinder must inject
        // the captured context — otherwise the tool throws and the call yields nothing (the original bug).
        var bound = AgentToolContextBinder.Bind([new ScopedEchoTool()], ["CTX"], chat: null);

        await using var bridge = new AgentToolBridge(NullLogger<AgentToolBridge>.Instance);
        var session = await bridge.StartAsync(bound, CancellationToken.None);
        using var http = new HttpClient();

        var call = await RpcAsync(http, session.Url, session.AuthToken,
            """{"jsonrpc":"2.0","id":1,"method":"tools/call","params":{"name":"scoped_echo","arguments":{"value":"hi"}}}""");
        call.status.Should().Be(200);
        var result = call.json!.RootElement.GetProperty("result");
        result.GetProperty("isError").GetBoolean().Should().BeFalse();
        result.GetProperty("content")[0].GetProperty("text").GetString().Should().Be("CTX:hi");
        call.json.Dispose();
    }

    [Fact]
    public async Task Bridge_ScopedTool_BoundWithoutContext_ReportsToolError()
    {
        // No matching context supplied: a wiring mistake. The in-process runner throws to the developer;
        // over the bridge there is no developer to reach, so the bound tool surfaces it as a tool error
        // the model sees (isError=true) rather than failing silently.
        var bound = AgentToolContextBinder.Bind([new ScopedEchoTool()], context: null, chat: null);

        await using var bridge = new AgentToolBridge(NullLogger<AgentToolBridge>.Instance);
        var session = await bridge.StartAsync(bound, CancellationToken.None);
        using var http = new HttpClient();

        var call = await RpcAsync(http, session.Url, session.AuthToken,
            """{"jsonrpc":"2.0","id":1,"method":"tools/call","params":{"name":"scoped_echo","arguments":{"value":"hi"}}}""");
        call.status.Should().Be(200);
        call.json!.RootElement.GetProperty("result").GetProperty("isError").GetBoolean().Should().BeTrue();
        call.json.Dispose();
    }

    private static async Task<(int status, JsonDocument? json)> RpcAsync(HttpClient http, Uri url, string? token, string body)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        };
        if (token is not null)
            request.Headers.Add("Authorization", "Bearer " + token);

        using var response = await http.SendAsync(request);
        var text = await response.Content.ReadAsStringAsync();
        var json = string.IsNullOrWhiteSpace(text) ? null : JsonDocument.Parse(text);
        return ((int)response.StatusCode, json);
    }

    private sealed class EchoTool : ITool
    {
        public string Name => "echo";
        public string Description => "Echoes the input value.";
        public JsonElement InputSchema =>
            JsonDocument.Parse("""{"type":"object","properties":{"value":{"type":"string"}}}""").RootElement;

        public Task<JsonElement> InvokeAsync(JsonElement arguments, CancellationToken cancellationToken)
        {
            var value = arguments.TryGetProperty("value", out var v) ? v.GetString() : "(none)";
            return Task.FromResult(JsonSerializer.SerializeToElement("echo:" + value));
        }
    }

    // Hand-rolled IScopedTool mirroring ToolBase<TScope,TInput,TOutput>: the context-less ITool path
    // throws (as the real base does), and the real work runs only when given a context. TScope is string.
    private sealed class ScopedEchoTool : IScopedTool
    {
        public string Name => "scoped_echo";
        public string Description => "Echoes the input value, prefixed by the server context.";
        public JsonElement InputSchema =>
            JsonDocument.Parse("""{"type":"object","properties":{"value":{"type":"string"}}}""").RootElement;

        public Type ContextType => typeof(string);

        public Task<JsonElement> InvokeAsync(JsonElement arguments, object context, CancellationToken cancellationToken)
        {
            var value = arguments.TryGetProperty("value", out var v) ? v.GetString() : "(none)";
            return Task.FromResult(JsonSerializer.SerializeToElement($"{context}:{value}"));
        }

        public Task<JsonElement> InvokeAsync(JsonElement arguments, CancellationToken cancellationToken)
            => throw new AiToolContextException($"Tool '{Name}' is scoped and must be invoked with a context.");
    }
}
