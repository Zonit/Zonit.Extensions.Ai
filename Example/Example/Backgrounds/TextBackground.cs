using Microsoft.Extensions.Hosting;
using Zonit.Extensions.AI;

namespace Example.Backgrounds;

internal class TextBackground(IImageClient imageClient) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var prompt = "Beautiful sunset over the mountains with the color {{$color}} of the sun.";


        var image = await imageClient
            .AddVariable("color", "red")
            .GenerateImageAsync(prompt, new GPTImage1 { 
            Quality = GPTImage1.QualityType.High,
            Size = GPTImage1.SizeType.Landscape
        }, stoppingToken);


        if (image?.Value != null)
        {
            var imageBytes = image.Value;

            var directory = @"D:\image";
            Directory.CreateDirectory(directory); // upewniamy się, że katalog istnieje

            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss_fff"); // format np. 20250428_154312_123
            var filePath = Path.Combine(directory, $"generated_image_{timestamp}.png");

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
