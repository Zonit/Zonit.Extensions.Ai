using System.Text;
using Example.Backgrounds;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Zonit.Extensions;
using Zonit.Extensions.Ai;
using IOFile = System.IO.File;

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

        if (!IOFile.Exists("appsettings.json"))
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

        // Register all AI providers - configuration loaded from appsettings.json "Ai:*"
        builder.Services.AddAiOpenAi();      // OpenAI (GPT-4, o1, DALL-E, etc.)
        builder.Services.AddAiAnthropic();   // Anthropic (Claude)
        builder.Services.AddAiGoogle();      // Google (Gemini)
        builder.Services.AddAiMistral();     // Mistral
        builder.Services.AddAiDeepSeek();    // DeepSeek
        builder.Services.AddAiX();           // X/Grok

        // Use ComprehensiveTestBackground for full provider testing
        builder.Services.AddHostedService<ComprehensiveTestBackground>();
        // Alternative: builder.Services.AddHostedService<ProductDescriptionTestBackground>();

        var app = builder.Build();
        app.Run();
    }
}
