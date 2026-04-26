using System.Diagnostics.CodeAnalysis;

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
    public static string GetEnumValue<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicFields)] TEnum>(
        this TEnum enumValue) where TEnum : Enum
    {
        // Enum members are public static fields — Type.GetField only requires
        // DAM(PublicFields), unlike Type.GetMember which would need 5 categories.
        var name = enumValue.ToString();
        var field = typeof(TEnum).GetField(name);
        if (field is not null)
        {
            var attributes = field.GetCustomAttributes(typeof(EnumValueAttribute), false);
            if (attributes.Length > 0)
            {
                return ((EnumValueAttribute)attributes[0]).Value;
            }
        }
        return name.ToLowerInvariant();
    }
}
