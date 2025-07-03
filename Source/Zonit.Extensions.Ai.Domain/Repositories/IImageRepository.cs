using Zonit.Extensions.Ai.Llm;

namespace Zonit.Extensions.Ai.Domain.Repositories;

public interface IImageRepository
{
    Task<Result<IFile>> GenerateAsync(IImageLlmBase llm, IPromptBase<IFile> prompt, CancellationToken cancellationToken = default);
}
