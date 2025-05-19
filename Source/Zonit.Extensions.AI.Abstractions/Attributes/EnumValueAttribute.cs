namespace Zonit.Extensions.Ai;

[AttributeUsage(AttributeTargets.Field, AllowMultiple = false)]
public class EnumValueAttribute(string value) : Attribute
{
    public string Value { get; } = value;
}