using System.Net;
using FluentAssertions;
using Xunit;
using Zonit.Extensions;

namespace Zonit.Extensions.Ai.Tests.Providers;

/// <summary>
/// Deterministic guard for <see cref="HttpClientBuilderExtensions.ResolveProxy"/> —
/// the pure translation from <see cref="AiProxyOptions"/> to an
/// <see cref="IWebProxy"/> that every provider's socket handler applies. Networking
/// config is easy to get subtly wrong, so the null/enabled/address/credential
/// branches are pinned here.
/// </summary>
public class ProxyResolutionTests
{
    [Fact]
    public void NoOptions_ReturnsNull()
        => HttpClientBuilderExtensions.ResolveProxy(null).Should().BeNull();

    [Fact]
    public void NoAddress_ReturnsNull()
        => HttpClientBuilderExtensions.ResolveProxy(new AiProxyOptions()).Should().BeNull();

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void BlankAddress_ReturnsNull(string address)
        => HttpClientBuilderExtensions.ResolveProxy(new AiProxyOptions { Address = address }).Should().BeNull();

    [Fact]
    public void Disabled_ReturnsNull_EvenWithAddress()
    {
        var options = new AiProxyOptions { Enabled = false, Address = "http://host:8080" };
        HttpClientBuilderExtensions.ResolveProxy(options).Should().BeNull();
    }

    [Fact]
    public void HttpAddress_NoCredentials_ProducesAnonymousProxy()
    {
        var proxy = HttpClientBuilderExtensions.ResolveProxy(new AiProxyOptions { Address = "http://us-proxy:8080" });

        proxy.Should().BeOfType<WebProxy>();
        var target = proxy!.GetProxy(new Uri("https://api.x.ai/v1/responses"))!;
        target.Host.Should().Be("us-proxy");
        target.Port.Should().Be(8080);
        target.Scheme.Should().Be("http");
        proxy.Credentials.Should().BeNull();
    }

    [Fact]
    public void Credentials_AreAttached_WhenUsernameSet()
    {
        var proxy = HttpClientBuilderExtensions.ResolveProxy(new AiProxyOptions
        {
            Address = "http://us-proxy:8080",
            Username = "user",
            Password = "secret",
        });

        var creds = proxy!.Credentials.Should().BeOfType<NetworkCredential>().Subject;
        creds.UserName.Should().Be("user");
        creds.Password.Should().Be("secret");
    }

    [Fact]
    public void SocksScheme_IsPreserved()
    {
        var proxy = HttpClientBuilderExtensions.ResolveProxy(new AiProxyOptions { Address = "socks5://us-proxy:1080" });

        var target = proxy!.GetProxy(new Uri("https://api.x.ai/v1/responses"))!;
        target.Scheme.Should().Be("socks5");
        target.Host.Should().Be("us-proxy");
        target.Port.Should().Be(1080);
    }
}
