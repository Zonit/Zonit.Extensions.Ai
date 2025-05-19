//using Microsoft.Extensions.Options;
//using System.Diagnostics;
//using System.Text.Json;
//using System.Text.RegularExpressions;
//using Zonit.Extensions.Ai.Abstractions.Options;

//namespace Zonit.Extensions.Ai.Infrastructure.Repositories.OpenAi;

//internal partial class OpenAiTextService(IOptions<AiOptions> _options)
//{
//    public async Task<Result<TValue>> GenerateAsync<TValue>(BaseModel model, string prompt, IReadOnlyCollection<Variable>? variables, CancellationToken cancellationToken = default)
//    {
//        if(model is not BaseOpenAiText)
//            throw new ArgumentException("Model type is not supported.");

//        var stopwatch = new Stopwatch();
//        List<ChatMessageContentPart> contentParts = new List<ChatMessageContentPart>();
//        var client = new ChatClient(model: model.Name, apiKey: ApiKey);
//        var messages = new List<ChatMessage>();

//        if (variables is not null)
//        {
//            var variableDict = variables
//                .Where(v => v.Value is string)
//                .ToDictionary(v => v.Key, v => v.Value as string);

//            prompt = VariablePlaceholderRegex().Replace(prompt, match =>
//            {
//                string key = match.Groups[1].Value;
//                return variableDict.TryGetValue(key, out string? value) ? value ?? match.Value : match.Value;
//            });

//            foreach (var variable in variables.Where(v => v.Value is not string))
//            {
//                if (variable.Value is Uri imageUri)
//                {
//                    var imagePart = ChatMessageContentPart.CreateImagePart(imageUri, ChatImageDetailLevel.Auto);
//                    contentParts.Add(imagePart);
//                }
//                else if (variable.Value is MemoryStream imageBytes)
//                {
//                    var binaryData = new BinaryData(imageBytes.ToArray());
//                    string mimeType = "image/png"; // Ustaw odpowiedni typ MIME, jeśli jest dostępny
//                    var imagePart = ChatMessageContentPart.CreateImagePart(binaryData, mimeType, ChatImageDetailLevel.Auto);
//                    contentParts.Add(imagePart);
//                }
//            }
//        }

//        contentParts.Insert(0, prompt);
//        messages.Add(new SystemChatMessage([.. contentParts]));

//        var options = new ChatCompletionOptions
//        {
//            ResponseFormat = ChatResponseFormat.CreateJsonSchemaFormat(
//                jsonSchemaFormatName: "response",
//                jsonSchema: BinaryData.FromString(GenerateJsonSchema<TValue>()),
//                jsonSchemaIsStrict: true),
//            EndUserId = "zonit", // TODO: Przypisz do zmiennej
//        };

//        if (model is ModelOpenAiGPT modelGPT)
//        {
//            options.MaxOutputTokenCount = modelGPT.MaxTokens;
//            options.Temperature = (float)modelGPT.Temperature;
//            options.TopP = (float)modelGPT.TopP;
//            options.FrequencyPenalty = (float)modelGPT.FrequencyPenalty;
//            options.PresencePenalty = (float)modelGPT.PresencePenalty;
//        }

//        stopwatch.Start();
//        ChatCompletion completion = await client.CompleteChatAsync(messages, options, cancellationToken);
//        stopwatch.Stop();

//        var responseJson = completion.Content[0].Text.Trim();

//        try
//        {
//            var parsedResponse = JsonSerializer.Deserialize<JsonElement>(responseJson);

//            // Pobierz "result" jeśli istnieje, inaczej użyj całego JSON-a
//            var jsonToDeserialize = parsedResponse.TryGetProperty("result", out var resultElement)
//                ? resultElement.GetRawText()
//                : responseJson;

//            var result = JsonSerializer.Deserialize<TValue>(jsonToDeserialize, new JsonSerializerOptions
//            {
//                PropertyNameCaseInsensitive = true
//            }) ?? throw new JsonException("Deserialization returned null.");

//            return new Result<TValue>()
//            {
//                Value = result,
//                MetaData = new(completion.Model, completion.Usage.InputTokenCount, completion.Usage.OutputTokenCount, stopwatch.Elapsed)
//            };
//        }
//        catch (Exception ex) when (ex is JsonException || ex is InvalidOperationException)
//        {
//            throw new JsonException($"Failed to parse JSON: {responseJson}", ex);
//        }
//    }

//    /// <summary>
//    /// Wygeneruj schemat JSON dla danego typu.
//    /// </summary>
//    /// <typeparam name="T"></typeparam>
//    /// <returns></returns>
//    static string GenerateJsonSchema<T>()
//    {
//        Type type = typeof(T);
//        var schema = GenerateJsonSchemaForType(type);

//        return JsonSerializer.Serialize(WrapWithResult(schema));
//    }

//    /// <summary>
//    /// Wygeneruj schemat JSON dla danego typu.
//    /// </summary>
//    /// <param name="type"></param>
//    /// <returns></returns>
//    static Dictionary<string, object> GenerateJsonSchemaForType(Type type)
//    {
//        if (type == typeof(string) || type.IsPrimitive || type == typeof(bool) || type == typeof(decimal))
//        {
//            return new Dictionary<string, object> { ["type"] = GetJsonType(type) };
//        }

//        if (type.IsArray || IsGenericList(type))
//        {
//            var elementType = type.IsArray ? type.GetElementType()! : type.GetGenericArguments()[0];
//            return new Dictionary<string, object>
//            {
//                ["type"] = "array",
//                ["items"] = GenerateJsonSchemaForType(elementType),
//                ["additionalProperties"] = false
//            };
//        }

//        var properties = type.GetProperties().ToDictionary(
//            prop => prop.Name,
//            prop => GenerateJsonSchemaForType(prop.PropertyType)
//        );

//        return new Dictionary<string, object>
//        {
//            ["type"] = "object",
//            ["properties"] = properties,
//            ["required"] = properties.Keys.ToList(),
//            ["additionalProperties"] = false
//        };
//    }

//    /// <summary>
//    /// Obejmij schemat odpowiedzi zewnętrznej w obiekcie "result".
//    /// </summary>
//    /// <param name="schema"></param>
//    /// <returns></returns>
//    static Dictionary<string, object> WrapWithResult(Dictionary<string, object> schema)
//    {
//        return new Dictionary<string, object>
//        {
//            ["type"] = "object",
//            ["properties"] = new Dictionary<string, object> { ["result"] = schema },
//            ["required"] = new List<string> { "result" },
//            ["additionalProperties"] = false
//        };
//    }

//    /// <summary>
//    /// Sprawdź, czy dany typ jest listą generyczną.
//    /// </summary>
//    /// <param name="type"></param>
//    /// <returns></returns>
//    static bool IsGenericList(Type type) => type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>);

//    /// <summary>
//    /// Pobierz typ JSON dla danego typu.
//    /// </summary>
//    /// <param name="type"></param>
//    /// <returns></returns>
//    static string GetJsonType(Type type) => type switch
//    {
//        _ when type == typeof(string) => "string",
//        _ when type == typeof(int) || type == typeof(long) || type == typeof(short) || type == typeof(byte) => "integer",
//        _ when type == typeof(bool) => "boolean",
//        _ when type == typeof(float) || type == typeof(double) || type == typeof(decimal) => "number",
//        _ when type.IsArray || IsGenericList(type) => "array",
//        _ => "object"
//    };
//    [GeneratedRegex(@"\{\{\$(\w+)\}\}")]
//    private static partial Regex VariablePlaceholderRegex();
//}
