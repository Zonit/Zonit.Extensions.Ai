using System.Text;
using Example.Backgrounds;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Zonit.Extensions.Ai;
using Zonit.Extensions.Ai.OpenAi;

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
        Console.OutputEncoding = Encoding.UTF8;
        Console.InputEncoding = Encoding.UTF8;

        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
        {
            Args = args
        });

        builder.Configuration.AddConfiguration(CreateConfiguration(args));

        // Get API key from configuration
        var apiKey = builder.Configuration["OpenAi:ApiKey"] ?? "";
        
        // Register AI with OpenAI provider - single call!
        builder.Services.AddOpenAi(options =>
        {
            options.OpenAi.ApiKey = apiKey;
        });
        
        builder.Services.AddHostedService<TextBackground>();

        var app = builder.Build();
        app.Run();
    }
}
