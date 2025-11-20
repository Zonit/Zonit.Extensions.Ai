using Microsoft.Extensions.Options;
using System.Diagnostics;
using System.Text.Json;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Unicode;
using System.Text.Json.Serialization;
using Zonit.Extensions.Ai.Application.Options;
using Zonit.Extensions.Ai.Domain.Repositories;
using Zonit.Extensions.Ai.Infrastructure.Serialization;
using Zonit.Extensions.Ai.Llm;
using Zonit.Extensions.Ai.Domain.Models;

namespace Zonit.Extensions.Ai.Infrastructure.Repositories.Google;

internal class GoogleRepository(IOptions<AiOptions> options, HttpClient httpClient) : ITextRepository
{
    private const string BaseUrl = "https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
        Encoder = JavaScriptEncoder.Create(UnicodeRanges.All),
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private static readonly JsonSerializerOptions DeserializationOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        Encoder = JavaScriptEncoder.Create(UnicodeRanges.All)
    };

    public async Task<Result<TResponse>> ResponseAsync<TResponse>(ITextLlmBase llm, IPromptBase<TResponse> prompt, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(options.Value.GoogleKey))
        {
            throw new InvalidOperationException("Google API key is not configured.");
        }

        var requestBody = BuildRequestBody(llm, prompt);
        var url = BaseUrl.Replace("{model}", llm.Name);
        var fullUrl = $"{url}?key={options.Value.GoogleKey}";
        
        var requestContent = new StringContent(JsonSerializer.Serialize(requestBody, JsonOptions), Encoding.UTF8, "application/json");

        httpClient.DefaultRequestHeaders.Clear();

        var stopwatch = new Stopwatch();
        stopwatch.Start();

        try
        {
            var response = await httpClient.PostAsync(fullUrl, requestContent, cancellationToken);
            stopwatch.Stop();

            var responseBytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
            var responseContent = Encoding.UTF8.GetString(responseBytes);

            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException($"Google Gemini API request failed with status {response.StatusCode}: {responseContent}");
            }

            var geminiResponse = JsonSerializer.Deserialize<GeminiApiResponse>(responseContent, DeserializationOptions);

            if (geminiResponse?.Candidates == null || !geminiResponse.Candidates.Any())
            {
                throw new InvalidOperationException($"Google Gemini API response does not contain valid candidates. Response: {responseContent}");
            }

            var firstCandidate = geminiResponse.Candidates.First();
            if (firstCandidate?.Content?.Parts == null || !firstCandidate.Content.Parts.Any())
            {
                throw new InvalidOperationException($"Google Gemini API response does not contain content parts. Full response: {responseContent}");
            }

            var textPart = firstCandidate.Content.Parts.FirstOrDefault(p => !string.IsNullOrEmpty(p.Text));
            if (string.IsNullOrEmpty(textPart?.Text))
            {
                throw new InvalidOperationException($"Google Gemini API response does not contain text content. Full response: {responseContent}");
            }

            var responseJson = textPart.Text;

            try
            {
                if (typeof(TResponse) == typeof(string))
                {
                    var result = (TResponse)(object)responseJson;
                    
                    var usage = new Usage
                    {
                        Input = geminiResponse.UsageMetadata?.PromptTokenCount ?? 0,
                        Output = geminiResponse.UsageMetadata?.CandidatesTokenCount ?? 0
                    };

                    return new Result<TResponse>()
                    {
                        Value = result,
                        MetaData = new(llm, usage, stopwatch.Elapsed)
                    };
                }
                else
                {
                    var parsedResponse = JsonSerializer.Deserialize<JsonElement>(responseJson, DeserializationOptions);

                    var jsonToDeserialize = parsedResponse.TryGetProperty("result", out var resultElement)
                        ? resultElement.GetRawText()
                        : responseJson;

                    var optionsJson = new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true,
                        Encoder = JavaScriptEncoder.Create(UnicodeRanges.All),
                        Converters = { new EnumJsonConverter() }
                    };

                    var result = JsonSerializer.Deserialize<TResponse>(jsonToDeserialize, optionsJson)
                        ?? throw new JsonException("Deserialization returned null.");

                    var usage = new Usage
                    {
                        Input = geminiResponse.UsageMetadata?.PromptTokenCount ?? 0,
                        Output = geminiResponse.UsageMetadata?.CandidatesTokenCount ?? 0
                    };

                    return new Result<TResponse>()
                    {
                        Value = result,
                        MetaData = new(llm, usage, stopwatch.Elapsed)
                    };
                }
            }
            catch (Exception ex) when (ex is JsonException || ex is InvalidOperationException)
            {
                throw new JsonException($"Failed to parse JSON: {responseJson}", ex);
            }
        }
        catch (HttpRequestException ex)
        {
            stopwatch.Stop();
            throw new InvalidOperationException($"HTTP request to Google Gemini API failed: {ex.Message}", ex);
        }
        catch (OperationCanceledException ex) when (cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException(
                $"Google Gemini request was cancelled. Model: {llm.Name}. " +
                $"Consider using a simpler model or reducing the complexity of the request.", ex);
        }
        catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException)
        {
            throw new TimeoutException(
                $"Google Gemini request timed out. Model: {llm.Name}. " +
                $"Consider using a simpler model or reducing the complexity of the request.", ex);
        }
    }

    private object BuildRequestBody<TResponse>(ITextLlmBase llm, IPromptBase<TResponse> prompt)
    {
        var systemPrompt = PromptService.BuildPrompt(prompt);
        
        var parts = new List<object>();
        
        var jsonSchema = JsonSchemaGenerator.GenerateJsonSchema<TResponse>();
        var instructionText = $"You must respond with valid JSON matching this schema: {jsonSchema}. Do not include any text before or after the JSON.\n\n{systemPrompt}";
        
        parts.Add(new
        {
            text = instructionText
        });

        if (prompt.Files != null && prompt.Files.Any())
        {
            foreach (var file in prompt.Files)
            {
                if (IsImageMimeType(file.MimeType))
                {
                    var base64Data = Convert.ToBase64String(file.Data);
                    
                    parts.Add(new
                    {
                        inlineData = new
                        {
                            mimeType = file.MimeType,
                            data = base64Data
                        }
                    });
                }
                else
                {
                    var fileContent = Encoding.UTF8.GetString(file.Data);
                    parts.Add(new
                    {
                        text = $"File: {file.Name}\n\n{fileContent}"
                    });
                }
            }
        }

        var contents = new[]
        {
            new
            {
                parts = parts
            }
        };

        var requestBody = new Dictionary<string, object>
        {
            ["contents"] = contents
        };

        var generationConfig = new Dictionary<string, object>();

        if (llm.MaxTokens > 0)
        {
            generationConfig["maxOutputTokens"] = llm.MaxTokens;
        }

        if (llm is GoogleBase googleLlm)
        {
            if (HasProperty(googleLlm, "Temperature"))
            {
                var temperature = GetPropertyValue<decimal>(googleLlm, "Temperature");
                generationConfig["temperature"] = (float)temperature;
            }

            if (HasProperty(googleLlm, "TopP"))
            {
                var topP = GetPropertyValue<decimal>(googleLlm, "TopP");
                generationConfig["topP"] = (float)topP;
            }

            if (HasProperty(googleLlm, "TopK"))
            {
                var topK = GetPropertyValue<int>(googleLlm, "TopK");
                generationConfig["topK"] = topK;
            }
        }

        generationConfig["responseMimeType"] = "application/json";

        if (generationConfig.Any())
        {
            requestBody["generationConfig"] = generationConfig;
        }

        return requestBody;
    }

    private static bool IsImageMimeType(string mimeType)
    {
        if (string.IsNullOrEmpty(mimeType))
            return false;

        var normalizedMimeType = mimeType.ToLowerInvariant();
        
        return normalizedMimeType switch
        {
            "image/jpeg" or "image/jpg" or "image/png" or "image/gif" or 
            "image/webp" => true,
            _ => false
        };
    }

    private static bool HasProperty(object obj, string propertyName)
    {
        return obj.GetType().GetProperty(propertyName) != null;
    }

    private static T GetPropertyValue<T>(object obj, string propertyName)
    {
        var property = obj.GetType().GetProperty(propertyName);
        return property != null ? (T)property.GetValue(obj)! : default(T)!;
    }

    private class GeminiApiResponse
    {
        [JsonPropertyName("candidates")]
        public List<GeminiCandidate>? Candidates { get; set; }
        
        [JsonPropertyName("usageMetadata")]
        public GeminiUsageMetadata? UsageMetadata { get; set; }
        
        [JsonPropertyName("modelVersion")]
        public string? ModelVersion { get; set; }
    }

    private class GeminiCandidate
    {
        [JsonPropertyName("content")]
        public GeminiContent? Content { get; set; }
        
        [JsonPropertyName("finishReason")]
        public string? FinishReason { get; set; }
        
        [JsonPropertyName("avgLogprobs")]
        public double? AvgLogprobs { get; set; }
    }

    private class GeminiContent
    {
        [JsonPropertyName("parts")]
        public List<GeminiPart>? Parts { get; set; }
        
        [JsonPropertyName("role")]
        public string? Role { get; set; }
    }

    private class GeminiPart
    {
        [JsonPropertyName("text")]
        public string? Text { get; set; }
    }

    private class GeminiUsageMetadata
    {
        [JsonPropertyName("promptTokenCount")]
        public int PromptTokenCount { get; set; }
        
        [JsonPropertyName("candidatesTokenCount")]
        public int CandidatesTokenCount { get; set; }
        
        [JsonPropertyName("totalTokenCount")]
        public int TotalTokenCount { get; set; }
    }
}
