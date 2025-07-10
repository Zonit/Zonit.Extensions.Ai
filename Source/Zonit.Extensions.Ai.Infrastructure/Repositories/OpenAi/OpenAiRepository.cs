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
    private const string OpenAiApiUrl = "/v1/chat/completions";

    public async Task<Result<TResponse>> ResponseAsync<TResponse>(ITextLlmBase llm, IPromptBase<TResponse> prompt, CancellationToken cancellationToken = default)
    {
        // Create CancellationToken with 10-minute timeout
        using var timeoutTokenSource = new CancellationTokenSource(TimeSpan.FromMinutes(10));
        using var combinedTokenSource = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken, 
            timeoutTokenSource.Token
        );

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

            // Send HTTP request
            using var response = await httpClient.PostAsync(OpenAiApiUrl, content, combinedTokenSource.Token);
            stopwatch.Stop();

            var responseJson = await response.Content.ReadAsStringAsync(combinedTokenSource.Token);

            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException($"OpenAI API request failed with status {response.StatusCode}: {responseJson}");
            }

            // Parse OpenAI response
            var openAiResponse = JsonSerializer.Deserialize<OpenAiResponse>(responseJson, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
                PropertyNameCaseInsensitive = true
            });

            if (openAiResponse?.Choices == null || !openAiResponse.Choices.Any())
            {
                throw new InvalidOperationException("No choices returned from OpenAI API");
            }

            var messageContent = openAiResponse.Choices[0].Message?.Content;
            if (string.IsNullOrEmpty(messageContent))
            {
                throw new InvalidOperationException("OpenAI response does not contain text content");
            }

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
                    Input = openAiResponse.Usage?.PromptTokens ?? 0,
                    Output = openAiResponse.Usage?.CompletionTokens ?? 0
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
        catch (OperationCanceledException ex) when (timeoutTokenSource.Token.IsCancellationRequested)
        {
            throw new TimeoutException(
                $"OpenAI request timed out after 10 minutes. Model: {llm.Name}. " +
                $"Consider using a simpler model or reducing the complexity of the request.", ex);
        }
        catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException)
        {
            throw new TimeoutException(
                $"OpenAI request timed out after 10 minutes. Model: {llm.Name}. " +
                $"Consider using a simpler model or reducing the complexity of the request.", ex);
        }
    }

    private object BuildRequestPayload<TResponse>(ITextLlmBase llm, IPromptBase<TResponse> prompt)
    {
        var messages = new List<object>
        {
            new
            {
                role = "system",
                content = PromptService.BuildPrompt(prompt)
            }
        };

        var requestPayload = new Dictionary<string, object>
        {
            ["model"] = llm.Name,
            ["messages"] = messages,
            ["max_completion_tokens"] = llm.MaxTokens
        };

        // Add user ID if provided
        if (!string.IsNullOrEmpty(prompt.UserName))
        {
            requestPayload["user"] = prompt.UserName;
        }

        // Add JSON schema for structured output
        var jsonSchema = JsonSchemaGenerator.GenerateJsonSchema<TResponse>();
        requestPayload["response_format"] = new
        {
            type = "json_schema",
            json_schema = new
            {
                name = "response",
                schema = JsonSerializer.Deserialize<object>(jsonSchema),
                description = JsonSchemaGenerator.GetSchemaDescription<TResponse>(),
                strict = true
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

        // Handle reasoning models - use reasoning_effort parameter
        if (llm is OpenAiReasoningBase reasoningModel)
        {
            requestPayload["reasoning_effort"] = reasoningModel.Reason switch
            {
                OpenAiReasoningBase.ReasonType.Low => "low",
                OpenAiReasoningBase.ReasonType.Medium => "medium",
                OpenAiReasoningBase.ReasonType.High => "high",
                null => "medium",
                _ => "medium"
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

// DTOs for OpenAI API response
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