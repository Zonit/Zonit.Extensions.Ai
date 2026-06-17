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
}
