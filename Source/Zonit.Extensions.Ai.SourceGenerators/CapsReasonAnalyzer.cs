using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Zonit.Extensions.Ai.SourceGenerators;

/// <summary>
/// ZAI001 — a model switch marked <c>[CapsReason(max)]</c> is set to <c>true</c> in an
/// object initializer together with a <c>Reason</c> level above <c>max</c>. The provider
/// would answer that request with HTTP 400, so it is reported as a compile error.
/// </summary>
/// <remarks>
/// Only constant values in the same initializer are visible here; anything set at
/// runtime is caught by the provider's own guard before the request is sent.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class CapsReasonAnalyzer : DiagnosticAnalyzer
{
    private const string AttributeName = "Zonit.Extensions.Ai.CapsReasonAttribute";

    /// <summary>ZAI001 descriptor.</summary>
    public static readonly DiagnosticDescriptor Rule = new(
        id: "ZAI001",
        title: "Reason level not allowed in this mode",
        messageFormat: "'{0} = true' limits Reason to {1} on {2}; '{3}' would be rejected by the API with HTTP 400",
        category: "Zonit.Extensions.Ai",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "A model switch marked [CapsReason] caps the Reason level while it is on. Lower Reason or turn the switch off.");

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterOperationAction(AnalyzeInitializer, OperationKind.ObjectOrCollectionInitializer);
    }

    private static void AnalyzeInitializer(OperationAnalysisContext context)
    {
        var initializer = (IObjectOrCollectionInitializerOperation)context.Operation;

        IPropertySymbol? cappingSwitch = null;
        int cap = int.MaxValue;
        ISimpleAssignmentOperation? reasonAssignment = null;
        int reasonLevel = 0;

        foreach (var member in initializer.Initializers)
        {
            if (member is not ISimpleAssignmentOperation { Target: IPropertyReferenceOperation target } assignment)
                continue;

            var property = target.Property;

            if (property.Name == "Reason")
            {
                if (ConstantInt(assignment.Value) is { } level)
                {
                    reasonAssignment = assignment;
                    reasonLevel = level;
                }
                continue;
            }

            if (property.Type.SpecialType != SpecialType.System_Boolean
                || ConstantBool(assignment.Value) != true)
                continue;

            foreach (var attribute in property.GetAttributes())
            {
                if (attribute.AttributeClass?.ToDisplayString() != AttributeName
                    || attribute.ConstructorArguments.Length != 1
                    || attribute.ConstructorArguments[0].Value is not int max)
                    continue;

                cappingSwitch = property;
                cap = max;
            }
        }

        if (cappingSwitch is null || reasonAssignment is null || reasonLevel <= cap)
            return;

        context.ReportDiagnostic(Diagnostic.Create(
            Rule,
            reasonAssignment.Syntax.GetLocation(),
            cappingSwitch.Name,
            EffortName(cap),
            initializer.Type?.Name ?? "this model",
            reasonAssignment.Value.Syntax.ToString()));
    }

    private static IOperation Unwrap(IOperation value)
    {
        while (value is IConversionOperation conversion)
            value = conversion.Operand;
        return value;
    }

    private static int? ConstantInt(IOperation value)
    {
        var constant = Unwrap(value).ConstantValue;
        if (!constant.HasValue || constant.Value is null)
            return null;
        return constant.Value switch
        {
            int i => i,
            byte b => b,
            short s => s,
            long l => (int)l,
            _ => null,
        };
    }

    private static bool? ConstantBool(IOperation value)
        => Unwrap(value).ConstantValue is { HasValue: true, Value: bool b } ? b : null;

    // Mirrors Zonit.Extensions.Ai.ReasoningEffort (analyzers cannot reference the runtime assembly).
    private static string EffortName(int value) => value switch
    {
        0 => "None",
        1 => "Low",
        2 => "Medium",
        3 => "High",
        4 => "Extra (xhigh)",
        5 => "Max",
        _ => value.ToString(),
    };
}
