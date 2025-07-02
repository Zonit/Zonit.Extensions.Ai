using Microsoft.Extensions.Options;
using OpenAI.Responses;
using System.ComponentModel;
using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Zonit.Extensions.Ai.Abstractions;
using Zonit.Extensions.Ai.Application.Options;
using Zonit.Extensions.Ai.Application.Services;
using Zonit.Extensions.Ai.Domain.Repositories;
using Zonit.Extensions.Ai.Llm;

namespace Zonit.Extensions.Ai.Infrastructure.Repositories.OpenAi;

internal partial class OpenAiRepository(IOptions<AiOptions> options) : IAiRepository
{
    public async Task<Result<TResponse>> ResponseAsync<TResponse>(ILlmBase llm, IPromptBase<TResponse> prompt)
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
                    jsonSchema: BinaryData.FromString(GenerateJsonSchema<TResponse>()),
                    jsonSchemaFormatDescription: GetSchemaDescription<TResponse>(),
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
            responseOptions.ReasoningOptions.ReasoningEffortLevel = variableReasoning.Reason switch
            {
                OpenAiReasoningBase.ReasonType.Low => ResponseReasoningEffortLevel.Low,
                OpenAiReasoningBase.ReasonType.Medium => ResponseReasoningEffortLevel.Medium,
                OpenAiReasoningBase.ReasonType.High => ResponseReasoningEffortLevel.High,
                null => ResponseReasoningEffortLevel.High,
                _ => ResponseReasoningEffortLevel.High
            };

