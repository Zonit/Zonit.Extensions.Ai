//using System.Diagnostics;
//using System.Net.Http.Json;
//using System.Text.Json.Serialization;
//using System.Text.Json;
//using Zonit.Extensions.Ai.Models;

//namespace Zonit.Extensions.Ai.Services.OpenAi;

//public class OpenAiImageService
//{
//    private readonly HttpClient _httpClient;

//    public OpenAiImageService(HttpClient httpClient)
//    {
//        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));

//        // test
//        if (_httpClient.Timeout < TimeSpan.FromMinutes(5))
//        {
//            _httpClient.Timeout = TimeSpan.FromMinutes(5);
//        }
//    }

//    public async Task<Result<IFile>> GenerateAsync(
//        string prompt,
//        IImageBase model,
//        CancellationToken cancellationToken = default)
//    {
//        if (model.Quantity > 1)
//            throw new ArgumentException("Method does not support multiple images.", nameof(model));

//        var requestBody = new
//        {
//            model = model.Name,
//            prompt = prompt,
//            n = model.Quantity,
//            size = model.SizeValue,
//            quality = model.QualityValue,
//        };

//        var stopwatch = Stopwatch.StartNew();

//        var response = await _httpClient.PostAsJsonAsync(
//            "v1/images/generations",
//            requestBody,
//            cancellationToken);

//        response.EnsureSuccessStatusCode();

//        var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
//        stopwatch.Stop();

//        var responseData = JsonSerializer.Deserialize<OpenAIImageResponse>(responseJson);
//        if (responseData?.Data == null || responseData.Data.Length == 0)
//            throw new InvalidOperationException("No image data returned");
        
//        var imageBytes = Convert.FromBase64String(responseData.Data[0].B64Json);

//        return new Result<IFile>
//        {
//            Value = new FileModel("", "image/png", imageBytes),
//            MetaData = new(model, new Usage
//            {
//                Input = responseData.Usage.InputTokens,
//                InputDetails = new Usage.Details
//                {
//                    Text = responseData.Usage.InputTokensDetails.TextTokens,
//                    Image = responseData.Usage.InputTokensDetails.ImageTokens,
//                },
//                Output = responseData.Usage.OutputTokens,
//            }, stopwatch.Elapsed)
//        };
//    }


//    private class OpenAIImageResponse
//    {
//        [JsonPropertyName("created")]
//        public long Created { get; set; }

//        [JsonPropertyName("data")]
//        public required ImageData[] Data { get; set; }

//        [JsonPropertyName("usage")]
//        public required UsageInfo Usage { get; set; }

//        public class ImageData
//        {
//            [JsonPropertyName("b64_json")]
//            public required string B64Json { get; set; }
//        }

//        public class UsageInfo
//        {
//            [JsonPropertyName("input_tokens")]
//            public int InputTokens { get; set; }

//            [JsonPropertyName("input_tokens_details")]
//            public required TokensDetails InputTokensDetails { get; set; }

//            [JsonPropertyName("output_tokens")]
//            public int OutputTokens { get; set; }

//            [JsonPropertyName("total_tokens")]
//            public int TotalTokens { get; set; }

//            public class TokensDetails
//            {
//                [JsonPropertyName("image_tokens")]
//                public int ImageTokens { get; set; }

//                [JsonPropertyName("text_tokens")]
//                public int TextTokens { get; set; }
//            }
//        }
//    }
//}
