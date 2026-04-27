using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Immutable;
using System.Linq;
using System.Text;

namespace Zonit.Extensions.Ai.SourceGenerators;

/// <summary>
/// Emits AOT-safe <c>JsonTypeInfo&lt;T&gt;</c> factories for every response
/// type referenced by a <c>PromptBase&lt;T&gt;</c> subclass in the consumer
/// assembly, plus all transitively reachable POCO / collection / nullable
/// types. Factories are registered with <c>AiJsonTypeInfoResolver</c> through
/// a <c>[ModuleInitializer]</c>.
/// </summary>
/// <remarks>
/// <para>
/// Supported shapes (v1):
/// </para>
/// <list type="bullet">
///   <item>Public POCOs with a parameterless constructor and public settable
///         properties (records with parameterless ctor included).</item>
///   <item>Primitive types: <c>string</c>, <c>bool</c>, all integer / floating
///         / decimal types, <c>char</c>, <c>byte</c>, <c>DateTime</c>,
///         <c>DateTimeOffset</c>, <c>DateOnly</c>, <c>TimeOnly</c>,
///         <c>TimeSpan</c>, <c>Guid</c>, <c>Uri</c>, <c>Version</c>.</item>
///   <item><c>Nullable&lt;T&gt;</c> over the above primitives.</item>
///   <item>Enums (using <c>JsonMetadataServices.GetEnumConverter</c>).</item>
///   <item><c>List&lt;T&gt;</c>, <c>IList&lt;T&gt;</c>, <c>IReadOnlyList&lt;T&gt;</c>,
///         <c>ICollection&lt;T&gt;</c>, <c>IEnumerable&lt;T&gt;</c>,
///         <c>T[]</c> — element type must itself be supported.</item>
/// </list>
/// <para>
/// When a type is not supported (custom converters, polymorphism, dictionaries,
/// records with parameterised primary constructors, …), the generator simply
/// does not emit a factory for it. <c>AiJsonTypeInfoResolver.GetTypeInfo</c>
/// returns <c>null</c> and the parsers fall back to the reflection-based path
/// (annotated with <c>[RequiresUnreferencedCode]</c>).
/// </para>
/// </remarks>
[Generator]
public class AiJsonTypeInfoGenerator : IIncrementalGenerator
{
    /// <summary>
    /// Fully-qualified, global::-prefixed format that does NOT collapse to language
    /// aliases (so emitted code says <c>global::System.Int32</c>, never <c>global::int</c>;
    /// <c>global::System.Nullable&lt;global::System.Int32&gt;</c>, never <c>global::int?</c>).
    /// Using <c>SymbolDisplayFormat.FullyQualifiedFormat</c> directly is unsafe because
    /// it has <c>UseSpecialTypes</c> on, which produces invalid output when concatenated
    /// with <c>global::</c>.
    /// </summary>
    private static readonly SymbolDisplayFormat FqFormat =
        SymbolDisplayFormat.FullyQualifiedFormat
            .WithMiscellaneousOptions(
                SymbolDisplayFormat.FullyQualifiedFormat.MiscellaneousOptions
                & ~SymbolDisplayMiscellaneousOptions.UseSpecialTypes);

