using Example.Backgrounds;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Zonit.Extensions;

namespace Example;

internal class Program
{
    public static IConfiguration CreateConfiguration(string[] args)
    {
        var builder = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
            .AddCommandLine(args);

        var configuration = builder.Build();

        if (!File.Exists("appsettings.json"))
            throw new FileNotFoundException("Nie znaleziono pliku ustawień appsettings.json.");

        return configuration;
    }
    static void Main(string[] args)
    {
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
        {
            Args = args
        });

        builder.Configuration.AddConfiguration(CreateConfiguration(args));

        builder.Services.AddHttpClient();
        builder.Services.AddAiExtension();
        builder.Services.AddHostedService<TextBackground>();


        var app = builder.Build();
        app.Run();
    }
}
