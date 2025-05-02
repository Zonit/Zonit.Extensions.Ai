using System.Text.RegularExpressions;
using Zonit.Extensions.AI;
using Zonit.Extensions.AI.Services.OpenAi;

public partial class ImageService(OpenAiImageService openAiImageService) : IImageClient
{
    private readonly OpenAiImageService _openAiImageService = openAiImageService;
    private readonly Dictionary<string, object?> _variables = [];

    // Prywatna metoda klonująca obiekt
    private ImageService CreateNewInstanceWithState()
    {
        var newInstance = new ImageService(_openAiImageService);

        // Kopiujemy wszystkie zmienne do nowej instancji
        foreach (var variable in _variables)
        {
            newInstance._variables[variable.Key] = variable.Value;
        }

        return newInstance;
    }

    public IImageClient AddVariable(string key, string? value)
    {
        var newClient = CreateNewInstanceWithState();
        newClient._variables[key] = value;
        return newClient;
    }

    public IImageClient AddVariable(string key, string[]? values)
    {
        var newClient = CreateNewInstanceWithState();
        newClient._variables[key] = values is null ? null : string.Join(", ", values);
        return newClient;
    }

    public IImageClient AddVariable(string key, int? value)
    {
        var newClient = CreateNewInstanceWithState();
        newClient._variables[key] = value?.ToString();
        return newClient;
    }

    public IImageClient AddVariable(string key, decimal? value)
    {
        var newClient = CreateNewInstanceWithState();
        newClient._variables[key] = value?.ToString();
        return newClient;
    }

    public IImageClient AddVariable(string key, bool? value)
    {
        var newClient = CreateNewInstanceWithState();
        newClient._variables[key] = value?.ToString();
        return newClient;
    }

    public IImageClient AddVariable(string key, DateTime? value)
    {
        var newClient = CreateNewInstanceWithState();
        newClient._variables[key] = value?.ToString("O");
        return newClient;
    }

    public IImageClient AddVariable(string key, Guid? value)
    {
        var newClient = CreateNewInstanceWithState();
        newClient._variables[key] = value?.ToString();
        return newClient;
    }

    public IImageClient AddVariable(string key, IFile? value)
    {
        var newClient = CreateNewInstanceWithState();
        newClient._variables[key] = value;
        return newClient;
    }

    public async Task<Result<IFile>> GenerateImageAsync(string prompt, IBaseModel model, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(prompt))
            throw new ArgumentNullException(nameof(prompt), "Prompt cannot be null or empty.");

        if (model.OutputImage is false)
            throw new ArgumentException("Model does not support image output.", nameof(model));

        if (model is not IImageModel imageModel)
            throw new NotSupportedException($"Model type {model.GetType().Name} is not supported.");

        // 1. Podmień zmienne tekstowe w prompt
        prompt = ReplacePromptVariables(prompt);

        // 2. (Opcjonalnie) przygotuj contentParts dla obrazków — jeżeli Twój openAiImageService to obsługuje

        return await _openAiImageService.GenerateAsync(prompt, imageModel, cancellationToken);
    }

    public Task<Result<IReadOnlyCollection<IFile>>> GenerateImagesAsync(string prompt, IBaseModel model, CancellationToken cancellationToken = default)
    {
        // Podobnie jak GenerateImageAsync, tylko dla wielu plików.
        throw new NotImplementedException();
    }

    private string ReplacePromptVariables(string prompt)
    {
        if (_variables.Count == 0)
            return prompt;

        var variableDict = _variables
            .Where(v => v.Value is string)
            .ToDictionary(v => v.Key, v => v.Value as string);

        return VariablePlaceholderRegex().Replace(prompt, match =>
        {
            string key = match.Groups[1].Value;
            return variableDict.TryGetValue(key, out string? value) ? value ?? match.Value : match.Value;
        });
    }

    [GeneratedRegex(@"\{\{\$(\w+)\}\}")]
    private static partial Regex VariablePlaceholderRegex();
}
