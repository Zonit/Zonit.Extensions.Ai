using Zonit.Extensions.AI.Services.OpenAi;

namespace Zonit.Extensions.AI.Services;

internal class ImageService(
    OpenAiImageService openAiImageService
    ) : VariableService<IImageClient>, IImageClient
{
    public Task<Result<IFile>> GenerateImageAsync(string prompt, BaseModel model, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(prompt))
            throw new ArgumentNullException(nameof(prompt), "Prompt cannot be null or empty.");

        if (model.OutputImage is false)
            throw new ArgumentException("Model does not support image output.", nameof(model));

        if (model is IImageModel imageModel)
            return openAiImageService.GenerateAsync(prompt, imageModel, cancellationToken);
        
        throw new NotSupportedException($"Model type {model.GetType().Name} is not supported.");
    }

    public Task<Result<IReadOnlyCollection<IFile>>> GenerateImagesAsync(string prompt, BaseModel model, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }
}
