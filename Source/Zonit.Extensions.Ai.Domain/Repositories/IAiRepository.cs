using Zonit.Extensions.Ai.Abstractions;
using Zonit.Extensions.Ai.Llm;

namespace Zonit.Extensions.Ai.Domain.Repositories;

public interface IAiRepository
{
    Task<Result<TResponse>> ResponseAsync<TResponse>(ILlmBase llm, IPromptBase<TResponse> prompt);
}
