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

namespace Zonit.Extensions.Ai.Infrastructure.Repositories.Anthropic;

internal class AnthropicRepository(IOptions<AiOptions> options, HttpClient httpClient) : ITextRepository
{
    private const string BaseUrl = "https://api.anthropic.com/v1/messages";
    private const string AnthropicVersion = "2023-06-01";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        WriteIndented = false,
        Encoder = JavaScriptEncoder.Create(UnicodeRanges.All),
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private static readonly JsonSerializerOptions DeserializationOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true,
        Encoder = JavaScriptEncoder.Create(UnicodeRanges.All)
    };

    public async Task<Result<TResponse>> ResponseAsync<TResponse>(ITextLlmBase llm, IPromptBase<TResponse> prompt, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(options.Value.AnthropicKey))
        {
            throw new InvalidOperationException("Anthropic API key is not configured.");
        }

        var requestBody = BuildRequestBody(llm, prompt);
        var requestContent = new StringContent(JsonSerializer.Serialize(requestBody, JsonOptions), Encoding.UTF8, "application/json");

        httpClient.DefaultRequestHeaders.Clear();
        httpClient.DefaultRequestHeaders.Add("x-api-key", options.Value.AnthropicKey);
        httpClient.DefaultRequestHeaders.Add("anthropic-version", AnthropicVersion);

        var stopwatch = new Stopwatch();
        stopwatch.Start();

        try
        {
            var response = await httpClient.PostAsync(BaseUrl, requestContent, cancellationToken);
            stopwatch.Stop();

            var responseBytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
            var responseContent = Encoding.UTF8.GetString(responseBytes);

            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException($"Anthropic API request failed with status {response.StatusCode}: {responseContent}");
            }

            var anthropicResponse = JsonSerializer.Deserialize<AnthropicApiResponse>(responseContent, DeserializationOptions);

            if (anthropicResponse?.Content == null || !anthropicResponse.Content.Any())
            {
                throw new InvalidOperationException($"Anthropic API response does not contain valid content. Response: {responseContent}");
            }

            var textContent = anthropicResponse.Content.FirstOrDefault(c => c.Type == "text");
            if (string.IsNullOrEmpty(textContent?.Text))
            {
                throw new InvalidOperationException($"Anthropic API response does not contain text content. Full response: {responseContent}");
            }

            var responseJson = textContent.Text;

            try
            {
                // Clean up markdown code blocks if present (Anthropic sometimes wraps JSON in ```json...```)
                responseJson = CleanJsonResponse(responseJson);

                if (typeof(TResponse) == typeof(string))
                {
                    var result = (TResponse)(object)responseJson;
                    
                    var usage = new Usage
                    {
                        Input = anthropicResponse.Usage?.InputTokens ?? 0,
                        Output = anthropicResponse.Usage?.OutputTokens ?? 0,
                        InputDetails = anthropicResponse.Usage?.CacheReadInputTokens != null || anthropicResponse.Usage?.CacheCreationInputTokens != null ? new Usage.Details
                        {
                            Text = anthropicResponse.Usage.CacheReadInputTokens ?? 0
                        } : null
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

                    // Get "result" if it exists, otherwise use the entire JSON (same logic as OpenAI)
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
                        Input = anthropicResponse.Usage?.InputTokens ?? 0,
                        Output = anthropicResponse.Usage?.OutputTokens ?? 0,
                        InputDetails = anthropicResponse.Usage?.CacheReadInputTokens != null || anthropicResponse.Usage?.CacheCreationInputTokens != null ? new Usage.Details
                        {
                            Text = anthropicResponse.Usage.CacheReadInputTokens ?? 0
                        } : null
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
            throw new InvalidOperationException($"HTTP request to Anthropic API failed: {ex.Message}", ex);
        }
        catch (OperationCanceledException ex) when (cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException(
                $"Anthropic request was cancelled. Model: {llm.Name}. " +
                $"Consider using a simpler model or reducing the complexity of the request.", ex);
        }
        catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException)
        {
            throw new TimeoutException(
                $"Anthropic request timed out. Model: {llm.Name}. " +
                $"Consider using a simpler model or reducing the complexity of the request.", ex);
        }
    }

    private object BuildRequestBody<TResponse>(ITextLlmBase llm, IPromptBase<TResponse> prompt)
    {
        var systemPrompt = PromptService.BuildPrompt(prompt);
        
        var content = new List<object>();
        
        content.Add(new
        {
            type = "text",
            text = systemPrompt
        });

        if (prompt.Files != null && prompt.Files.Any())
        {
            foreach (var file in prompt.Files)
            {
                if (IsImageMimeType(file.MimeType))
                {
                    var base64Data = Convert.ToBase64String(file.Data);
                    
                    content.Add(new
                    {
                        type = "image",
                        source = new
                        {
                            type = "base64",
                            media_type = file.MimeType,
                            data = base64Data
                        }
                    });
                }
                else
                {
                    var fileContent = Encoding.UTF8.GetString(file.Data);
                    content.Add(new
                    {
                        type = "text",
                        text = $"File: {file.Name}\n\n{fileContent}"
                    });
                }
            }
        }

        var messages = new[]
        {
            new
            {
                role = "user",
                content = content
            }
        };

        var requestBody = new Dictionary<string, object>
        {
            ["model"] = llm.Name,
            ["max_tokens"] = llm.MaxTokens,
            ["messages"] = messages
        };

        // Add JSON schema for structured output (similar to OpenAI implementation)
        // Anthropic doesn't have native structured output support, so we use prompt engineering
        var jsonSchema = JsonSchemaGenerator.GenerateJsonSchema<TResponse>();
        var schemaDescription = JsonSchemaGenerator.GetSchemaDescription<TResponse>();
        
        requestBody["system"] = new[]
        {
            new
            {
                type = "text",
                text = $@"You are a precise JSON response generator. You MUST respond with valid JSON that strictly adheres to the provided schema.

JSON Schema:
{jsonSchema}

Schema Description: {schemaDescription}

CRITICAL RULES:
1. Output ONLY valid JSON - no explanations, no markdown code blocks, no extra text
2. The JSON must match the schema exactly
3. All required fields must be present
4. Use correct data types for each field
5. Return the JSON wrapped in a 'result' key: {{""result"": {{your_response}}}}
6. Do NOT wrap the response in ```json code blocks

Example structure:
{{
  ""result"": {{
    // Your response matching the schema
  }}
}}"
            }
        };

        if (llm is AnthropicBase anthropicLlm)
        {
            if (anthropicLlm.ThinkingBudget.HasValue)
            {
                requestBody["thinking"] = new
                {
                    type = "enabled",
                    budget_tokens = anthropicLlm.ThinkingBudget.Value
                };
            }

            if (HasProperty(anthropicLlm, "Temperature"))
            {
                var temperature = GetPropertyValue<decimal>(anthropicLlm, "Temperature");
                requestBody["temperature"] = (float)temperature;
            }

            if (HasProperty(anthropicLlm, "TopP"))
            {
                var topP = GetPropertyValue<decimal>(anthropicLlm, "TopP");
                requestBody["top_p"] = (float)topP;
            }

            if (HasProperty(anthropicLlm, "TopK"))
            {
                var topK = GetPropertyValue<int>(anthropicLlm, "TopK");
                requestBody["top_k"] = topK;
            }
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

    /// <summary>
    /// Cleans JSON response by removing markdown code blocks that Anthropic sometimes adds
    /// </summary>
    private static string CleanJsonResponse(string response)
    {
        if (string.IsNullOrWhiteSpace(response))
            return response;

        var trimmed = response.Trim();
        
        // Remove markdown code blocks: ```json...``` or ```...```
        if (trimmed.StartsWith("```"))
        {
            // Find the first newline after opening ```
            var firstNewline = trimmed.IndexOf('\n');
            if (firstNewline > 0)
            {
                trimmed = trimmed.Substring(firstNewline + 1);
            }
            
            // Remove closing ```
            if (trimmed.EndsWith("```"))
            {
                trimmed = trimmed.Substring(0, trimmed.Length - 3);
            }
            
            trimmed = trimmed.Trim();
        }

        return trimmed;
    }

    private class AnthropicApiResponse
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }
        
        [JsonPropertyName("type")]
        public string? Type { get; set; }
        
        [JsonPropertyName("role")]
        public string? Role { get; set; }
        
        [JsonPropertyName("content")]
        public List<AnthropicContent>? Content { get; set; }
        
        [JsonPropertyName("model")]
        public string? Model { get; set; }
        
        [JsonPropertyName("stop_reason")]
        public string? StopReason { get; set; }
        
        [JsonPropertyName("stop_sequence")]
        public string? StopSequence { get; set; }
        
        [JsonPropertyName("usage")]
        public AnthropicUsage? Usage { get; set; }
    }

    private class AnthropicContent
    {
        [JsonPropertyName("type")]
        public string? Type { get; set; }
        
        [JsonPropertyName("text")]
        public string? Text { get; set; }
        
        [JsonPropertyName("thinking")]
        public string? Thinking { get; set; }
    }

    private class AnthropicUsage
    {
        [JsonPropertyName("input_tokens")]
        public int InputTokens { get; set; }
        
        [JsonPropertyName("cache_creation_input_tokens")]
        public int? CacheCreationInputTokens { get; set; }
        
        [JsonPropertyName("cache_read_input_tokens")]
        public int? CacheReadInputTokens { get; set; }
        
        [JsonPropertyName("output_tokens")]
        public int OutputTokens { get; set; }
    }
}
