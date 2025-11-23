using Microsoft.Extensions.Options;
using System.Diagnostics;
using System.Text.Json;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Unicode;
using System.Text.Json.Serialization.Metadata;
using Zonit.Extensions.Ai.Application.Options;
using Zonit.Extensions.Ai.Domain.Repositories;
using Zonit.Extensions.Ai.Infrastructure.Serialization;
using Zonit.Extensions.Ai.Llm;
using Zonit.Extensions.Ai.Domain.Models;

namespace Zonit.Extensions.Ai.Infrastructure.Repositories.OpenAi;

internal partial class OpenAiRepository(IOptions<AiOptions> options, HttpClient httpClient) : ITextRepository
{
    private readonly string _apiKey = options.Value.OpenAiKey ?? throw new ArgumentException("OpenAI API key is required");
    private const string OpenAiApiUrl = "/v1/responses";

    // JSON serializer options with proper UTF-8 support and AOT compatibility
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();
    private static readonly JsonSerializerOptions DeserializationOptions = CreateDeserializationOptions();

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            WriteIndented = false,
            Encoder = JavaScriptEncoder.Create(UnicodeRanges.All)
        };

#if NET8_0_OR_GREATER
        // Use source-generated JSON serializer for AOT compatibility
        options.TypeInfoResolver = JsonTypeInfoResolver.Combine(
            AiJsonSerializerContext.Default,
            new DefaultJsonTypeInfoResolver()
        );
#endif

        return options;
    }

    private static JsonSerializerOptions CreateDeserializationOptions()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            PropertyNameCaseInsensitive = true,
            Encoder = JavaScriptEncoder.Create(UnicodeRanges.All)
        };

#if NET8_0_OR_GREATER
        // Use source-generated JSON serializer for AOT compatibility
        options.TypeInfoResolver = JsonTypeInfoResolver.Combine(
            AiJsonSerializerContext.Default,
            new DefaultJsonTypeInfoResolver()
        );
