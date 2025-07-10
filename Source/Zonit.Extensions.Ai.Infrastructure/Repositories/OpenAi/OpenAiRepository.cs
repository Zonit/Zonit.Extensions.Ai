using Microsoft.Extensions.Options;
using System.Diagnostics;
using System.Text.Json;
using System.Text;
using Zonit.Extensions.Ai.Application.Options;
using Zonit.Extensions.Ai.Domain.Repositories;
using Zonit.Extensions.Ai.Infrastructure.Serialization;
using Zonit.Extensions.Ai.Llm;

namespace Zonit.Extensions.Ai.Infrastructure.Repositories.OpenAi;

internal partial class OpenAiRepository(IOptions<AiOptions> options, HttpClient httpClient) : ITextRepository
{
    private readonly string _apiKey = options.Value.OpenAiKey ?? throw new ArgumentException("OpenAI API key is required");
    private const string OpenAiApiUrl = "/v1/responses";

    public async Task<Result<TResponse>> ResponseAsync<TResponse>(ITextLlmBase llm, IPromptBase<TResponse> prompt, CancellationToken cancellationToken = default)
    {
        // Validate that the model supports the Responses API endpoint
        if (!llm.Endpoints.HasFlag(EndpointsType.Response))
        {
            throw new ArgumentException($"Model '{llm.Name}' does not support the Responses API endpoint. " +
                                      $"Supported endpoints: {llm.Endpoints}. " +
                                      $"Please use a model that supports EndpointsType.Response.");
        }

        // Build the request payload
        var requestPayload = BuildRequestPayload(llm, prompt);
        var jsonPayload = JsonSerializer.Serialize(requestPayload, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            WriteIndented = false
        });

        var stopwatch = new Stopwatch();
        stopwatch.Start();
        
        try
        {
            var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

            // Send HTTP request - let Polly handle the timeout
            using var response = await httpClient.PostAsync(OpenAiApiUrl, content, cancellationToken);
            stopwatch.Stop();

            var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException($"OpenAI API request failed with status {response.StatusCode}: {responseJson}");
            }

            // Parse OpenAI response
            var openAiResponse = JsonSerializer.Deserialize<OpenAiResponsesApiResponse>(responseJson, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
                PropertyNameCaseInsensitive = true
            });

            if (openAiResponse == null)
            {
                throw new InvalidOperationException("Failed to deserialize OpenAI Responses API response");
            }

            // Check for API errors
            if (openAiResponse.Status != "completed")
            {
                var errorMessage = openAiResponse.Error != null 
                    ? JsonSerializer.Serialize(openAiResponse.Error)
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
                var parsedResponse = JsonSerializer.Deserialize<JsonElement>(messageContent);

                // Get "result" if it exists, otherwise use the entire JSON
                var jsonToDeserialize = parsedResponse.TryGetProperty("result", out var resultElement)
                    ? resultElement.GetRawText()
                    : messageContent;

                var optionsJson = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    Converters = { new NullableEnumJsonConverter() }
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
            ["input"] = PromptService.BuildPrompt(prompt),
            ["max_output_tokens"] = llm.MaxTokens
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
            requestPayload["reasoning"] = new
            {
                effort = reasoningModel.Reason switch
                {
                    OpenAiReasoningBase.ReasonType.Low => "low",
                    OpenAiReasoningBase.ReasonType.Medium => "medium",
                    OpenAiReasoningBase.ReasonType.High => "high",
                    null => "medium",
                    _ => "medium"
                }
            };
        }

        // Handle chat models (GPT)
        if (llm is OpenAiChatBase chatModel)
        {
            requestPayload["temperature"] = chatModel.Temperature;
            requestPayload["top_p"] = chatModel.TopP;
        }

        // Handle tools
        if (prompt.Tools is not null && prompt.Tools.Any())
        {
            var tools = new List<object>();
            
            foreach (var tool in prompt.Tools)
            {
                if (tool is WebSearchTool webSearch)
                {
                    tools.Add(new
                    {
                        type = "web_search",
                        web_search = new
                        {
                            search_context_size = webSearch.ContextSize switch
                            {
                                WebSearchTool.ContextSizeType.Low => "low",
                                WebSearchTool.ContextSizeType.Medium => "medium",
                                WebSearchTool.ContextSizeType.High => "high",
                                _ => "medium"
                            }
                        }
                    });
                }
            }

            if (tools.Any())
            {
                requestPayload["tools"] = tools;

                // Handle tool choice
                if (prompt.ToolChoice is not null)
                {
                    requestPayload["tool_choice"] = prompt.ToolChoice.Value switch
                    {
                        ToolsType.None => "none",
                        ToolsType.WebSearch => new { type = "web_search" },
                        ToolsType.FileSearch => new { type = "file_search" },
                        _ => "auto"
                    };
                }
            }
        }

        return requestPayload;
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