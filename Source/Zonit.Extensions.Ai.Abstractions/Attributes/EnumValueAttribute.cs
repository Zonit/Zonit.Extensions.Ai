namespace Zonit.Extensions.Ai;

/// <summary>
/// Specifies the API value for an enum member.
/// Used to map enum values to their string representation in API requests.
/// </summary>
[AttributeUsage(AttributeTargets.Field, AllowMultiple = false)]
public sealed class EnumValueAttribute : Attribute
{
    /// <summary>
    /// Gets the API value for this enum member.
    /// </summary>
    public string Value { get; }

    /// <summary>
    /// Creates a new EnumValueAttribute with the specified API value.
    /// </summary>
    /// <param name="value">The string value to use in API requests.</param>
    public EnumValueAttribute(string value)
    {
        Value = value;
    }
}

/// <summary>
/// Extension methods for reading EnumValue attributes.
/// </summary>
public static class EnumValueExtensions
{
    /// <summary>
    /// Gets the EnumValue attribute value for an enum member.
    /// Returns the enum member name if no attribute is defined.
    /// </summary>
    public static string GetEnumValue<TEnum>(this TEnum enumValue) where TEnum : Enum
    {
        var memberInfo = typeof(TEnum).GetMember(enumValue.ToString());
        if (memberInfo.Length > 0)
        {
            var attributes = memberInfo[0].GetCustomAttributes(typeof(EnumValueAttribute), false);
            if (attributes.Length > 0)
            {
                return ((EnumValueAttribute)attributes[0]).Value;
            }
        }
        return enumValue.ToString().ToLowerInvariant();
    }
}