#endif

        return options;
    }

    public async Task<Result<TResponse>> ResponseAsync<TResponse>(ITextLlmBase llm, IPromptBase<TResponse> prompt, CancellationToken cancellationToken = default)
    {
        // Validate that the model supports the Responses API endpoint
        if (!llm.SupportedEndpoints.HasFlag(EndpointsType.Response))
        {
            throw new ArgumentException($"Model '{llm.Name}' does not support the Responses API endpoint. " +
                                      $"Supported endpoints: {llm.SupportedEndpoints}. " +
                                      $"Please use a model that supports EndpointsType.Response.");
        }

        // Build the request payload
        var requestPayload = BuildRequestPayload(llm, prompt);
        var jsonPayload = JsonSerializer.Serialize(requestPayload, JsonOptions);

        var stopwatch = new Stopwatch();
        stopwatch.Start();
        
        try
        {
            var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

            // Send HTTP request - let Polly handle the timeout
            using var response = await httpClient.PostAsync(OpenAiApiUrl, content, cancellationToken);
            stopwatch.Stop();

            // Read response with explicit UTF-8 encoding
            var responseBytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
            var responseJson = Encoding.UTF8.GetString(responseBytes);

            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException($"OpenAI API request failed with status {response.StatusCode}: {responseJson}");
            }

            // Parse OpenAI response with proper UTF-8 support
            var openAiResponse = JsonSerializer.Deserialize<OpenAiResponsesApiResponse>(responseJson, DeserializationOptions);

            if (openAiResponse == null)
            {
                throw new InvalidOperationException("Failed to deserialize OpenAI Responses API response");
            }

            // Check for API errors
            if (openAiResponse.Status != "completed")
            {
                var errorMessage = openAiResponse.Error != null 
                    ? JsonSerializer.Serialize(openAiResponse.Error, JsonOptions)
                    : $"Response status: {openAiResponse.Status}";
                throw new InvalidOperationException($"OpenAI Responses API returned non-completed status: {errorMessage}");
            }

            if (openAiResponse.Output == null || !openAiResponse.Output.Any())
            {
                throw new InvalidOperationException("No output returned from OpenAI Responses API");
            }

            // Find the first message output
            var messageOutput = openAiResponse.Output.FirstOrDefault(o => o.Type == "message");
            if (messageOutput?.Content == null || !messageOutput.Content.Any())
            {
                throw new InvalidOperationException("No message content returned from OpenAI Responses API");
            }

            // Find the first text content
            var textContent = messageOutput.Content.FirstOrDefault(c => c.Type == "output_text");
            if (string.IsNullOrEmpty(textContent?.Text))
            {
                throw new InvalidOperationException("OpenAI response does not contain text content");
            }

            var messageContent = textContent.Text;

            try
            {
                var parsedResponse = JsonSerializer.Deserialize<JsonElement>(messageContent, DeserializationOptions);

                // Get "result" if it exists, otherwise use the entire JSON
                var jsonToDeserialize = parsedResponse.TryGetProperty("result", out var resultElement)
                    ? resultElement.GetRawText()
                    : messageContent;

                var optionsJson = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    Encoder = JavaScriptEncoder.Create(UnicodeRanges.All),
                    Converters = { new EnumJsonConverter() }
                };

                var result = JsonSerializer.Deserialize<TResponse>(jsonToDeserialize, optionsJson)
                    ?? throw new JsonException("Deserialization returned null.");

                // Create Usage object based on response data
                var usage = new Usage
                {
                    Input = openAiResponse.Usage?.InputTokens ?? 0,
                    Output = openAiResponse.Usage?.OutputTokens ?? 0,
                    InputDetails = openAiResponse.Usage?.InputTokensDetails != null ? new Usage.Details
                    {
                        Text = openAiResponse.Usage.InputTokensDetails.CachedTokens,
                        // Note: OpenAI Responses API currently only provides cached_tokens in input_tokens_details
                        // Other details like text tokens, image tokens are not separately provided
                    } : null,
                    OutputDetails = openAiResponse.Usage?.OutputTokensDetails != null ? new Usage.Details
                    {
                        Text = openAiResponse.Usage.OutputTokensDetails.ReasoningTokens,
                        // Note: reasoning_tokens in output_tokens_details represents reasoning tokens used
                        // Total output tokens include both reasoning and visible output tokens
                    } : null
                };

                return new Result<TResponse>()
                {
                    Value = result,
                    MetaData = new(llm, usage, stopwatch.Elapsed)
                };
            }
            catch (Exception ex) when (ex is JsonException || ex is InvalidOperationException)
            {
                throw new JsonException($"Failed to parse JSON: {messageContent}", ex);
            }
        }
        catch (OperationCanceledException ex) when (cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException(
                $"OpenAI request was cancelled. Model: {llm.Name}. " +
                $"Consider using a simpler model or reducing the complexity of the request.", ex);
        }
        catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException)
        {
            throw new TimeoutException(
                $"OpenAI request timed out. Model: {llm.Name}. " +
                $"Consider using a simpler model or reducing the complexity of the request.", ex);
        }
    }

    private object BuildRequestPayload<TResponse>(ITextLlmBase llm, IPromptBase<TResponse> prompt)
    {
        var requestPayload = new Dictionary<string, object>
        {
            ["model"] = llm.Name,
            ["max_output_tokens"] = llm.MaxTokens
        };

        // Build input as message format for Responses API
        var content = new List<object>();
        
        // Add text prompt
        content.Add(new
        {
            type = "input_text",
            text = PromptService.BuildPrompt(prompt)
        });

        // Add files if provided
        if (prompt.Files != null && prompt.Files.Any())
        {
            foreach (var file in prompt.Files)
            {
                // Check if the file is an image or document based on MIME type
                if (IsImageMimeType(file.MimeType))
                {
                    // Handle images using input_image type
                    var base64Data = Convert.ToBase64String(file.Data);
                    var dataUri = $"data:{file.MimeType};base64,{base64Data}";
                    
                    content.Add(new
                    {
                        type = "input_image",
                        image_url = dataUri  // Direct string, not an object
                    });
                }
                else
                {
                    // Handle documents (PDFs, text files, etc.) using input_file type
                    var base64Data = Convert.ToBase64String(file.Data);
                    
                    content.Add(new
                    {
                        type = "input_file",
                        file_data = $"data:{file.MimeType};base64,{base64Data}",
                        filename = file.Name
                    });
                }
            }
        }

        // Create message format input
        requestPayload["input"] = new[]
        {
            new
            {
                type = "message",
                role = "user",
                content = content
            }
        };

        // Add user ID if provided
        if (!string.IsNullOrEmpty(prompt.UserName))
        {
            requestPayload["user"] = prompt.UserName;
        }

        // Add JSON schema for structured output - use explicit property names to avoid snake_case conversion issues
        var jsonSchema = JsonSchemaGenerator.GenerateJsonSchema<TResponse>();
        var schemaObject = JsonSerializer.Deserialize<object>(jsonSchema);
        
        requestPayload["text"] = new Dictionary<string, object>
        {
            ["format"] = new Dictionary<string, object>
            {
                ["type"] = "json_schema",
                ["name"] = "response",
                ["description"] = JsonSchemaGenerator.GetSchemaDescription<TResponse>(),
                ["schema"] = schemaObject,
                ["strict"] = true
            }
        };

        // Handle different LLM types
        if (llm is OpenAiBase openAiBase)
        {
            if (openAiBase.StoreLogs)
            {
                requestPayload["store"] = true;
            }
        }

        // Handle reasoning models - use reasoning parameter
        if (llm is OpenAiReasoningBase reasoningModel)
        {
            // Add reasoning effort parameter
            // Note: GPT-5.1 defaults to "none", other reasoning models default to "medium"
            requestPayload["reasoning"] = new Dictionary<string, object>
            {
                ["effort"] = reasoningModel.Reason switch
                {
                    OpenAiReasoningBase.ReasonType.None => "none",
                    //OpenAiReasoningBase.ReasonType.Minimal => "minimal",
                    OpenAiReasoningBase.ReasonType.Low => "low",
                    OpenAiReasoningBase.ReasonType.Medium => "medium",
                    OpenAiReasoningBase.ReasonType.High => "high",
                    null => "medium", // Default for most reasoning models (except GPT-5.1 which defaults to "none")
                    _ => "medium"
                }
            };

            // Add verbosity parameter for GPT-5 models (output verbosity control)
            if (reasoningModel.Verbosity.HasValue)
            {
                if (!requestPayload.ContainsKey("text"))
                {
                    requestPayload["text"] = new Dictionary<string, object>();
                }

                var textConfig = (Dictionary<string, object>)requestPayload["text"];
                textConfig["verbosity"] = reasoningModel.Verbosity.Value switch
                {
                    OpenAiReasoningBase.VerbosityType.Low => "low",
                    OpenAiReasoningBase.VerbosityType.Medium => "medium",
                    OpenAiReasoningBase.VerbosityType.High => "high",
                    _ => "medium"
                };
            }

            // IMPORTANT: GPT-5 models do NOT support temperature, top_p, or logprobs
            // These parameters would cause API errors if sent with reasoning models
            // Temperature and top_p are intentionally NOT added here
        }
        // Handle chat models (GPT-4, GPT-3.5, etc.) - NOT for reasoning models
        else if (llm is OpenAiChatBase chatModel)
        {
            requestPayload["temperature"] = chatModel.Temperature;
            requestPayload["top_p"] = chatModel.TopP;
        }

        // Handle tools - simplified format for Responses API
        if (llm.Tools is not null && llm.Tools.Any())
        {
            var tools = new List<object>();
            
            foreach (var tool in llm.Tools)
            {
                if (tool is FileSearchTool fileSearch)
                {
                    // Handle file search tool with vector store support
                    var fileSearchTool = new Dictionary<string, object>
                    {
                        ["type"] = "file_search"
                    };

                    // Add vector store IDs directly on the tool object (required)
                    if (!string.IsNullOrEmpty(fileSearch.VectorId))
                    {
                        fileSearchTool["vector_store_ids"] = new[] { fileSearch.VectorId };
                    }

                    // Add max_num_results if provided
                    if (fileSearch.MaxNumResults.HasValue)
                    {
                        fileSearchTool["max_num_results"] = fileSearch.MaxNumResults.Value;
                    }

                    // Add ranking options if provided
                    if (fileSearch.RankingOptions != null)
                    {
                        var rankingOptions = new Dictionary<string, object>();
                        
                        if (!string.IsNullOrEmpty(fileSearch.RankingOptions.Ranker))
                        {
                            rankingOptions["ranker"] = fileSearch.RankingOptions.Ranker;
                        }

                        if (fileSearch.RankingOptions.ScoreThreshold.HasValue)
                        {
                            rankingOptions["score_threshold"] = fileSearch.RankingOptions.ScoreThreshold.Value;
                        }

                        if (rankingOptions.Any())
                        {
                            fileSearchTool["ranking_options"] = rankingOptions;
                        }
                    }

                    // Add filters if provided
                    if (fileSearch.Filters != null)
                    {
                        fileSearchTool["filters"] = fileSearch.Filters;
                    }

                    tools.Add(fileSearchTool);
                }
            }

            if (tools.Any())
            {
                requestPayload["tools"] = tools;

                // Handle tool choice based on supported tools
                if (llm.SupportedTools.HasFlag(ToolsType.FileSearch))
                {
                    requestPayload["tool_choice"] = "auto";
                }
            }
        }

        // Handle legacy prompt-based tools for backward compatibility
        if (prompt.Tools is not null && prompt.Tools.Any())
        {
            var legacyTools = new List<object>();
            
            foreach (var tool in prompt.Tools)
            {
                if (tool is WebSearchTool webSearch)
                {
                    // Use simplified format for Responses API
                    legacyTools.Add(new
                    {
                        type = "web_search"
                    });
                }
            }

            if (legacyTools.Any())
            {
                // Merge with existing tools or create new array
                if (requestPayload.ContainsKey("tools"))
                {
                    var existingTools = (List<object>)requestPayload["tools"];
                    existingTools.AddRange(legacyTools);
                }
                else
                {
                    requestPayload["tools"] = legacyTools;
                }

                // Handle tool choice
                if (prompt.ToolChoice is not null)
                {
                    requestPayload["tool_choice"] = prompt.ToolChoice.Value switch
                    {
                        ToolsType.None => "none",
                        ToolsType.WebSearch => "auto",
                        ToolsType.FileSearch => "auto",
                        _ => "auto"
                    };
                }
            }
        }

        return requestPayload;
    }

    /// <summary>
    /// Determines if a MIME type represents an image that should be processed as input_image
    /// </summary>
    private static bool IsImageMimeType(string mimeType)
    {
        if (string.IsNullOrEmpty(mimeType))
            return false;

        var normalizedMimeType = mimeType.ToLowerInvariant();
        
        return normalizedMimeType switch
        {
            "image/jpeg" or "image/jpg" or "image/png" or "image/gif" or 
            "image/bmp" or "image/webp" or "image/tiff" or "image/tif" or
            "image/svg+xml" or "image/x-icon" or "image/vnd.microsoft.icon" => true,
            _ => false
        };
    }
}

