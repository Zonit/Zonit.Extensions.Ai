using Microsoft.Extensions.DependencyInjection;
using Zonit.Extensions.Ai.Domain.Repositories;
using Zonit.Extensions.Ai.Infrastructure.Repositories.OpenAi;

namespace Zonit.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddAiInfrastructureExtension(this IServiceCollection services)
    {
        services.AddTransient<IAiRepository, OpenAiRepository>();

        return services;
    }
}