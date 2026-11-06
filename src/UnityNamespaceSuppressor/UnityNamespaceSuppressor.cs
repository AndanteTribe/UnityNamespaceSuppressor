using System;
using System.Collections.Immutable;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace UnityNamespaceSuppressor;

/// <summary>
/// Preserves block-scoped namespaces for types associated with Unity MonoScripts.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class UnityNamespaceSuppressor : DiagnosticSuppressor
{
    private static readonly SuppressionDescriptor s_rule = new(
        "UNITYSUP001",
        "IDE0161",
        new LocalizableResourceString("UnitySup001Justification", Resources.ResourceManager, typeof(Resources)));

    /// <inheritdoc />
    public override ImmutableArray<SuppressionDescriptor> SupportedSuppressions { get; } = ImmutableArray.Create(s_rule);

    /// <inheritdoc />
    public override void ReportSuppressions(SuppressionAnalysisContext context)
    {
        var monoBehaviour = context.Compilation.GetTypeByMetadataName("UnityEngine.MonoBehaviour");
        var scriptableObject = context.Compilation.GetTypeByMetadataName("UnityEngine.ScriptableObject");
        var scriptedImporter = context.Compilation.GetTypeByMetadataName("UnityEditor.AssetImporters.ScriptedImporter");
        if (monoBehaviour is null && scriptableObject is null && scriptedImporter is null)
        {
            return;
        }

        foreach (var diagnostic in context.ReportedDiagnostics)
        {
            context.CancellationToken.ThrowIfCancellationRequested();
            if (diagnostic.Id != s_rule.SuppressedDiagnosticId || diagnostic.Location.SourceTree is not { } tree)
            {
                continue;
            }

            if (IsSuppressionDisabled(context, tree))
            {
                continue;
            }

            var node = tree.GetRoot(context.CancellationToken).FindNode(diagnostic.Location.SourceSpan);
            var declaration = node.FirstAncestorOrSelf<NamespaceDeclarationSyntax>();
            if (declaration is null)
            {
                continue;
            }

            var model = context.GetSemanticModel(tree);
            // Only descend into namespaces. Nested types are not independent MonoScript types.
            foreach (var descendant in declaration.DescendantNodes(static node => node is NamespaceDeclarationSyntax))
            {
                context.CancellationToken.ThrowIfCancellationRequested();
                if (descendant is not ClassDeclarationSyntax classDeclaration
                    || model.GetDeclaredSymbol(classDeclaration, context.CancellationToken) is not INamedTypeSymbol symbol)
                {
                    continue;
                }

                if (IsUnityScript(symbol, monoBehaviour, scriptableObject, scriptedImporter, context.CancellationToken))
                {
                    context.ReportSuppression(Suppression.Create(s_rule, diagnostic));
                    break;
                }
            }
        }
    }

    private static bool IsSuppressionDisabled(SuppressionAnalysisContext context, SyntaxTree tree)
    {
        // NoWarn disables suppressors globally. Per-file diagnostic settings need explicit
        // handling: the compiler separates them from ordinary analyzer-config options.
        var treeOptions = context.Compilation.Options.SyntaxTreeOptionsProvider;
        if (treeOptions is not null
            && (treeOptions.TryGetDiagnosticValue(tree, s_rule.Id, context.CancellationToken, out var report)
                || treeOptions.TryGetGlobalDiagnosticValue(s_rule.Id, context.CancellationToken, out report)))
        {
            return report == ReportDiagnostic.Suppress;
        }

        // Other hosts can expose the raw .editorconfig values through analyzer options.
        var options = context.Options.AnalyzerConfigOptionsProvider;
        const string severityKey = "dotnet_diagnostic.UNITYSUP001.severity";
        return (options.GetOptions(tree).TryGetValue(severityKey, out var severity) ||
                options.GlobalOptions.TryGetValue(severityKey, out severity)) &&
               string.Equals(severity, "none", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsUnityScript(
        INamedTypeSymbol symbol,
        INamedTypeSymbol? monoBehaviour,
        INamedTypeSymbol? scriptableObject,
        INamedTypeSymbol? scriptedImporter,
        CancellationToken cancellationToken)
    {
        for (var baseType = symbol.BaseType; baseType is not null; baseType = baseType.BaseType)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (SymbolEqualityComparer.Default.Equals(baseType, monoBehaviour)
                || SymbolEqualityComparer.Default.Equals(baseType, scriptableObject)
                || SymbolEqualityComparer.Default.Equals(baseType, scriptedImporter))
            {
                return true;
            }
        }

        return false;
    }
}