// DTOs for OpenAI Responses API response
internal class OpenAiResponsesApiResponse
{
    public string? Id { get; set; }
    public string? Object { get; set; }
    public long? CreatedAt { get; set; }
    public string? Status { get; set; }
    public object? Error { get; set; }
    public object? IncompleteDetails { get; set; }
    public string? Instructions { get; set; }
    public int? MaxOutputTokens { get; set; }
    public string? Model { get; set; }
    public List<ResponseOutput>? Output { get; set; }
    public bool? ParallelToolCalls { get; set; }
    public string? PreviousResponseId { get; set; }
    public ResponseReasoning? Reasoning { get; set; }
    public bool? Store { get; set; }
    public double? Temperature { get; set; }
    public ResponseText? Text { get; set; }
    public object? ToolChoice { get; set; }
    public List<object>? Tools { get; set; }
    public double? TopP { get; set; }
    public string? Truncation { get; set; }
    public ResponseUsageInfo? Usage { get; set; }
    public string? User { get; set; }
    public object? Metadata { get; set; }
}

internal class ResponseOutput
{
    public string? Type { get; set; }
    public string? Id { get; set; }
    public string? Status { get; set; }
    public string? Role { get; set; }
    public List<ResponseContent>? Content { get; set; }
}

internal class ResponseContent
{
    public string? Type { get; set; }
    public string? Text { get; set; }
    public List<object>? Annotations { get; set; }
}

