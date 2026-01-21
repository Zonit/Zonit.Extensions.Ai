using System.Diagnostics.CodeAnalysis;
using Scriban;
using Scriban.Runtime;
using Zonit.Extensions;

namespace Zonit.Extensions.Ai;

/// <summary>
/// Base class for creating typed prompts with Scriban templating.
/// Inherit from this class to create your prompts.
/// </summary>
/// <typeparam name="TResponse">The expected response type - strongly typed!</typeparam>
/// <remarks>
/// This class uses reflection to discover properties for template rendering.
/// It is not compatible with AOT compilation without additional configuration.
/// </remarks>
[RequiresUnreferencedCode("Uses reflection to get properties for template rendering.")]
public abstract class PromptBase<TResponse> : IPrompt<TResponse>
{
    /// <summary>
    /// Override to provide system message/instruction.
    /// </summary>
    public virtual string? System => null;

    /// <summary>
    /// Override to provide the prompt with Scriban syntax.
    /// Properties are available as snake_case (e.g., MyProperty -> {{ my_property }}).
    /// </summary>
    public abstract string Prompt { get; }

    /// <summary>
    /// Gets the rendered prompt text.
    /// </summary>
    public string Text => RenderTemplate();

    /// <summary>
    /// Files attached to this prompt.
    /// Uses the Asset value object from Zonit.Extensions.
    /// </summary>
    public virtual IReadOnlyList<Asset>? Files { get; init; }

    private string RenderTemplate()
    {
        var template = Scriban.Template.Parse(Prompt);

        if (template.HasErrors)
            throw new InvalidOperationException($"Template parse error: {string.Join(", ", template.Messages)}");

        var scriptObject = new ScriptObject();

        // Add all public properties as snake_case
        foreach (var prop in GetType().GetProperties())
        {
            if (prop.Name is nameof(System) or nameof(Text) or nameof(Files) or nameof(Prompt))
                continue;

            var value = prop.GetValue(this);
            var snakeName = ToSnakeCase(prop.Name);
            scriptObject.Add(snakeName, value);
        }

        var context = new TemplateContext();
        context.PushGlobal(scriptObject);

        return template.Render(context);
    }

    private static string ToSnakeCase(string name)
    {
        var result = new System.Text.StringBuilder();

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

/// <summary>
/// Simple prompt for quick one-off usage without creating a class.
/// </summary>
/// <typeparam name="TResponse">The expected response type.</typeparam>
public sealed class SimplePrompt<TResponse> : IPrompt<TResponse>
{
    /// <summary>
    /// Creates a simple prompt with text.
    /// </summary>
    public SimplePrompt(string text)
    {
        Text = text ?? throw new ArgumentNullException(nameof(text));
    }

    /// <inheritdoc />
    public string? System { get; init; }

    /// <inheritdoc />
    public string Text { get; }

    /// <inheritdoc />
    public IReadOnlyList<Asset>? Files { get; init; }
}

/// <summary>
/// Base class for image generation prompts with Scriban templating.
/// Inherit from this class to create typed image prompts with parameters.
/// </summary>
/// <remarks>
/// This class uses reflection to discover properties for template rendering.
/// It is not compatible with AOT compilation without additional configuration.
/// </remarks>
/// <example>
/// <code>
/// public class ProductImagePrompt : ImagePromptBase
/// {
///     public required string ProductName { get; init; }
///     public required string Style { get; init; }
///     
///     public override string Prompt => 
///         "Professional product photo of {{ product_name }} in {{ style }} style";
/// }
/// </code>
/// </example>
[RequiresUnreferencedCode("Uses reflection to get properties for template rendering.")]
public abstract class ImagePromptBase : PromptBase<Asset>
{
}

/// <summary>
/// Simple image prompt for quick one-off usage without creating a class.
/// Returns an Asset containing the generated image.
/// </summary>
public sealed class SimpleImagePrompt : IPrompt<Asset>
{
    /// <summary>
    /// Creates a simple image prompt with description.
    /// </summary>
    public SimpleImagePrompt(string description)
    {
        Text = description ?? throw new ArgumentNullException(nameof(description));
    }

    /// <inheritdoc />
    public string? System { get; init; }

    /// <inheritdoc />
    public string Text { get; }

    /// <inheritdoc />
    public IReadOnlyList<Asset>? Files { get; init; }
}
