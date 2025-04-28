namespace Zonit.Extensions.AI;

[AttributeUsage(AttributeTargets.Field, AllowMultiple = false)]
public class EnumValueAttribute(string value) : Attribute
{
    public string Value { get; } = value;
}