using FluentAssertions;
using Xunit;

namespace Zonit.Extensions.Ai.Tests.Agent;

/// <summary>
/// Tests for the <see cref="Mcp"/> descriptor value type.
/// </summary>
public class McpTests
{
    [Fact]
    public void Ctor_ShouldAssignAllProperties()
    {
        var mcp = new Mcp("github", "https://mcp.example.com/sse", "secret");

        mcp.Name.Should().Be("github");
        mcp.Url.Should().Be("https://mcp.example.com/sse");
        mcp.Token.Should().Be("secret");
        mcp.AllowedTools.Should().BeNull();
    }

    [Fact]
    public void Ctor_ShouldAllowNullToken()
    {
        var mcp = new Mcp("public", "https://mcp.example.com/sse");
        mcp.Token.Should().BeNull();
    }

    [Fact]
    public void Ctor_ShouldAcceptAllowList()
    {
        var allowed = new[] { "get_gold_price", "get_cot_data" };
        var mcp = new Mcp("gold", "https://mcp.example.com/sse",
                          token: null, allowedTools: allowed);

        mcp.AllowedTools.Should().BeEquivalentTo(allowed);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Ctor_ShouldRejectEmptyUrl(string? url)
    {
        var act = () => new Mcp("name", url!);
        act.Should().Throw<ArgumentException>().WithParameterName("url");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Ctor_ShouldRejectEmptyName(string? name)
    {
        var act = () => new Mcp(name!, "https://mcp.example.com/sse");
        act.Should().Throw<ArgumentException>().WithParameterName("name");
    }

    [Theory]
    [InlineData("http://mcp.example.com/sse")]        // not HTTPS
    [InlineData("/relative/path")]                     // not absolute
    [InlineData("ftp://mcp.example.com")]              // wrong scheme
    [InlineData("not-a-url")]                          // garbage
    public void Ctor_ShouldRejectNonHttpsOrInvalidUrl(string url)
    {
        var act = () => new Mcp("name", url);
        act.Should().Throw<ArgumentException>().WithParameterName("url");
    }

    [Fact]
    public void RecordEquality_ShouldConsiderAllFields()
    {
        var a = new Mcp("github", "https://mcp.example.com/sse", "tok1");
        var b = new Mcp("github", "https://mcp.example.com/sse", "tok1");
        var c = new Mcp("github", "https://mcp.example.com/sse", "tok2");

        a.Should().Be(b);
        a.Should().NotBe(c);
    }
}
