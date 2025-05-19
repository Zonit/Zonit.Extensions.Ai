namespace Zonit.Extensions.Ai;

public interface IVariable<TClient>
{
    TClient AddVariable(string key, string? value);
    TClient AddVariable(string key, string[]? values);
    TClient AddVariable(string key, int? value);
    TClient AddVariable(string key, decimal? value);
    TClient AddVariable(string key, bool? value);
    TClient AddVariable(string key, DateTime? value);
    TClient AddVariable(string key, Guid? value);
}