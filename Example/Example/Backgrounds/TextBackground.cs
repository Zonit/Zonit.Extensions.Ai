using Microsoft.Extensions.Hosting;
using OpenAI.Images;
using Zonit.Extensions.Ai;
using Zonit.Extensions.Ai.Application.Services;
using Zonit.Extensions.Ai.Domain.Repositories;
using Zonit.Extensions.Ai.Llm.OpenAi;
using Zonit.Extensions.Ai.Prompts;

namespace Example.Backgrounds;
//IImageClient imageClient
internal class TextBackground(IAiRepository aiRepository) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var translate = new TranslatePrompt
        {
            Content = "Hello world!",
            Language = "pl",
            Culture = "pl"
        };

        var search = new SearchPrompt
        {
            Query = "Hotel w Warszawie",
        };

        var personal = new PersonInfoPrompt
        {
            Name = "Dawid",
            Age = 30
        };

        var test = await aiRepository.ResponseAsync(new GPT41 { }, personal);

        Console.WriteLine(test);
        //var image = await imageClient
        //    .AddVariable("color", "red")
        //    .GenerateImageAsync(prompt, new GPTImage1 { 
        //    Quality = GPTImage1.QualityType.Low,
        //    Size = GPTImage1.SizeType.Landscape
        //}, stoppingToken);


        //if (image?.Value != null)
        //{
        //    var imageBytes = image.Value;

        //    var directory = @"D:\image";
        //    Directory.CreateDirectory(directory); // upewniamy się, że katalog istnieje

        //    var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss_fff"); // format np. 20250428_154312_123
        //    var filePath = Path.Combine(directory, $"generated_image_{timestamp}.png");

        //    await File.WriteAllBytesAsync(filePath, image.Value.Data, stoppingToken);

        //    Console.WriteLine($"Obraz zapisano w lokalizacji: {filePath}");
        //    Console.WriteLine($"Koszt wejściowy: {image.MetaData.PriceInput} wyjściowy: {image.MetaData.PriceOutput} całkowity: {image.MetaData.PriceTotal}");
        //}
        //else
        //{
        //    Console.WriteLine("Nie udało się wygenerować obrazu.");
        //}
    }
}
