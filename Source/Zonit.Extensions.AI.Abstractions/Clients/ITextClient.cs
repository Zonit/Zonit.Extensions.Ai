namespace Zonit.Extensions.AI;

public interface ITextClient : IVariable<ITextClient>
{
    Task<Result<TValue>> GenerateAsync<TValue>(string prompt, BaseModel model, CancellationToken cancellationToken = default);
}