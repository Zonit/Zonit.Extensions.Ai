using Microsoft.Extensions.Hosting;
using Zonit.Extensions.Ai;
using Zonit.Extensions.Ai.Llm.OpenAi;
using Zonit.Extensions.Ai.Prompts;
using Zonit.Extensions.Ai.Domain.Models;

namespace Example.Backgrounds;
//IImageClient imageClient
internal class TextBackground(IAiClient client) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        //var translate = new TranslatePrompt
        //{
        //    Content = "Hello world!",
        //    Language = "pl",
        //    Culture = "pl"
        //};

        //var search = new SearchPrompt
        //{
        //    Query = "Hotel w Warszawie",
        //};

        //var personal = new PersonInfoPrompt
        //{
        //    Name = "Dawid",
        //    Age = 30
        //};

        var testPrompt = new TestPrompt();

        var test = await client.GenerateAsync(testPrompt, new GPT41
        {
            MaxTokens = 5000,
            StoreLogs = true
        });

        Console.WriteLine(test);

        // Test image analysis with files
        try
        {
            // Try to load an existing image file
            var imagePath = @"D:\image\generated_image_20250711_133854_320.png";
            
            if (File.Exists(imagePath))
            {
                Console.WriteLine($"Ładowanie obrazu z: {imagePath}");
                
                // Load the image file
                var imageFile = await FileModel.CreateFromFilePathAsync(imagePath);
                
                // Now analyze the image
                var imageAnalysisPrompt = new ImageAnalysisPrompt
                {
                    Files = new[] { imageFile }
                };

                var analysis = await client.GenerateAsync(imageAnalysisPrompt, new GPT41
                {
                    MaxTokens = 2000,
                    StoreLogs = true
                }, stoppingToken);

                if (analysis?.Value != null)
                {
                    Console.WriteLine($"Opis obrazu: {analysis.Value.Description}");
                    Console.WriteLine($"Kategoria: {analysis.Value.Category}");
                    Console.WriteLine($"Główne obiekty: {string.Join(", ", analysis.Value.MainObjects)}");
                    Console.WriteLine($"Koszt analizy - wejściowy: {analysis.MetaData.PriceInput} wyjściowy: {analysis.MetaData.PriceOutput} całkowity: {analysis.MetaData.PriceTotal}");
                }
                else
                {
                    Console.WriteLine("Nie udało się przeanalizować obrazu.");
                }
            }
            else
            {
                Console.WriteLine($"Plik obrazu nie istnieje: {imagePath}");
                Console.WriteLine("Generuję nowy obraz...");
                
                // Generate a new image if the file doesn't exist
                var animal = new AnimalPrompt
                {
                    Animal = "pies",
                };

                var image = await client.GenerateAsync(animal, new GPTImage1
                {
                    Quality = GPTImage1.QualityType.Low,
                    Size = GPTImage1.SizeType.Landscape
                }, stoppingToken);

                if (image?.Value != null)
                {
                    Console.WriteLine("Wygenerowano obraz, teraz analizuję...");

                    // Now analyze the generated image
                    var imageAnalysisPrompt = new ImageAnalysisPrompt
                    {
                        Files = new[] { image.Value }
                    };

                    var analysis = await client.GenerateAsync(imageAnalysisPrompt, new GPT41
                    {
                        MaxTokens = 2000,
                        StoreLogs = true
                    }, stoppingToken);

                    if (analysis?.Value != null)
                    {
                        Console.WriteLine($"Opis obrazu: {analysis.Value.Description}");
                        Console.WriteLine($"Kategoria: {analysis.Value.Category}");
                        Console.WriteLine($"Główne obiekty: {string.Join(", ", analysis.Value.MainObjects)}");
                    }
                    else
                    {
                        Console.WriteLine("Nie udało się przeanalizować obrazu.");
                    }

                    // Save the generated image
                    var directory = @"D:\image";
                    Directory.CreateDirectory(directory);

                    var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss_fff");
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
        catch (Exception ex)
        {
            Console.WriteLine($"Błąd podczas testowania analizy obrazu: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
        }

        return;
    }
}
