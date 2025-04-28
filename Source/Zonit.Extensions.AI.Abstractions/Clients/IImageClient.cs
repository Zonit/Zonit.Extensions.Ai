namespace Zonit.Extensions.AI;

public interface IImageClient : IVariable<IImageClient>
{
    Task<Result<IFile>> GenerateImageAsync(string prompt, BaseModel model, CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyCollection<IFile>>> GenerateImagesAsync(string prompt, BaseModel model, CancellationToken cancellationToken = default);
}