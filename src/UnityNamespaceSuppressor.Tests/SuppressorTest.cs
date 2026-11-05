using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Xunit;

namespace UnityNamespaceSuppressor.Tests;

internal static class SuppressorTest
{
    private static readonly Lazy<DiagnosticAnalyzer> s_namespaceAnalyzer = new(LoadNamespaceAnalyzer);

    public static async Task<ImmutableArray<Diagnostic>> RunAsync(string[] sources,
        ImmutableArray<MetadataReference> references = default,
        bool enableSuppressor = true,
        ReportDiagnostic severity = ReportDiagnostic.Warn,
        bool disableSuppression = false,
        bool allowCompilerErrors = false,
        IReadOnlyDictionary<string, string>? suppressionSettings = null,
        bool useSyntaxTreeOptions = false)
    {
        var trees = sources.Select((source, index) => CSharpSyntaxTree.ParseText(source,
            new CSharpParseOptions(LanguageVersion.CSharp10), $"Test{index}.cs"));
        var options = new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
            .WithSpecificDiagnosticOptions(new Dictionary<string, ReportDiagnostic>
            {
                ["IDE0161"] = severity,
                ["UNITYSUP001"] = disableSuppression ? ReportDiagnostic.Suppress : ReportDiagnostic.Default
            });
        if (useSyntaxTreeOptions)
        {
            options = options.WithSyntaxTreeOptionsProvider(new SuppressionOptionsProvider(suppressionSettings!));
        }
        var compilation = CSharpCompilation.Create("Tests", trees,
            references.IsDefault ? UnityReferences.All : references, options);
        if (!allowCompilerErrors)
        {
            Assert.Empty(compilation.GetDiagnostics().Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error));
        }

        var analyzers = ImmutableArray.Create(s_namespaceAnalyzer.Value);
        if (enableSuppressor)
        {
            analyzers = analyzers.Add(new UnityNamespaceSuppressor());
        }

        var analyzerOptions = new AnalyzerOptions(ImmutableArray<AdditionalText>.Empty,
            new FileScopedOptionsProvider(useSyntaxTreeOptions ? null : suppressionSettings));
        var diagnostics = await compilation.WithAnalyzers(analyzers,
            new CompilationWithAnalyzersOptions(analyzerOptions, null,
                concurrentAnalysis: true, logAnalyzerExecutionTime: false, reportSuppressedDiagnostics: true))
            .GetAnalyzerDiagnosticsAsync();
        Assert.DoesNotContain(diagnostics, static diagnostic => diagnostic.Id == "AD0001");
        return diagnostics;
    }

    private static DiagnosticAnalyzer LoadNamespaceAnalyzer()
    {
        Assembly.LoadFrom(Path.Combine(AppContext.BaseDirectory, "Microsoft.CodeAnalysis.CodeStyle.dll"));
        var assembly = Assembly.LoadFrom(Path.Combine(AppContext.BaseDirectory, "Microsoft.CodeAnalysis.CSharp.CodeStyle.dll"));
        var type = assembly.GetTypes().Single(static type => type.Name == "ConvertToFileScopedNamespaceDiagnosticAnalyzer");
        return (DiagnosticAnalyzer)Activator.CreateInstance(type, nonPublic: true)!;
    }

    private sealed class FileScopedOptionsProvider(IReadOnlyDictionary<string, string>? suppressionSettings) : AnalyzerConfigOptionsProvider
    {
        private static readonly AnalyzerConfigOptions s_options = new FileScopedOptions();

        public override AnalyzerConfigOptions GlobalOptions => ForPath("");
        public override AnalyzerConfigOptions GetOptions(SyntaxTree tree) => ForPath(tree.FilePath);
        public override AnalyzerConfigOptions GetOptions(AdditionalText textFile) => s_options;

        private AnalyzerConfigOptions ForPath(string path) => suppressionSettings is not null &&
            suppressionSettings.TryGetValue(path, out var severity) ? new FileScopedOptions(severity) : s_options;
    }

    private sealed class SuppressionOptionsProvider(IReadOnlyDictionary<string, string> settings) : SyntaxTreeOptionsProvider
    {
        public override GeneratedKind IsGenerated(SyntaxTree tree, CancellationToken cancellationToken) => GeneratedKind.Unknown;

        public override bool TryGetDiagnosticValue(SyntaxTree tree, string diagnosticId, CancellationToken cancellationToken,
            out ReportDiagnostic severity) => TryGetValue(tree.FilePath, diagnosticId, out severity);

        public override bool TryGetGlobalDiagnosticValue(string diagnosticId, CancellationToken cancellationToken,
            out ReportDiagnostic severity) => TryGetValue("", diagnosticId, out severity);

        private bool TryGetValue(string path, string diagnosticId, out ReportDiagnostic severity)
        {
            severity = ReportDiagnostic.Default;
            if (diagnosticId != "UNITYSUP001" || !settings.TryGetValue(path, out var value))
            {
                return false;
            }

            severity = string.Equals(value, "none", StringComparison.OrdinalIgnoreCase) ? ReportDiagnostic.Suppress : ReportDiagnostic.Default;
            return true;
        }
    }

    private sealed class FileScopedOptions(string? suppressionSeverity = null) : AnalyzerConfigOptions
    {
        public override bool TryGetValue(string key, out string value)
        {
            value = key switch
            {
                "csharp_style_namespace_declarations" => "file_scoped:warning",
                "dotnet_diagnostic.IDE0161.severity" => "warning",
                "dotnet_diagnostic.UNITYSUP001.severity" => suppressionSeverity ?? "",
                _ => ""
            };
            return value.Length != 0;
        }
    }
}