            responseOptions.ReasoningOptions.ReasoningSummaryVerbosity = variableReasoning.ReasonSummary switch
            {
                OpenAiReasoningBase.ReasonSummaryType.None => ResponseReasoningSummaryVerbosity.Concise,
                OpenAiReasoningBase.ReasonSummaryType.Auto => new ResponseReasoningSummaryVerbosity("auto"),
                OpenAiReasoningBase.ReasonSummaryType.Detailed => ResponseReasoningSummaryVerbosity.Detailed,
                null => ResponseReasoningSummaryVerbosity.Detailed, // domyślna wartość
                _ => ResponseReasoningSummaryVerbosity.Detailed
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
                        webSearchToolContextSize: webSearch.ContextSize switch
                        {
                            WebSearchTool.ContextSizeType.Low => WebSearchToolContextSize.Low,
                            WebSearchTool.ContextSizeType.Medium => WebSearchToolContextSize.Medium,
                            WebSearchTool.ContextSizeType.High => WebSearchToolContextSize.High,
                            _ => WebSearchToolContextSize.Medium
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

    // Dodaj tę klasę w tym samym pliku lub w oddzielnym pliku
    private class NullableEnumJsonConverter : JsonConverterFactory
    {
        public override bool CanConvert(Type typeToConvert)
        {
            if (!typeToConvert.IsGenericType)
                return false;

            return typeToConvert.GetGenericTypeDefinition() == typeof(Nullable<>) &&
                   typeToConvert.GetGenericArguments()[0].IsEnum;
        }

        public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options)
        {
            Type enumType = typeToConvert.GetGenericArguments()[0];
            Type converterType = typeof(NullableEnumConverter<>).MakeGenericType(enumType);
            return (JsonConverter)Activator.CreateInstance(converterType)!;
        }

        private class NullableEnumConverter<TEnum> : JsonConverter<TEnum?> where TEnum : struct, Enum
        {
            public override TEnum? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            {
                if (reader.TokenType == JsonTokenType.Null)
                    return null;

                // Obsługa dla wartości prostych (string)
                if (reader.TokenType == JsonTokenType.String)
                {
                    string enumValue = reader.GetString()!;
                    return (TEnum)Enum.Parse(typeof(TEnum), enumValue);
                }

                // Obsługa dla formatu obiektu {"HasValue":true,"Value":"Educational"}
                if (reader.TokenType == JsonTokenType.StartObject)
                {
                    string? enumValue = null;
                    bool hasValue = false;

                    // Odczytaj właściwości obiektu
                    while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
                    {
                        if (reader.TokenType == JsonTokenType.PropertyName)
                        {
                            string propertyName = reader.GetString()!;
                            reader.Read();

                            if (propertyName.Equals("Value", StringComparison.OrdinalIgnoreCase) && reader.TokenType == JsonTokenType.String)
                            {
                                enumValue = reader.GetString();
                            }
                            else if (propertyName.Equals("HasValue", StringComparison.OrdinalIgnoreCase) && reader.TokenType == JsonTokenType.True)
                            {
                                hasValue = true;
                            }
                        }
                    }

                    if (hasValue && enumValue != null)
                    {
                        return (TEnum)Enum.Parse(typeof(TEnum), enumValue);
                    }
                }

                return null;
            }

            public override void Write(Utf8JsonWriter writer, TEnum? value, JsonSerializerOptions options)
            {
                if (!value.HasValue)
                {
                    writer.WriteNullValue();
                    return;
                }

                writer.WriteStringValue(value.Value.ToString());
            }
        }
    }

    /// <summary>
    /// Pobierz opis całego schematu na podstawie atrybutu Description klasy.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    static string? GetSchemaDescription<T>()
    {
        var type = typeof(T);

        // Sprawdź czy klasa ma atrybut Description
        var descriptionAttr = type.GetCustomAttribute<DescriptionAttribute>();
        if (descriptionAttr != null && !string.IsNullOrEmpty(descriptionAttr.Description))
        {
            return descriptionAttr.Description;
        }

        // Alternatywnie możesz utworzyć domyślny opis na podstawie nazwy typu
        return $"Response format for {type.Name}";
    }

    /// <summary>
    /// Wygeneruj schemat JSON dla danego typu.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    static string GenerateJsonSchema<T>()
    {
        Type type = typeof(T);
        var schema = GenerateJsonSchemaForType(type);

        return JsonSerializer.Serialize(WrapWithResult(schema));
    }

    /// <summary>
    /// Wygeneruj schemat JSON dla danego typu z obsługą atrybutów Description.
    /// </summary>
    /// <param name="type"></param>
    /// <returns></returns>
    static Dictionary<string, object> GenerateJsonSchemaForType(Type type)
    {
        if (type == typeof(string) || type.IsPrimitive || type == typeof(bool) || type == typeof(decimal))
        {
            return new Dictionary<string, object> { ["type"] = GetJsonType(type) };
        }

        if (type.IsEnum)
        {
            var enumValues = Enum.GetNames(type).ToList();
            return new Dictionary<string, object>
            {
                ["type"] = "string",
                ["enum"] = enumValues
            };
        }

        // Obsługa nullable enum
        if (Nullable.GetUnderlyingType(type)?.IsEnum == true)
        {
            var underlyingEnumType = Nullable.GetUnderlyingType(type)!;
            var enumValues = Enum.GetNames(underlyingEnumType).ToList();
            return new Dictionary<string, object>
            {
                ["type"] = "string",
                ["enum"] = enumValues
            };
        }

        if (type.IsArray || IsGenericList(type))
        {
            var elementType = type.IsArray ? type.GetElementType()! : type.GetGenericArguments()[0];
            return new Dictionary<string, object>
            {
                ["type"] = "array",
                ["items"] = GenerateJsonSchemaForType(elementType),
                ["additionalProperties"] = false
            };
        }

        var properties = new Dictionary<string, object>();
        var requiredProperties = new List<string>();

        foreach (var prop in type.GetProperties())
        {
            var propSchema = GenerateJsonSchemaForType(prop.PropertyType);

            // Dodaj description z atrybutu, jeśli istnieje
            var descriptionAttr = prop.GetCustomAttribute<DescriptionAttribute>();
            if (descriptionAttr != null && !string.IsNullOrEmpty(descriptionAttr.Description))
            {
                propSchema["description"] = descriptionAttr.Description;
            }

            properties[prop.Name] = propSchema;

            // W strict mode OpenAI wymaga wszystkich właściwości w required
            requiredProperties.Add(prop.Name);
        }

        var schema = new Dictionary<string, object>
        {
            ["type"] = "object",
            ["properties"] = properties,
            ["additionalProperties"] = false
        };

        // W strict mode zawsze dodaj required z wszystkimi właściwościami
        if (requiredProperties.Count > 0)
        {
            schema["required"] = requiredProperties;
        }

        return schema;
    }

    /// <summary>
    /// Obejmij schemat odpowiedzi zewnętrznej w obiekcie "result".
    /// </summary>
    /// <param name="schema"></param>
    /// <returns></returns>
    static Dictionary<string, object> WrapWithResult(Dictionary<string, object> schema)
    {
        return new Dictionary<string, object>
        {
            ["type"] = "object",
            ["properties"] = new Dictionary<string, object> { ["result"] = schema },
            ["required"] = new List<string> { "result" },
            ["additionalProperties"] = false
        };
    }

    /// <summary>
    /// Sprawdź, czy dany typ jest listą generyczną.
    /// </summary>
    /// <param name="type"></param>
    /// <returns></returns>
    static bool IsGenericList(Type type) => type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>);

    /// <summary>
    /// Pobierz typ JSON dla danego typu.
    /// </summary>
    /// <param name="type"></param>
    /// <returns></returns>
    static string GetJsonType(Type type) => type switch
    {
        _ when type == typeof(string) => "string",
        _ when type == typeof(int) || type == typeof(long) || type == typeof(short) || type == typeof(byte) => "integer",
        _ when type == typeof(bool) => "boolean",
        _ when type == typeof(float) || type == typeof(double) || type == typeof(decimal) => "number",
        _ when type.IsArray || IsGenericList(type) => "array",
        _ => "object"
    };

    [GeneratedRegex(@"\{\{\$(\w+)\}\}")]
    private static partial Regex VariablePlaceholderRegex();
}