    private static string Fq(ITypeSymbol t) =>
        t.WithNullableAnnotation(NullableAnnotation.NotAnnotated).ToDisplayString(FqFormat);

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var responseTypes = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (node, _) => IsCandidatePromptClass(node),
                transform: static (ctx, _) => GetResponseType(ctx))
            .Where(static t => t is not null);

        var combined = context.CompilationProvider.Combine(responseTypes.Collect());
        context.RegisterSourceOutput(combined, static (spc, source) => Execute(source.Left, source.Right, spc));
    }

    private static bool IsCandidatePromptClass(SyntaxNode node)
    {
        return node is ClassDeclarationSyntax classDecl
               && classDecl.BaseList is { Types.Count: > 0 };
    }

    private static INamedTypeSymbol? GetResponseType(GeneratorSyntaxContext context)
    {
        var classDecl = (ClassDeclarationSyntax)context.Node;
        if (context.SemanticModel.GetDeclaredSymbol(classDecl) is not INamedTypeSymbol classSymbol)
            return null;
        if (classSymbol.IsAbstract)
            return null;

        var baseType = classSymbol.BaseType;
        while (baseType is not null)
        {
            if (baseType.IsGenericType
                && baseType.ConstructedFrom.Name == "PromptBase"
                && baseType.ContainingNamespace.ToDisplayString() == "Zonit.Extensions.Ai"
                && baseType.TypeArguments.Length == 1
                && baseType.TypeArguments[0] is INamedTypeSymbol responseType)
            {
                return responseType;
            }
            baseType = baseType.BaseType;
        }

        return null;
    }

    private static void Execute(
        Compilation compilation,
        ImmutableArray<INamedTypeSymbol?> rootTypes,
        SourceProductionContext context)
    {
        var roots = rootTypes
            .Where(t => t is not null)
            .Cast<INamedTypeSymbol>()
            .GroupBy(t => Fq(t), System.StringComparer.Ordinal)
            .Select(g => g.First())
            .ToList();

        if (roots.Count == 0)
            return;

        var collector = new TypeCollector();
        foreach (var root in roots)
        {
            collector.Visit(root);
        }

        if (collector.Pocos.Count == 0 && collector.Collections.Count == 0)
            return;

        context.AddSource("AiJsonTypeInfo.g.cs", Render(collector));
    }

    private static string Render(TypeCollector collector)
    {
        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated/>");
        sb.AppendLine("#nullable enable");
        sb.AppendLine("#pragma warning disable CS8019 // Unused using directive");
        sb.AppendLine();
        sb.AppendLine("using System;");
        sb.AppendLine("using System.Collections.Generic;");
        sb.AppendLine("using System.Runtime.CompilerServices;");
        sb.AppendLine("using System.Text.Json;");
        sb.AppendLine("using System.Text.Json.Serialization;");
        sb.AppendLine("using System.Text.Json.Serialization.Metadata;");
        sb.AppendLine("using Zonit.Extensions.Ai;");
        sb.AppendLine();
        sb.AppendLine("namespace Zonit.Extensions.Ai.Generated;");
        sb.AppendLine();
        sb.AppendLine("/// <summary>");
        sb.AppendLine("/// AOT-safe JsonTypeInfo factories registered with AiJsonTypeInfoResolver.");
        sb.AppendLine("/// </summary>");
        sb.AppendLine("internal static class __AiJsonTypeInfoBindings");
        sb.AppendLine("{");
        sb.AppendLine("    [ModuleInitializer]");
        sb.AppendLine("    internal static void Init()");
        sb.AppendLine("    {");

        foreach (var poco in collector.Pocos.Values.OrderBy(p => p.FullName, System.StringComparer.Ordinal))
        {
            sb.AppendLine($"        AiJsonTypeInfoResolver.Register(typeof({poco.FullName}), Build_{poco.MangledName});");
        }
        foreach (var coll in collector.Collections.Values.OrderBy(c => c.CollectionFullName, System.StringComparer.Ordinal))
        {
            sb.AppendLine($"        AiJsonTypeInfoResolver.Register(typeof({coll.CollectionFullName}), Build_{coll.MangledName});");
        }

        sb.AppendLine("    }");
        sb.AppendLine();

        // Per-POCO factory.
        foreach (var poco in collector.Pocos.Values.OrderBy(p => p.FullName, System.StringComparer.Ordinal))
        {
            EmitPocoFactory(sb, poco);
        }

        // Per-collection factory.
        foreach (var coll in collector.Collections.Values.OrderBy(c => c.CollectionFullName, System.StringComparer.Ordinal))
        {
            EmitCollectionFactory(sb, coll);
        }

        sb.AppendLine("}");
        return sb.ToString();
    }

    private static void EmitPocoFactory(StringBuilder sb, PocoTypeInfo poco)
    {
        var fq = poco.FullName; // already global::-qualified by FqFormat
        var hasAnyRequired = poco.Properties.Any(p => p.IsRequired);

        // Emit per-property UnsafeAccessor stubs at class scope for every init-only
        // setter we need to invoke (regular `obj.Prop = value` would produce CS8852).
        // The compiler generates a thunk that bypasses the init-only restriction;
        // this is fully supported under NativeAOT.
        foreach (var p in poco.Properties.Where(p => p.HasSetter && p.IsInitOnly))
        {
            sb.AppendLine($"    [global::System.Runtime.CompilerServices.UnsafeAccessor(global::System.Runtime.CompilerServices.UnsafeAccessorKind.Method, Name = \"set_{p.Name}\")]");
            sb.AppendLine($"    private static extern void __set_{poco.MangledName}__{p.Name}({fq} target, {p.TypeFullName} value);");
            sb.AppendLine();
        }

        sb.AppendLine($"    private static JsonTypeInfo Build_{poco.MangledName}(JsonSerializerOptions options)");
        sb.AppendLine("    {");
        sb.AppendLine($"        var info = JsonMetadataServices.CreateObjectInfo<{fq}>(options, new JsonObjectInfoValues<{fq}>");
        sb.AppendLine("        {");

        // ObjectCreator: only emit if the type has no required members; otherwise
        // CS9035 ("required members must be set in object initializer") fires. STJ
        // recognises a null ObjectCreator and uses the IsRequired metadata + setters
        // to populate the instance.
        if (hasAnyRequired)
        {
            sb.AppendLine("            ObjectCreator = null,");
        }
        else
        {
            sb.AppendLine($"            ObjectCreator = static () => new {fq}(),");
        }
        sb.AppendLine("            ObjectWithParameterizedConstructorCreator = null,");

        // .NET 10: PropertyMetadataInitializer is `Func<JsonSerializerContext, JsonPropertyInfo[]>`.
        // STJ invokes it as `propInitFunc(typeInfo.SerializerContext)` — and that argument is
        // **null** whenever the JsonTypeInfo was built through a custom IJsonTypeInfoResolver
        // (i.e. AiJsonTypeInfoResolver.Register), because no JsonSerializerContext is attached.
        // Dereferencing `ctx.Options` therefore throws NRE on the very first deserialise call.
        // Fix: capture the outer `options` parameter via closure (non-static lambda) and ignore
        // the argument entirely. `options` is always non-null here — the resolver passed it in.
        // We also need the statement-body form so we can apply post-construction tweaks
        // (e.g. setting JsonPropertyInfo.IsRequired = true for `required` members).
        sb.AppendLine("            PropertyMetadataInitializer = (_) =>");
        sb.AppendLine("            {");

        for (int i = 0; i < poco.Properties.Length; i++)
        {
            var p = poco.Properties[i];
            var propTypeFq = p.TypeFullName; // already global::-qualified
            sb.AppendLine($"                var __p_{i} = JsonMetadataServices.CreatePropertyInfo<{propTypeFq}>(options, new JsonPropertyInfoValues<{propTypeFq}>");
            sb.AppendLine("                {");
            sb.AppendLine("                    IsProperty = true,");
            sb.AppendLine("                    IsPublic = true,");
            sb.AppendLine("                    IsVirtual = false,");
            sb.AppendLine($"                    DeclaringType = typeof({fq}),");
            sb.AppendLine($"                    PropertyName = \"{p.Name}\",");
            sb.AppendLine($"                    Getter = static (obj) => (({fq})obj).{p.Name},");
            if (p.HasSetter)
            {
                if (p.IsInitOnly)
                {
                    sb.AppendLine($"                    Setter = static (obj, value) => __set_{poco.MangledName}__{p.Name}(({fq})obj, ({propTypeFq})value!),");
                }
                else
                {
                    sb.AppendLine($"                    Setter = static (obj, value) => (({fq})obj).{p.Name} = ({propTypeFq})value!,");
                }
            }
            else
            {
                sb.AppendLine("                    Setter = null,");
            }
            sb.AppendLine("                    Converter = null,");
            sb.AppendLine("                    IgnoreCondition = null,");
            sb.AppendLine("                    HasJsonInclude = false,");
            // .NET 10 breaking change: JsonPropertyInfoValues<T>.NumberHandling changed
            // from JsonNumberHandling? to non-nullable JsonNumberHandling. Omitting the
            // initialiser leaves the default value (JsonNumberHandling.Strict == 0),
            // which matches the previous `null` semantics (no per-property override).
            sb.AppendLine("                });");
            if (p.IsRequired)
            {
                sb.AppendLine($"                __p_{i}.IsRequired = true;");
            }
        }

        sb.Append("                return new JsonPropertyInfo[] { ");
        for (int i = 0; i < poco.Properties.Length; i++)
        {
            if (i > 0) sb.Append(", ");
            sb.Append($"__p_{i}");
        }
        sb.AppendLine(" };");
        sb.AppendLine("            },");

        sb.AppendLine("            ConstructorParameterMetadataInitializer = null,");
        // .NET 10 change: JsonObjectInfoValues<T>.NumberHandling is now a non-nullable
        // struct (default 0 = Strict), so we omit it instead of writing `= null`.
        sb.AppendLine("            SerializeHandler = null,");
        sb.AppendLine("        });");
        sb.AppendLine("        return info;");
        sb.AppendLine("    }");
        sb.AppendLine();
    }

    private static void EmitCollectionFactory(StringBuilder sb, CollectionTypeInfo coll)
    {
        var collFq = coll.CollectionFullName; // already global::-qualified
        var elemFq = coll.ElementFullName;    // already global::-qualified

        sb.AppendLine($"    private static JsonTypeInfo Build_{coll.MangledName}(JsonSerializerOptions options)");
        sb.AppendLine("    {");

        // .NET 10: JsonCollectionInfoValues<T>.NumberHandling is non-nullable
        // (was JsonNumberHandling? before). Omitting it leaves the default value
        // (Strict == 0), which matches the previous `null` semantics.
        if (coll.Kind == CollectionKind.Array)
        {
            sb.AppendLine($"        return JsonMetadataServices.CreateArrayInfo<{elemFq}>(options, new JsonCollectionInfoValues<{elemFq}[]>");
            sb.AppendLine("        {");
            sb.AppendLine("            ObjectCreator = null,");
            sb.AppendLine("            SerializeHandler = null,");
            sb.AppendLine("        });");
        }
        else
        {
            // List<T> / IList<T> / IReadOnlyList<T> / ICollection<T> / IEnumerable<T>
            sb.AppendLine($"        return JsonMetadataServices.CreateListInfo<{collFq}, {elemFq}>(options, new JsonCollectionInfoValues<{collFq}>");
            sb.AppendLine("        {");
            if (coll.Kind == CollectionKind.ConcreteList)
                sb.AppendLine($"            ObjectCreator = static () => new {collFq}(),");
            else
                sb.AppendLine($"            ObjectCreator = static () => new global::System.Collections.Generic.List<{elemFq}>(),");
            sb.AppendLine("            SerializeHandler = null,");
            sb.AppendLine("        });");
        }

        sb.AppendLine("    }");
        sb.AppendLine();
    }

    // ---------------------------------------------------------------------
    // Type collection (transitive walk over POCO graph)
    // ---------------------------------------------------------------------

    private sealed class TypeCollector
    {
        private readonly HashSet<string> _visited = new();
        public Dictionary<string, PocoTypeInfo> Pocos { get; } = new();
        public Dictionary<string, CollectionTypeInfo> Collections { get; } = new();

        public void Visit(ITypeSymbol type)
        {
            type = Unwrap(type);
            var fq = Fq(type);
            if (!_visited.Add(fq))
                return;

            // Built-ins / unsupported are simply skipped (resolver returns null at runtime).
            if (IsBuiltIn(type))
                return;
            if (type.TypeKind == TypeKind.Enum)
                return; // STJ has built-in enum handling via DefaultJsonTypeInfoResolver — not registered here.

            // Nullable<T>
            if (type is INamedTypeSymbol nullable
                && nullable.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T)
            {
                Visit(nullable.TypeArguments[0]);
                return;
            }

            // Arrays.
            if (type is IArrayTypeSymbol arr && arr.Rank == 1)
            {
                if (IsSupported(arr.ElementType))
                {
                    var element = Unwrap(arr.ElementType);
                    var elementFq = Fq(element);
                    var collFqName = $"{elementFq}[]";
                    if (!Collections.ContainsKey(collFqName))
                    {
                        Collections[collFqName] = new CollectionTypeInfo(
                            CollectionFullName: collFqName,
                            ElementFullName: elementFq,
                            MangledName: Mangle(collFqName),
                            Kind: CollectionKind.Array);
                    }
                    Visit(arr.ElementType);
                }
                return;
            }

            // Generic collections.
            if (type is INamedTypeSymbol named && named.IsGenericType)
            {
                var kind = ClassifyCollection(named);
                if (kind is not null)
                {
                    var element = named.TypeArguments[0];
                    if (IsSupported(element))
                    {
                        var collFq = Fq(named);
                        if (!Collections.ContainsKey(collFq))
                        {
                            Collections[collFq] = new CollectionTypeInfo(
                                CollectionFullName: collFq,
                                ElementFullName: Fq(element),
                                MangledName: Mangle(collFq),
                                Kind: kind.Value);
                        }
                        Visit(element);
                    }
                    return;
                }
            }

            // POCO?
            if (type is INamedTypeSymbol poco
                && (poco.TypeKind == TypeKind.Class || poco.TypeKind == TypeKind.Struct)
                && !poco.IsGenericType
                && poco.DeclaredAccessibility == Accessibility.Public
                && !poco.IsAbstract
                && HasPublicParameterlessConstructor(poco))
            {
                var pocoInfo = BuildPocoInfo(poco);
                if (pocoInfo is not null)
                {
                    Pocos[Fq(poco)] = pocoInfo;
                    foreach (var p in pocoInfo.Properties)
                    {
                        var propType = ResolveTypeSymbol(poco, p.PropertyTypeSymbol);
                        if (propType is not null)
                            Visit(propType);
                    }
                }
            }
        }

        private static ITypeSymbol Unwrap(ITypeSymbol t) => t.WithNullableAnnotation(NullableAnnotation.NotAnnotated);

        private static ITypeSymbol? ResolveTypeSymbol(ITypeSymbol _, ITypeSymbol candidate) => candidate;

        private static PocoTypeInfo? BuildPocoInfo(INamedTypeSymbol type)
        {
            var props = new List<PocoProperty>();
            var seen = new HashSet<string>(System.StringComparer.Ordinal);

            for (var current = (INamedTypeSymbol?)type; current is not null && current.SpecialType != SpecialType.System_Object; current = current.BaseType)
            {
                foreach (var member in current.GetMembers())
                {
                    if (member is not IPropertySymbol prop)
                        continue;
                    if (prop.IsStatic || prop.IsIndexer)
                        continue;
                    if (prop.DeclaredAccessibility != Accessibility.Public)
                        continue;
                    if (prop.GetMethod is null)
                        continue;
                    if (!seen.Add(prop.Name))
                        continue;
                    if (!IsSupported(prop.Type))
                        continue;

                    var setMethod = prop.SetMethod;
                    var hasSetter = setMethod is { DeclaredAccessibility: Accessibility.Public };
                    // `init`-only setters cannot be invoked through ordinary
                    // assignment outside an object initializer; we route them
                    // through UnsafeAccessor instead. `required` members force
                    // ObjectCreator = null and need IsRequired = true on the
                    // generated JsonPropertyInfo.
                    var isInitOnly = hasSetter && setMethod!.IsInitOnly;
                    var isRequired = prop.IsRequired;

                    props.Add(new PocoProperty(
                        Name: prop.Name,
                        TypeFullName: Fq(prop.Type),
                        PropertyTypeSymbol: prop.Type,
                        HasSetter: hasSetter,
                        IsInitOnly: isInitOnly,
                        IsRequired: isRequired));
                }
            }

            if (props.Count == 0)
                return null;

            var fullName = Fq(type);
            return new PocoTypeInfo(fullName, Mangle(fullName), props.ToImmutableArray());
        }

        private static bool HasPublicParameterlessConstructor(INamedTypeSymbol type)
        {
            // Structs always have a parameterless ctor; classes need an explicit one
            // unless none are declared (implicit default).
            if (type.TypeKind == TypeKind.Struct)
                return true;

            var ctors = type.InstanceConstructors;
            if (ctors.Length == 0)
                return true;
            return ctors.Any(c => c.Parameters.Length == 0 && c.DeclaredAccessibility == Accessibility.Public);
        }

        private static bool IsSupported(ITypeSymbol type)
        {
            type = type.WithNullableAnnotation(NullableAnnotation.NotAnnotated);
            if (IsBuiltIn(type))
                return true;
            if (type.TypeKind == TypeKind.Enum)
                return true;
            if (type is INamedTypeSymbol nullable && nullable.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T)
                return IsSupported(nullable.TypeArguments[0]);
            if (type is IArrayTypeSymbol arr && arr.Rank == 1)
                return IsSupported(arr.ElementType);
            if (type is INamedTypeSymbol named && named.IsGenericType && ClassifyCollection(named) is not null)
                return IsSupported(named.TypeArguments[0]);
            // POCO check (no generics, parameterless ctor)
            if (type is INamedTypeSymbol poco
                && (poco.TypeKind == TypeKind.Class || poco.TypeKind == TypeKind.Struct)
                && !poco.IsGenericType
                && poco.DeclaredAccessibility == Accessibility.Public
                && !poco.IsAbstract
                && HasPublicParameterlessConstructor(poco))
            {
                return true;
            }
            return false;
        }

        private static CollectionKind? ClassifyCollection(INamedTypeSymbol named)
        {
            var def = named.ConstructedFrom.ToDisplayString();
            return def switch
            {
                "System.Collections.Generic.List<T>" => CollectionKind.ConcreteList,
                "System.Collections.Generic.IList<T>" => CollectionKind.InterfaceList,
                "System.Collections.Generic.IReadOnlyList<T>" => CollectionKind.InterfaceList,
                "System.Collections.Generic.ICollection<T>" => CollectionKind.InterfaceList,
                "System.Collections.Generic.IEnumerable<T>" => CollectionKind.InterfaceList,
                _ => null,
            };
        }

        private static bool IsBuiltIn(ITypeSymbol type) => type.SpecialType switch
        {
            SpecialType.System_Boolean
            or SpecialType.System_Char
            or SpecialType.System_SByte
            or SpecialType.System_Byte
            or SpecialType.System_Int16
            or SpecialType.System_UInt16
            or SpecialType.System_Int32
            or SpecialType.System_UInt32
            or SpecialType.System_Int64
            or SpecialType.System_UInt64
            or SpecialType.System_Single
            or SpecialType.System_Double
            or SpecialType.System_Decimal
            or SpecialType.System_String
            or SpecialType.System_Object
            or SpecialType.System_DateTime => true,
            _ => type.ToDisplayString() switch
            {
                "System.DateTimeOffset" => true,
                "System.DateOnly" => true,
                "System.TimeOnly" => true,
                "System.TimeSpan" => true,
                "System.Guid" => true,
                "System.Uri" => true,
                "System.Version" => true,
                _ => false,
            },
        };

        private static string Mangle(string name)
        {
            var sb = new StringBuilder(name.Length);
            foreach (var ch in name)
            {
                if (char.IsLetterOrDigit(ch))
                    sb.Append(ch);
                else
                    sb.Append('_');
            }
            return sb.ToString();
        }
    }

    private enum CollectionKind { Array, ConcreteList, InterfaceList }

    private sealed record PocoTypeInfo(string FullName, string MangledName, ImmutableArray<PocoProperty> Properties);
    private sealed record PocoProperty(string Name, string TypeFullName, ITypeSymbol PropertyTypeSymbol, bool HasSetter, bool IsInitOnly, bool IsRequired);
    private sealed record CollectionTypeInfo(string CollectionFullName, string ElementFullName, string MangledName, CollectionKind Kind);
}
