using Zonit.Extensions.Ai.Abstractions;
using Zonit.Extensions.Ai.Llm;

namespace Zonit.Extensions.Ai.Application.Services;

public interface IResponseService
{
    Task<Result<TModel>?> GenerateAsync<TModel>(IPromptBase prompt, ILlmBase model, CancellationToken cancellationToken = default);
}

public class ResponseService : IResponseService
{
    public async Task<Result<TModel>?> GenerateAsync<TModel>(IPromptBase prompt, ILlmBase model, CancellationToken cancellationToken = default)
    {
        if (prompt is OpenAiBase)
        {

        }


        return default;
    }
}