namespace Zonit.Extensions.AI;

public class Result<T>
{
    public required T Value { get; init; }
    public required MetaData MetaData { get; init; }
}