using Microsoft.Extensions.DependencyInjection;
using Zonit.Extensions.Ai.Domain.Repositories;
using Zonit.Extensions.Ai.Llm;

namespace Zonit.Extensions.Ai.Application.Services;

public class AiService(
    [FromKeyedServices("OpenAi")] ITextRepository openAiRepository,
    [FromKeyedServices("OpenAi")] IImageRepository openAiImageRepository,


    [FromKeyedServices("X")] ITextRepository xAiRepository
    ) : IAiClient
{
    public async Task<Result<TResponse>> GenerateAsync<TResponse>(IPromptBase<TResponse> prompt, ITextLlmBase model, CancellationToken cancellationToken = default)
    {
        if (model is OpenAiBase)
            return await openAiRepository.ResponseAsync(model, prompt, cancellationToken);
        else if (model is XBase)
            return await xAiRepository.ResponseAsync(model, prompt, cancellationToken);

        return default;
    }

    public async Task<Result<IFile>> GenerateAsync(IPromptBase<IFile> prompt, IImageLlmBase model, CancellationToken cancellationToken = default)
    {
        if (model is OpenAiBase)
            return await openAiImageRepository.GenerateAsync(model, prompt, cancellationToken);

        return default;
    }


    public async Task<Result<IReadOnlyCollection<IFile>>> GenerateAsync(IPromptBase<IReadOnlyCollection<IFile>> prompt, IImageLlmBase model, CancellationToken cancellationToken = default)
    {
        if (prompt is OpenAiBase)
        {

        }

        return default;
    }
}