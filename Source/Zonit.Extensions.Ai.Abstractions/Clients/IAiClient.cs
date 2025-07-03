using Zonit.Extensions.Ai.Llm;

namespace Zonit.Extensions.Ai;

public interface IAiClient
{
    Task<Result<TResponse>?> GenerateAsync<TResponse>(IPromptBase<TResponse> prompt, ITextLlmBase model, CancellationToken cancellationToken = default);
    Task<Result<IFile>?> GenerateAsync(IPromptBase<IFile> prompt, IImageLlmBase model, CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyCollection<IFile>>?> GenerateAsync(IPromptBase<IReadOnlyCollection<IFile>> prompt, IImageLlmBase model, CancellationToken cancellationToken = default);
}