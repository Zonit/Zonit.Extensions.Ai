using Microsoft.Extensions.Options;
using OpenAI.Responses;
using System.Diagnostics;
using System.Text.Json;
using Zonit.Extensions.Ai.Application.Options;
using Zonit.Extensions.Ai.Application.Services;
using Zonit.Extensions.Ai.Domain.Repositories;
using Zonit.Extensions.Ai.Infrastructure.Serialization;
using Zonit.Extensions.Ai.Llm;

namespace Zonit.Extensions.Ai.Infrastructure.Repositories.OpenAi;

internal partial class OpenAiRepository(IOptions<AiOptions> options) : ITextRepository
{
#pragma warning disable OPENAI001 // Typ jest przeznaczony wyłącznie do celów ewaluacyjnych i może zostać zmieniony albo usunięty w przyszłych aktualizacjach. Wstrzymaj tę diagnostykę, aby kontynuować.

    public async Task<Result<TResponse>> ResponseAsync<TResponse>(ITextLlmBase llm, IPromptBase<TResponse> prompt, CancellationToken cancellationToken = default)
    {
        var client = new OpenAIResponseClient(model: llm.Name, apiKey: options.Value.OpenAiKey);

       var messages = new List<ResponseItem>
        {
            ResponseItem.CreateSystemMessageItem(PromptService.BuildPrompt(prompt))
        };

        var responseOptions = new ResponseCreationOptions()
        {
            MaxOutputTokenCount = llm.MaxTokens,
            EndUserId = prompt.UserName,
            TextOptions = new()
            {
                TextFormat = ResponseTextFormat.CreateJsonSchemaFormat(
                    jsonSchemaFormatName: "response",
                    jsonSchema: BinaryData.FromString(JsonSchemaGenerator.GenerateJsonSchema<TResponse>()),
                    jsonSchemaFormatDescription: JsonSchemaGenerator.GetSchemaDescription<TResponse>(),
                    jsonSchemaIsStrict: true)
            },
        };

        // Domyślne ustawienia
        if (llm is OpenAiBase variable)
        {
            responseOptions.StoredOutputEnabled = variable.StoreLogs;
        }

        // Model myślący
        if (llm is OpenAiReasoningBase variableReasoning)
        {
            // Inicjalizuj ReasoningOptions jeśli jeszcze nie istnieje
            responseOptions.ReasoningOptions ??= new ResponseReasoningOptions();
            responseOptions.ReasoningOptions.ReasoningEffortLevel = variableReasoning.Reason switch
            {
                OpenAiReasoningBase.ReasonType.Low => ResponseReasoningEffortLevel.Low,
                OpenAiReasoningBase.ReasonType.Medium => ResponseReasoningEffortLevel.Medium,
                OpenAiReasoningBase.ReasonType.High => ResponseReasoningEffortLevel.High,
                null => ResponseReasoningEffortLevel.Medium,
                _ => ResponseReasoningEffortLevel.Medium
            };
            responseOptions.ReasoningOptions.ReasoningSummaryVerbosity = variableReasoning.ReasonSummary switch
            {
                OpenAiReasoningBase.ReasonSummaryType.None => ResponseReasoningSummaryVerbosity.Concise,
                OpenAiReasoningBase.ReasonSummaryType.Auto => new ResponseReasoningSummaryVerbosity("auto"),
                OpenAiReasoningBase.ReasonSummaryType.Detailed => ResponseReasoningSummaryVerbosity.Detailed,
                null => new ResponseReasoningSummaryVerbosity("auto"),
                _ => new ResponseReasoningSummaryVerbosity("auto")
            };
        }

        // Model chat typu GPT
        if (llm is OpenAiChatBase variableChat)
        {
            responseOptions.Temperature = (float)variableChat.Temperature;
            responseOptions.TopP = (float)variableChat.TopP;
        }

        // Tools
        if (prompt.Tools is not null)
        {
            if (prompt.ToolChoice is not null)
            {
                responseOptions.ToolChoice = prompt.ToolChoice.Value switch
                {
                    ToolsType.None => ResponseToolChoice.CreateNoneChoice(),
                    ToolsType.WebSearch => ResponseToolChoice.CreateWebSearchChoice(),
                    ToolsType.FileSearch => ResponseToolChoice.CreateFileSearchChoice(),
                    //ToolsType.CodeInterpreter => ResponseToolChoice.CreateComputerChoice(),
                    _ => ResponseToolChoice.CreateAutoChoice()
                };
            }

            foreach (var tool in prompt.Tools)
            {
                if (tool is WebSearchTool webSearch)
                {
                    responseOptions.Tools.Add(ResponseTool.CreateWebSearchTool(
                        // TODO: Dodaj region
                        searchContextSize: webSearch.ContextSize switch
                        {
                            WebSearchTool.ContextSizeType.Low => WebSearchContextSize.Low,
                            WebSearchTool.ContextSizeType.Medium => WebSearchContextSize.Medium,
                            WebSearchTool.ContextSizeType.High => WebSearchContextSize.High,
                            _ => WebSearchContextSize.Medium
                        }
                    ));
                }
            }
        }

        var stopwatch = new Stopwatch();
        stopwatch.Start();
        OpenAIResponse response = await client.CreateResponseAsync(inputItems: messages, responseOptions);
        stopwatch.Stop();

        // Znajdź pierwszą wiadomość z odpowiedzią (pomiń web search items)
        MessageResponseItem? message = response.OutputItems
            .OfType<MessageResponseItem>()
            .FirstOrDefault();

        if (message == null)
        {
            throw new InvalidOperationException("Brak wiadomości z odpowiedzią w odpowiedzi OpenAI.");
        }

        var responseJson = message.Content?.FirstOrDefault()?.Text;

        if (string.IsNullOrEmpty(responseJson))
        {
            throw new InvalidOperationException("Odpowiedź OpenAI nie zawiera treści tekstowej.");
        }

        try
        {
            var parsedResponse = JsonSerializer.Deserialize<JsonElement>(responseJson);

            // Pobierz "result" jeśli istnieje, inaczej użyj całego JSON-a
            var jsonToDeserialize = parsedResponse.TryGetProperty("result", out var resultElement)
                ? resultElement.GetRawText()
                : responseJson;

            var optionsJson = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                Converters = { new NullableEnumJsonConverter() }
            };

            var result = JsonSerializer.Deserialize<TResponse>(jsonToDeserialize, optionsJson)
                ?? throw new JsonException("Deserialization returned null.");

            // Utwórz obiekt Usage na podstawie danych z response
            var usage = new Usage
            {
                Input = response.Usage?.InputTokenCount ?? 0,
                Output = response.Usage?.OutputTokenCount ?? 0
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

#pragma warning restore OPENAI001 // Typ jest przeznaczony wyłącznie do celów ewaluacyjnych i może zostać zmieniony albo usunięty w przyszłych aktualizacjach. Wstrzymaj tę diagnostykę, aby kontynuować.

}