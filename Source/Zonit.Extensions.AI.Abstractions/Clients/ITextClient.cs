namespace Zonit.Extensions.Ai;

public interface ITextClient : IVariable<ITextClient>
{
    Task<Result<TValue>> GenerateAsync<TValue>(string prompt, BaseModel model, CancellationToken cancellationToken = default);
}