using Microsoft.Extensions.Hosting;
using Zonit.Extensions.AI;

namespace Example.Backgrounds;

internal class TextBackground(IImageClient imageClient) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var prompt = "A beautiful sunset over the mountains";

        var image = await imageClient.GenerateImageAsync(prompt, new GPTImage1 { 
            Quality = GPTImage1.QualityType.Low,
            Size = GPTImage1.SizeType.Landscape
        }, stoppingToken);


        if (image?.Value != null)
        {
            var imageBytes = image.Value;
            var filePath = @"D:\generated_image.png";
            await File.WriteAllBytesAsync(filePath, image.Value.Data, stoppingToken);
            Console.WriteLine($"Obraz zapisano w lokalizacji: {filePath}");
            Console.WriteLine($"Koszt wejściowy: {image.MetaData.PriceInput} wyjściowy: {image.MetaData.PriceOutput} całkowity: {image.MetaData.PriceTotal}");
        }
        else
        {
            Console.WriteLine("Nie udało się wygenerować obrazu.");
        }
    }
}
