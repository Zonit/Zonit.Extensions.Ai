using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using Zonit.Extensions.Ai.Application.Options;
using Zonit.Extensions.Ai.Application.Services;
using Zonit.Extensions.Ai.Domain.Repositories;
using Zonit.Extensions.Ai.Infrastructure.Serialization;
using Zonit.Extensions.Ai.Llm;
using Zonit.Extensions.Ai.Llm.X;

namespace Zonit.Extensions.Ai.Infrastructure.Repositories.X;

internal class XRepository(IOptions<AiOptions> options, HttpClient httpClient) : ITextRepository
{
    private const string BaseUrl = "https://api.x.ai/v1/chat/completions";

    public async Task<Result<TResponse>> ResponseAsync<TResponse>(ITextLlmBase llm, IPromptBase<TResponse> prompt, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(options.Value.XKey))
        {
            throw new InvalidOperationException("X API key is not configured.");
        }

        var requestBody = BuildRequestBody(llm, prompt);
        var requestContent = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");

        // Configure HTTP client
        httpClient.DefaultRequestHeaders.Clear();
        httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {options.Value.XKey}");

        var stopwatch = new Stopwatch();
        stopwatch.Start();

        try
        {
            var response = await httpClient.PostAsync(BaseUrl, requestContent, cancellationToken);
            stopwatch.Stop();

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                throw new InvalidOperationException($"X API request failed with status {response.StatusCode}: {errorContent}");
            }

            var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
            