internal class ResponseReasoning
{
    public string? Effort { get; set; }
    public string? Summary { get; set; }
}

internal class ResponseText
{
    public ResponseTextFormat? Format { get; set; }
}

internal class ResponseTextFormat
{
    public string? Type { get; set; }
}

internal class ResponseUsageInfo
{
    public int InputTokens { get; set; }
    public ResponseInputTokensDetails? InputTokensDetails { get; set; }
    public int OutputTokens { get; set; }
    public ResponseOutputTokensDetails? OutputTokensDetails { get; set; }
    public int TotalTokens { get; set; }
}

internal class ResponseInputTokensDetails
{
    public int CachedTokens { get; set; }
}

internal class ResponseOutputTokensDetails
{
    public int ReasoningTokens { get; set; }
}

// Legacy DTOs kept for compatibility (can be removed if not used elsewhere)
internal class OpenAiResponse
{
    public List<Choice>? Choices { get; set; }
    public UsageInfo? Usage { get; set; }
}

internal class Choice
{
    public Message? Message { get; set; }
}

internal class Message
{
    public string? Content { get; set; }
}

internal class UsageInfo
{
    public int PromptTokens { get; set; }
    public int CompletionTokens { get; set; }
    public int TotalTokens { get; set; }
    public CompletionTokensDetails? CompletionTokensDetails { get; set; }
}

internal class CompletionTokensDetails
{
    public int ReasoningTokens { get; set; }
}
