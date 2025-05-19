using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Text.Json;
using Zonit.Extensions.Ai.Models;
using Microsoft.Extensions.Options;
using Zonit.Extensions.Ai.Abstractions.Options;

namespace Zonit.Extensions.Ai.Services.OpenAi;

public class OpenAiImageService
{
    readonly HttpClient _httpClient;

    public OpenAiImageService(
       HttpClient httpClient,
       IOptions<AiOptions> options
    )
    {
        _httpClient = httpClient ?? new HttpClient();
        _httpClient.Timeout = TimeSpan.FromMinutes(10);
        _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {options.Value.OpenAiKey}");
    }

    public async Task<Result<IFile>> GenerateAsync(
        string prompt, 
        IImageModel model, 
        CancellationToken cancellationToken = default)
    {
        if(model.Quantity > 1)
            throw new ArgumentException("Method does not support multiple images.", nameof(model));


        var stopwatch = new Stopwatch();
        
        var requestBody = new
        {
            model = model.Name,
            prompt = prompt,
            n = model.Quantity,
            size = model.SizeValue,
            quality = model.QualityValue,
        };

        var response = await _httpClient.PostAsJsonAsync(
            "https://api.openai.com/v1/images/generations",
            requestBody,
            cancellationToken
        );

        response.EnsureSuccessStatusCode(); 

        stopwatch.Start();
        var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
        stopwatch.Stop();

        var responseData = JsonSerializer.Deserialize<OpenAIImageResponse>(responseJson);
        var imageBytes = Convert.FromBase64String(responseData.Data[0].B64Json);

        return new Result<IFile>
        {
            Value = new FileModel("", "image/png", imageBytes),
            MetaData = new(model, new Usage {
                Input = responseData.Usage.InputTokens,
                InputDetails = new Usage.Details
                {
                    Text = responseData.Usage.InputTokensDetails.TextTokens,
                    Image = responseData.Usage.InputTokensDetails.ImageTokens,
                },
                Output = responseData.Usage.OutputTokens,
            }, stopwatch.Elapsed)
        };
  
    }

    private class OpenAIImageResponse
    {
        [JsonPropertyName("created")]
        public long Created { get; set; }

        [JsonPropertyName("data")]
        public required ImageData[] Data { get; set; }

        [JsonPropertyName("usage")]
        public required UsageInfo Usage { get; set; }

        public class ImageData
        {
            [JsonPropertyName("b64_json")]
            public string B64Json { get; set; }
        }

        public class UsageInfo
        {
            [JsonPropertyName("input_tokens")]
            public int InputTokens { get; set; }

            [JsonPropertyName("input_tokens_details")]
            public required TokensDetails InputTokensDetails { get; set; }

            [JsonPropertyName("output_tokens")]
            public int OutputTokens { get; set; }

            [JsonPropertyName("total_tokens")]
            public int TotalTokens { get; set; }

            public class TokensDetails
            {
                [JsonPropertyName("image_tokens")]
                public int ImageTokens { get; set; }

                [JsonPropertyName("text_tokens")]
                public int TextTokens { get; set; }
            }
        }
    }
}
