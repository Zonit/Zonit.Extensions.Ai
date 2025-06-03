namespace Zonit.Extensions.Ai;

[AttributeUsage(AttributeTargets.Property)]
public class PromptKeyAttribute(string? name = null) : Attribute
{
    public string? Name { get; } = name;
}