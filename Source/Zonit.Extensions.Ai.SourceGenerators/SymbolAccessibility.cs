using Microsoft.CodeAnalysis;

namespace Zonit.Extensions.Ai.SourceGenerators;

internal static class SymbolAccessibility
{
    /// <summary>
    /// Returns <c>true</c> when <paramref name="type"/> — and every type it is nested in —
    /// is at least <c>internal</c>, so a top-level generated member in the SAME assembly can
    /// name it (via <c>typeof(...)</c>, as a generic type argument, or in a cast) without
    /// triggering CS0122. Types that are <c>private</c>, <c>protected</c> or
    /// <c>private protected</c> at any nesting level are not reachable from the generated
    /// module-initializer code and must be skipped (the runtime then falls back to reflection).
    /// </summary>
    public static bool IsAccessibleToAssembly(INamedTypeSymbol type)
    {
        for (INamedTypeSymbol? t = type; t is not null; t = t.ContainingType)
        {
            switch (t.DeclaredAccessibility)
            {
                case Accessibility.Public:
                case Accessibility.Internal:
                case Accessibility.ProtectedOrInternal:
                    break; // reachable within the same assembly
                default:
                    return false; // Private / Protected / ProtectedAndInternal / NotApplicable
            }
        }
        return true;
    }
}
