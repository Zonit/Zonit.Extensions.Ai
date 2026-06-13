using System.Text;

namespace Zonit.Extensions.Ai;

/// <summary>
/// Shared naming conventions for the AI layer. Currently the snake_case mapping used to expose
/// values to Scriban templates (PascalCase / camelCase property or JSON key → snake_case variable).
/// </summary>
internal static class AiNaming
{
    /// <summary>
    /// Converts a PascalCase / camelCase name to snake_case (e.g. <c>TimeFrame</c> / <c>timeFrame</c>
    /// → <c>time_frame</c>). Names already lower/snake pass through unchanged.
    /// </summary>
    public static string ToSnakeCase(string name)
    {
        var result = new StringBuilder(name.Length + 4);
        for (var i = 0; i < name.Length; i++)
        {
            var c = name[i];
            if (char.IsUpper(c))
            {
                if (i > 0)
                    result.Append('_');
                result.Append(char.ToLowerInvariant(c));
            }
            else
            {
                result.Append(c);
            }
        }
        return result.ToString();
    }
}
