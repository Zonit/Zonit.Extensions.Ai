using Zonit.Extensions.Ai.Llm;

namespace Zonit.Extensions.Ai.Domain.Repositories;

public interface ITextRepository
{
    Task<Result<TResponse>> ResponseAsync<TResponse>(ITextLlmBase llm, IPromptBase<TResponse> prompt, CancellationToken cancellationToken = default);
}
