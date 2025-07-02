using Zonit.Extensions.Ai.Llm;

namespace Zonit.Extensions.Ai.Abstractions.Clients;

public interface IResponseClient
{
    Task ResponseAsync(ILlmBase llm, IPromptBase prompt);
}