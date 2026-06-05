using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Zonit.Extensions;
using Zonit.Extensions.Ai;
using Zonit.Extensions.Ai.OpenAi;
using Zonit.Extensions.Ai.Anthropic;

namespace Zonit.Extensions.Ai.Tests.Providers;

/// <summary>
/// Tests for idempotent DI registration of AI providers.
/// Ensures multiple plugins can safely call registration methods without errors.
/// </summary>
public class ServiceCollectionExtensionsTests
{
    [Fact]
    public void AddAiOpenAi_CalledMultipleTimes_ShouldNotThrow()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act - Simulate multiple plugins registering OpenAI
        var action = () =>
        {
            services.AddAiOpenAi("key-1");
            services.AddAiOpenAi("key-2");
            services.AddAiOpenAi("key-3");
        };

        // Assert
        action.Should().NotThrow();
    }

    [Fact]
    public void AddAiOpenAi_CalledMultipleTimes_ShouldRegisterProviderOnlyOnce()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddAiOpenAi("key-1");
        services.AddAiOpenAi("key-2");
        services.AddAiOpenAi("key-3");

        // Assert - Should have exactly 2 registrations (keyed + factory transient)
        var providerRegistrations = services
            .Where(s => s.ServiceType == typeof(IModelProvider))
            .ToList();

        // Count OpenAI-related registrations (keyed + factory)
        var openAiRegistrations = providerRegistrations.Count(s =>
            (s.ServiceKey is Type t && t == typeof(OpenAiProvider)) ||
            s.ImplementationType == typeof(OpenAiProvider) ||
            s.ImplementationFactory != null);

        // Should have 2: keyed service + factory delegate
        openAiRegistrations.Should().Be(2);
    }

    [Fact]
    public void AddAiAnthropic_CalledMultipleTimes_ShouldNotThrow()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        var action = () =>
        {
            services.AddAiAnthropic("key-1");
            services.AddAiAnthropic("key-2");
        };

        // Assert
        action.Should().NotThrow();
    }

    [Fact]
    public void IsProviderRegistered_WhenNotRegistered_ShouldReturnFalse()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act & Assert
        services.IsProviderRegistered<OpenAiProvider>().Should().BeFalse();
    }

    [Fact]
    public void IsProviderRegistered_WhenRegistered_ShouldReturnTrue()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddAiOpenAi("key");

        // Act & Assert
        services.IsProviderRegistered<OpenAiProvider>().Should().BeTrue();
    }

    [Fact]
    public void TryAddModelProvider_WhenAlreadyRegistered_ShouldNotAddDuplicate()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddAiOpenAi("key");
        var countBefore = services.Count(s => s.ServiceType == typeof(IModelProvider));

        // Act
        services.TryAddModelProvider<OpenAiProvider>();
        var countAfter = services.Count(s => s.ServiceType == typeof(IModelProvider));

        // Assert
        countAfter.Should().Be(countBefore);
    }

    [Fact]
    public void MultipleProviders_ShouldNotDuplicateRegistrations()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act - Register multiple providers multiple times
        services.AddAiOpenAi("openai-key");
        services.AddAiOpenAi("openai-key"); // Duplicate - should be ignored
        services.AddAiAnthropic("anthropic-key");
        services.AddAiAnthropic("anthropic-key"); // Duplicate - should be ignored

        // Assert - Count IModelProvider registrations
        var providerRegistrations = services
            .Where(s => s.ServiceType == typeof(IModelProvider))
            .ToList();

        // Each provider should have exactly 2 registrations (keyed + factory)
        // So total should be 4 (2 providers x 2 registrations each)
        providerRegistrations.Should().HaveCount(4);
    }
}
