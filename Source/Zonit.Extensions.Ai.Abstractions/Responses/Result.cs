namespace Zonit.Extensions.Ai;

public class Result<T>
{
    public required T Value { get; init; }
    public required MetaData MetaData { get; init; }

    public void SetProcessName(string processName)
        => MetaData.Process = processName;
}