using System.Collections.Immutable;
using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Xunit;
using Zonit.Extensions.Ai.Anthropic;
using Zonit.Extensions.Ai.SourceGenerators;

namespace Zonit.Extensions.Ai.Tests.Analyzers;

/// <summary>
/// ZAI001 turns a <c>[CapsReason]</c> violation into a compile error: a switch set to
/// <c>true</c> next to a <c>Reason</c> the API rejects in that mode. Compiles small
/// snippets against the real model assemblies and runs the shipped analyzer.
/// </summary>
public class CapsReasonAnalyzerTests
{
    [Theory]
    [InlineData("new Sonnet55 { ToolStepReasoning = true, Reason = Sonnet55.ReasonType.Max }")]
    [InlineData("new Sonnet55 { ToolStepReasoning = true, Reason = Sonnet55.ReasonType.Extra }")]
    [InlineData("new Sonnet55 { Reason = Sonnet55.ReasonType.Max, ToolStepReasoning = true }")]
    public async Task ToolStepAboveHigh_IsACompileError(string expression)
    {
        var diagnostics = await AnalyzeAsync(expression);

        diagnostics.Should().ContainSingle(d => d.Id == "ZAI001")
            .Which.Severity.Should().Be(DiagnosticSeverity.Error);
    }

    [Theory]
    [InlineData("new Sonnet55 { ToolStepReasoning = true, Reason = Sonnet55.ReasonType.High }")]
    [InlineData("new Sonnet55 { ToolStepReasoning = true, Reason = Sonnet55.ReasonType.Low }")]
    [InlineData("new Sonnet55 { ToolStepReasoning = true }")]
    [InlineData("new Sonnet55 { ToolStepReasoning = false, Reason = Sonnet55.ReasonType.Max }")]
    [InlineData("new Sonnet55 { Reason = Sonnet55.ReasonType.Max }")]
    public async Task AllowedCombinations_Compile(string expression)
        => (await AnalyzeAsync(expression)).Should().NotContain(d => d.Id == "ZAI001");

    private static async Task<ImmutableArray<Diagnostic>> AnalyzeAsync(string expression)
    {
        var source = $$"""
            using Zonit.Extensions.Ai.Anthropic;
            public static class Snippet
            {
                public static object Model = {{expression}};
            }
            """;

        var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator)
            .Select(path => MetadataReference.CreateFromFile(path))
            .Append(MetadataReference.CreateFromFile(typeof(Sonnet55).Assembly.Location))
            .Append(MetadataReference.CreateFromFile(typeof(CapsReasonAttribute).Assembly.Location));

        var compilation = CSharpCompilation.Create(
            "Snippet",
            [CSharpSyntaxTree.ParseText(source)],
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        compilation.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error)
            .Should().BeEmpty("the snippet itself must be valid C#");

        return await compilation
            .WithAnalyzers([new CapsReasonAnalyzer()])
            .GetAnalyzerDiagnosticsAsync();
    }
}