            var jsonOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };
            
            var xResponse = JsonSerializer.Deserialize<XApiResponse>(responseContent, jsonOptions);

            if (xResponse?.Choices == null || xResponse.Choices.Length == 0 || 
                xResponse.Choices[0]?.Message?.Content == null)
            {
                throw new InvalidOperationException($"X API response does not contain valid content. Response: {responseContent}");
            }

            var responseJson = xResponse.Choices[0].Message?.Content;

            // Note: Reasoning content is available in xResponse.Choices[0].Message?.ReasoningContent
            // but is not currently exposed in the Result. Future enhancement could add this to MetaData.

            if (string.IsNullOrEmpty(responseJson))
            {
                throw new InvalidOperationException("X API response content is null or empty.");
            }

            try
            {
                var parsedResponse = JsonSerializer.Deserialize<JsonElement>(responseJson);

                // Get "result" if exists, otherwise use the whole JSON
                var jsonToDeserialize = parsedResponse.TryGetProperty("result", out var resultElement)
                    ? resultElement.GetRawText()
                    : responseJson;

                var optionsJson = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    Converters = { new EnumJsonConverter() }
                };

                var result = JsonSerializer.Deserialize<TResponse>(jsonToDeserialize, optionsJson)
                    ?? throw new JsonException("Deserialization returned null.");

                // Create Usage object based on response data
                var usage = new Usage
                {
                    Input = xResponse.Usage?.PromptTokens ?? 0,
                    Output = xResponse.Usage?.CompletionTokens ?? 0
                };

                return new Result<TResponse>()
                {
                    Value = result,
                    MetaData = new(llm, usage, stopwatch.Elapsed)
                };
            }
            catch (Exception ex) when (ex is JsonException || ex is InvalidOperationException)
            {
                throw new JsonException($"Failed to parse JSON: {responseJson}", ex);
            }
        }
        catch (HttpRequestException ex)
        {
            stopwatch.Stop();
            throw new InvalidOperationException($"HTTP request to X API failed: {ex.Message}", ex);
        }
    }

    private object BuildRequestBody<TResponse>(ITextLlmBase llm, IPromptBase<TResponse> prompt)
    {
        var messages = new[]
        {
            new
            {
                role = "system",
                content = PromptService.BuildPrompt(prompt)
            }
        };

        var requestBody = new Dictionary<string, object>
        {
            ["messages"] = messages,
            ["model"] = llm.Name,
            ["response_format"] = new
            {
                type = "json_schema",
                json_schema = new
                {
                    name = "response",
                    schema = JsonSerializer.Deserialize<object>(JsonSchemaGenerator.GenerateJsonSchema<TResponse>()),
                    description = JsonSchemaGenerator.GetSchemaDescription<TResponse>(),
                    strict = true
                }
            }
        };

        // Configure X chat-specific parameters
        if (llm is XChatBase xChatLlm)
        {
            // Add search parameters if WebSearch is configured
            if (xChatLlm.WebSearch.Mode != ModeType.Never)
            {
                var searchParams = BuildSearchParameters(xChatLlm.WebSearch);
                requestBody["search_parameters"] = searchParams;
            }

            // Add temperature if available (check if XChatBase has temperature property)
            if (HasProperty(xChatLlm, "Temperature"))
            {
                var temperature = GetPropertyValue<decimal>(xChatLlm, "Temperature");
                requestBody["temperature"] = (float)temperature;
            }

            // Add top_p if available
            if (HasProperty(xChatLlm, "TopP"))
            {
                var topP = GetPropertyValue<decimal>(xChatLlm, "TopP");
                requestBody["top_p"] = (float)topP;
            }

            // Add reasoning effort for reasoning models
            if (llm is XReasoningBase xReasoningLlm && xReasoningLlm.Reason.HasValue)
            {
                requestBody["reasoning_effort"] = xReasoningLlm.Reason.Value switch
                {
                    XReasoningBase.ReasonType.Low => "low",
                    XReasoningBase.ReasonType.High => "high",
                    _ => "low"
                };
            }

            // Add max_tokens
            requestBody["max_tokens"] = llm.MaxTokens;
        }

        return requestBody;
    }

    private object BuildSearchParameters(Search webSearch)
    {
        var searchParams = new Dictionary<string, object>();

        // Set search mode
        searchParams["mode"] = webSearch.Mode switch
        {
            ModeType.Always => "on",
            ModeType.Auto => "auto", 
            ModeType.Never => "off",
            _ => "auto"
        };

        // Set return citations
        searchParams["return_citations"] = webSearch.Citations;

        // Set date range
        if (webSearch.FromDate.HasValue)
        {
            searchParams["from_date"] = webSearch.FromDate.Value.ToString("yyyy-MM-dd");
        }

        if (webSearch.ToDate.HasValue)
        {
            searchParams["to_date"] = webSearch.ToDate.Value.ToString("yyyy-MM-dd");
        }

        // Set max search results
        if (webSearch.MaxResults != 20) // Only set if different from default
        {
            searchParams["max_search_results"] = webSearch.MaxResults;
        }

        // Set sources with detailed configurations
        if (webSearch.Sources?.Length > 0)
        {
            var sources = new List<object>();
            
            foreach (var searchSource in webSearch.Sources)
            {
                var source = BuildSourceConfiguration(searchSource, webSearch);
                sources.Add(source);
            }

            searchParams["sources"] = sources;
        }

        return searchParams;
    }

    private Dictionary<string, object> BuildSourceConfiguration(ISearchSource searchSource, Search webSearch)
    {
        var source = new Dictionary<string, object>
        {
            ["type"] = GetSourceTypeDescription(searchSource.Type)
        };

        switch (searchSource)
        {
            case WebSearchSource webSource:
                BuildWebSourceParameters(source, webSource, webSearch);
                break;
                
            case XSearchSource xSource:
                BuildXSourceParameters(source, xSource);
                break;
                
            case NewsSearchSource newsSource:
                BuildNewsSourceParameters(source, newsSource, webSearch);
                break;
                
            case RssSearchSource rssSource:
                BuildRssSourceParameters(source, rssSource);
                break;
        }

        return source;
    }

    private void BuildWebSourceParameters(Dictionary<string, object> source, WebSearchSource webSource, Search webSearch)
    {
        // Country parameter
        if (!string.IsNullOrEmpty(webSource.Country))
        {
            source["country"] = webSource.Country;
        }
        else if (!string.IsNullOrEmpty(webSearch.Region))
        {
            source["country"] = webSearch.Region;
        }

        // Excluded websites (max 5)
        if (webSource.ExcludedWebsites?.Length > 0)
        {
            if (webSource.AllowedWebsites?.Length > 0)
            {
                throw new InvalidOperationException("Cannot use both ExcludedWebsites and AllowedWebsites for the same web source.");
            }
            source["excluded_websites"] = webSource.ExcludedWebsites.Take(5).ToArray();
        }

        // Allowed websites (max 5)
        if (webSource.AllowedWebsites?.Length > 0)
        {
            source["allowed_websites"] = webSource.AllowedWebsites.Take(5).ToArray();
        }

        // Safe search
        if (!webSource.SafeSearch)
        {
            source["safe_search"] = false;
        }
    }

    private void BuildXSourceParameters(Dictionary<string, object> source, XSearchSource xSource)
    {
        // Included X handles (max 10)
        if (xSource.IncludedXHandles?.Length > 0)
        {
            if (xSource.ExcludedXHandles?.Length > 0)
            {
                throw new InvalidOperationException("Cannot use both IncludedXHandles and ExcludedXHandles for the same X source.");
            }
            source["included_x_handles"] = xSource.IncludedXHandles.Take(10).ToArray();
        }

        // Excluded X handles (max 10)
        if (xSource.ExcludedXHandles?.Length > 0)
        {
            source["excluded_x_handles"] = xSource.ExcludedXHandles.Take(10).ToArray();
        }

        // Post favorite count filter
        if (xSource.PostFavoriteCount.HasValue)
        {
            source["post_favorite_count"] = xSource.PostFavoriteCount.Value;
        }

        // Post view count filter
        if (xSource.PostViewCount.HasValue)
        {
            source["post_view_count"] = xSource.PostViewCount.Value;
        }
    }

    private void BuildNewsSourceParameters(Dictionary<string, object> source, NewsSearchSource newsSource, Search webSearch)
    {
        // Country parameter
        if (!string.IsNullOrEmpty(newsSource.Country))
        {
            source["country"] = newsSource.Country;
        }
        else if (!string.IsNullOrEmpty(webSearch.Region))
        {
            source["country"] = webSearch.Region;
        }

        // Excluded websites (max 5)
        if (newsSource.ExcludedWebsites?.Length > 0)
        {
            source["excluded_websites"] = newsSource.ExcludedWebsites.Take(5).ToArray();
        }

        // Safe search
        if (!newsSource.SafeSearch)
        {
            source["safe_search"] = false;
        }
    }

    private void BuildRssSourceParameters(Dictionary<string, object> source, RssSearchSource rssSource)
    {
        // RSS links (currently supports 1 link)
        if (rssSource.Links?.Length > 0)
        {
            source["links"] = rssSource.Links.Take(1).ToArray();
        }
    }

    private static string GetSourceTypeDescription(SourceType sourceType)
    {
        var field = typeof(SourceType).GetField(sourceType.ToString());
        var attribute = field?.GetCustomAttributes(typeof(DescriptionAttribute), false)
                              .Cast<DescriptionAttribute>()
                              .FirstOrDefault();
        return attribute?.Description ?? sourceType.ToString().ToLowerInvariant();
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

    // X API Response Models
    private class XApiResponse
    {
        [JsonPropertyName("choices")]
        public XChoice[]? Choices { get; set; }
        
        [JsonPropertyName("usage")]
        public XUsage? Usage { get; set; }
        
        [JsonPropertyName("citations")]
        public string[]? Citations { get; set; }
    }

    private class XChoice
    {
        [JsonPropertyName("message")]
        public XMessage? Message { get; set; }
    }

    private class XMessage
    {
        [JsonPropertyName("content")]
        public string? Content { get; set; }
        
        [JsonPropertyName("reasoning_content")]
        public string? ReasoningContent { get; set; }
    }

    private class XUsage
    {
        [JsonPropertyName("prompt_tokens")]
        public int PromptTokens { get; set; }
        
        [JsonPropertyName("completion_tokens")]
        public int CompletionTokens { get; set; }
        
        [JsonPropertyName("total_tokens")]
        public int TotalTokens { get; set; }
        
        [JsonPropertyName("completion_tokens_details")]
        public XCompletionTokensDetails? CompletionTokensDetails { get; set; }
    }
    
    private class XCompletionTokensDetails
    {
        [JsonPropertyName("reasoning_tokens")]
        public int ReasoningTokens { get; set; }
    }
}
