using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Zonit.Extensions.Ai.Application.Services;
using Zonit.Extensions.Ai.Domain.Models;
using Zonit.Extensions.Ai.Domain.Repositories;
using Zonit.Extensions.Ai.Llm;

namespace Zonit.Extensions.Ai.Infrastructure.Repositories.OpenAi;

public class OpenAiImageRepository(HttpClient httpClient) : IImageRepository
{
    private readonly HttpClient _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));

    public async Task<Result<IFile>> GenerateAsync(IImageLlmBase llm, IPromptBase<IFile> prompt, CancellationToken cancellationToken = default)
    {
        if (llm.Quantity > 1)
            throw new ArgumentException("Method does not support multiple images.", nameof(llm));

        var requestBody = new
        {
            model = llm.Name,
            prompt = PromptService.BuildPrompt(prompt),
            n = llm.Quantity,
            size = llm.SizeValue,
            quality = llm.QualityValue,
        };

        var stopwatch = Stopwatch.StartNew();

        var response = await _httpClient.PostAsJsonAsync(
            "v1/images/generations",
            requestBody,
            cancellationToken);

        response.EnsureSuccessStatusCode();

        var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
        stopwatch.Stop();

        var responseData = JsonSerializer.Deserialize<OpenAIImageResponse>(responseJson);
        if (responseData?.Data == null || responseData.Data.Length == 0)
            throw new InvalidOperationException("No image data returned");

        var imageBytes = Convert.FromBase64String(responseData.Data[0].B64Json);

        return new Result<IFile>
        {
            Value = new FileModel("", "image/png", imageBytes),
            MetaData = new(llm, new Usage
            {
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
            public required string B64Json { get; set; }
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
