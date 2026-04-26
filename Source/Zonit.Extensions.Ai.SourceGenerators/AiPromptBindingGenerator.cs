using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Immutable;
using System.Linq;
using System.Text;

namespace Zonit.Extensions.Ai.SourceGenerators;

/// <summary>
/// Emits AOT-safe <see cref="System.Action"/> bindings that populate a
/// Scriban <c>ScriptObject</c> from each concrete <c>PromptBase&lt;T&gt;</c>
/// subclass — replacing reflection-based property discovery in
/// <c>PromptBase&lt;T&gt;.RenderTemplate()</c>.
/// </summary>
/// <remarks>
/// The generated file registers the bindings via a
/// <c>[ModuleInitializer]</c>, so the registry is populated as soon as the
/// consumer assembly is loaded.
/// </remarks>
[Generator]
public class AiPromptBindingGenerator : IIncrementalGenerator
{
    private static readonly string[] ExcludedPropertyNames =
    {
        "System",   // PromptBase / IPrompt
        "Text",     // PromptBase / IPrompt
        "Files",    // PromptBase / IPrompt
        "Prompt",   // PromptBase
    };

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var promptClasses = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (node, _) => IsCandidateClass(node),
                transform: static (ctx, _) => GetPromptBindingInfo(ctx))
            .Where(static info => info is not null);

        var compilation = context.CompilationProvider.Combine(promptClasses.Collect());
        context.RegisterSourceOutput(compilation, static (spc, source) => Execute(source.Right!, spc));
    }

    private static bool IsCandidateClass(SyntaxNode node)
    {
        return node is ClassDeclarationSyntax classDecl
               && classDecl.BaseList != null
               && classDecl.BaseList.Types.Any();
    }

    private static PromptBindingInfo? GetPromptBindingInfo(GeneratorSyntaxContext context)
    {
        var classDecl = (ClassDeclarationSyntax)context.Node;
        if (context.SemanticModel.GetDeclaredSymbol(classDecl) is not INamedTypeSymbol classSymbol)
            return null;

        if (classSymbol.IsAbstract)
            return null;

        // Walk the inheritance chain looking for PromptBase<T> in the Zonit namespace.
        var promptBase = classSymbol.BaseType;
        while (promptBase is not null)
        {
            if (promptBase.IsGenericType
                && promptBase.ConstructedFrom.Name == "PromptBase"
                && promptBase.ContainingNamespace.ToDisplayString() == "Zonit.Extensions.Ai")
            {
                break;
            }
            promptBase = promptBase.BaseType;
        }

        if (promptBase is null)
            return null;

        // Collect public, instance, readable properties — including those declared
        // on intermediate base classes (e.g. ImagePromptBase) but excluding members
        // already on PromptBase / IPrompt.
        var seen = new HashSet<string>(System.StringComparer.Ordinal);
        var properties = new List<(string Name, string SnakeCase)>();

        for (var current = classSymbol; current is not null; current = current.BaseType)
        {
            // Stop at PromptBase — its own members are excluded.
            if (current.IsGenericType
                && current.ConstructedFrom.Name == "PromptBase"
                && current.ContainingNamespace.ToDisplayString() == "Zonit.Extensions.Ai")
            {
                break;
            }

            foreach (var member in current.GetMembers())
            {
                if (member is not IPropertySymbol prop)
                    continue;

                if (prop.IsStatic
                    || prop.IsIndexer
                    || prop.GetMethod is null
                    || prop.DeclaredAccessibility != Accessibility.Public)
                {
                    continue;
                }

                if (System.Array.IndexOf(ExcludedPropertyNames, prop.Name) >= 0)
                    continue;

                if (!seen.Add(prop.Name))
                    continue; // overridden / shadowed — keep the most-derived one.

                properties.Add((prop.Name, ToSnakeCase(prop.Name)));
            }
        }

        if (properties.Count == 0)
            return new PromptBindingInfo(classSymbol.ToDisplayString(), ImmutableArray<PromptProperty>.Empty);

        return new PromptBindingInfo(
            classSymbol.ToDisplayString(),
            properties.Select(p => new PromptProperty(p.Name, p.SnakeCase)).ToImmutableArray());
    }

    private static void Execute(ImmutableArray<PromptBindingInfo?> infos, SourceProductionContext context)
    {
        var prompts = infos
            .Where(i => i is not null)
            .Cast<PromptBindingInfo>()
            .OrderBy(p => p.FullClassName, System.StringComparer.Ordinal)
            .ToList();

        if (prompts.Count == 0)
            return;

        context.AddSource("AiPromptBindings.g.cs", GenerateBindings(prompts));
    }

    private static string GenerateBindings(List<PromptBindingInfo> prompts)
    {
        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated/>");
        sb.AppendLine("#nullable enable");
        sb.AppendLine();
        sb.AppendLine("using System;");
        sb.AppendLine("using System.Runtime.CompilerServices;");
        sb.AppendLine("using Scriban.Runtime;");
        sb.AppendLine("using Zonit.Extensions.Ai;");
        sb.AppendLine();
        sb.AppendLine("namespace Zonit.Extensions.Ai.Generated;");
        sb.AppendLine();
        sb.AppendLine("/// <summary>");
        sb.AppendLine("/// AOT-safe Scriban bindings for prompt types in this assembly.");
        sb.AppendLine("/// Registered via a module initializer at assembly load time.");
        sb.AppendLine("/// </summary>");
        sb.AppendLine("internal static class __AiPromptBindings");
        sb.AppendLine("{");
        sb.AppendLine("    [ModuleInitializer]");
        sb.AppendLine("    internal static void Init()");
        sb.AppendLine("    {");

        foreach (var prompt in prompts)
        {
            sb.AppendLine($"        PromptBindingRegistry.Register(typeof(global::{prompt.FullClassName}), static (instance, obj) =>");
            sb.AppendLine("        {");
            sb.AppendLine($"            var p = (global::{prompt.FullClassName})instance;");
            foreach (var prop in prompt.Properties)
            {
                sb.AppendLine($"            obj.Add(\"{prop.SnakeCase}\", p.{prop.Name});");
            }
            sb.AppendLine("        });");
        }

        sb.AppendLine("    }");
        sb.AppendLine("}");
        return sb.ToString();
    }

    private static string ToSnakeCase(string name)
    {
        var sb = new StringBuilder(name.Length + 4);
        for (var i = 0; i < name.Length; i++)
        {
            var c = name[i];
            if (char.IsUpper(c))
            {
                if (i > 0)
                    sb.Append('_');
                sb.Append(char.ToLowerInvariant(c));
            }
            else
            {
                sb.Append(c);
            }
        }
        return sb.ToString();
    }

    private sealed record PromptBindingInfo(string FullClassName, ImmutableArray<PromptProperty> Properties);
    private sealed record PromptProperty(string Name, string SnakeCase);
}
