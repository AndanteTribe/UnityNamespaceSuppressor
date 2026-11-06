using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Xunit;

namespace UnityNamespaceSuppressor.Tests;

public class UnityNamespaceSuppressorTests
{
    [Theory]
    [InlineData("UnityEngine.MonoBehaviour")]
    [InlineData("UnityEngine.ScriptableObject")]
    [InlineData("UnityEngine.StateMachineBehaviour")]
    [InlineData("UnityEngine.Playables.PlayableAsset")]
    [InlineData("UnityEngine.Rendering.RenderPipelineAsset")]
    [InlineData("UnityEditor.Editor")]
    [InlineData("UnityEditor.EditorWindow")]
    [InlineData("UnityEditor.ScriptableWizard")]
    [InlineData("UnityEditor.ScriptableSingleton<Player>")]
    [InlineData("UnityEditor.EditorTools.EditorTool")]
    [InlineData("UnityEditor.AssetImporters.ScriptedImporter")]
    public async Task UnityScript_SuppressesRealIde0161(string baseType)
    {
        var source = $"namespace Game {{ public class Player : {baseType} {{ }} }}";
        var baseline = Assert.Single(await SuppressorTest.RunAsync([source], enableSuppressor: false));
        Assert.Equal("IDE0161", baseline.Id);
        Assert.False(baseline.IsSuppressed);

        var diagnostic = Assert.Single(await SuppressorTest.RunAsync([source]));
        Assert.Equal("IDE0161", diagnostic.Id);
        Assert.True(diagnostic.IsSuppressed);
    }

    [Theory]
    [InlineData("public class Plain { }")]
    [InlineData("[System.Serializable] public class Data { public int Value; }")]
    [InlineData("public class Data { public UnityEngine.MonoBehaviour Value; }")]
    [InlineData("public class MonoBehaviour { } public class Player : MonoBehaviour { }")]
    [InlineData("public class ScriptableObject { } public class Settings : ScriptableObject { }")]
    [InlineData("public class ScriptedImporter { } public class Importer : ScriptedImporter { }")]
    [InlineData("public class Data { public UnityEditor.AssetImporters.ScriptedImporter Value; }")]
    [InlineData("public class Player : UnityEngine.Object { }")]
    [InlineData("public class Player : UnityEngine.Component { }")]
    [InlineData("public class Player : UnityEngine.Behaviour { }")]
    [InlineData("public class Importer : UnityEditor.AssetImporter { }")]
    [InlineData("public class Processor : UnityEditor.AssetPostprocessor { }")]
    [InlineData("public class Drawer : UnityEditor.PropertyDrawer { }")]
    [InlineData("public class Drawer : UnityEditor.DecoratorDrawer { }")]
    [InlineData("public class Settings : UnityEditor.SettingsProvider { }")]
    [InlineData("public class Wrapper { public class Nested : UnityEngine.MonoBehaviour { } }")]
    [InlineData("public class Wrapper { public class Nested : UnityEngine.ScriptableObject { } }")]
    [InlineData("public class Wrapper { public class Nested : UnityEditor.AssetImporters.ScriptedImporter { } }")]
    [InlineData("public interface IMarker { } public struct Value : IMarker { } public enum Mode { Default }")]
    [InlineData("public delegate void Callback();")]
    public async Task UnrelatedOrNestedType_DoesNotSuppress(string declaration)
    {
        var diagnostic = Assert.Single(await SuppressorTest.RunAsync([$"namespace Game {{ {declaration} }}"]));
        Assert.Equal("IDE0161", diagnostic.Id);
        Assert.False(diagnostic.IsSuppressed);
    }

    [Theory]
    [InlineData("using Script = UnityEngine.MonoBehaviour; namespace Game { public class Player : Script { } }")]
    [InlineData("using Engine = UnityEngine; namespace Game { public class Settings : Engine.ScriptableObject { } }")]
    [InlineData("using Importer = UnityEditor.AssetImporters.ScriptedImporter; namespace Game { public class CustomImporter : Importer { } }")]
    [InlineData("namespace Game { public class Player : global::UnityEngine.MonoBehaviour { } }")]
    [InlineData("namespace Game { public abstract class Base<T> : UnityEngine.MonoBehaviour { } public class Player : Base<int> { } }")]
    [InlineData("namespace Game { public class Plain { } internal abstract class Player : UnityEngine.MonoBehaviour { } }")]
    public async Task SymbolBasedInheritance_Suppresses(string source)
    {
        Assert.True(Assert.Single(await SuppressorTest.RunAsync([source])).IsSuppressed);
    }

    [Theory]
    [InlineData("UnityEngine.MonoBehaviour")]
    [InlineData("UnityEngine.ScriptableObject")]
    [InlineData("UnityEditor.AssetImporters.ScriptedImporter")]
    public async Task PartialDeclaration_UsesBaseTypeFromAnotherFile(string baseType)
    {
        var diagnostics = await SuppressorTest.RunAsync([
            "namespace Game { public partial class Player { } }",
            $"namespace Game {{ public partial class Player : {baseType} {{ }} }}"
        ]);
        Assert.Equal(2, diagnostics.Length);
        Assert.All(diagnostics, static diagnostic => Assert.True(diagnostic.IsSuppressed));
    }

    [Fact]
    public async Task OtherFilesAndOtherDeclarationsOfSameNamespace_AreIndependent()
    {
        var diagnostics = await SuppressorTest.RunAsync([
            "namespace Game { public class Player : UnityEngine.MonoBehaviour { } }",
            "namespace Game { public class Plain { } }"
        ]);
        Assert.Equal(2, diagnostics.Length);
        Assert.True(diagnostics.Single(static diagnostic => diagnostic.Location.SourceTree!.FilePath == "Test0.cs").IsSuppressed);
        Assert.False(diagnostics.Single(static diagnostic => diagnostic.Location.SourceTree!.FilePath == "Test1.cs").IsSuppressed);
    }

    [Theory]
    [InlineData("UnityEngine.MonoBehaviour")]
    [InlineData("UnityEngine.ScriptableObject")]
    [InlineData("UnityEditor.AssetImporters.ScriptedImporter")]
    public async Task BaseTypeInReferencedAssembly_Suppresses(string baseType)
    {
        var library = UnityReferences.Compile("Game.Framework", $$"""
            namespace Framework
            {
                public class Base<T> : {{baseType}} { }
                public class Intermediate : Base<int> { }
            }
            """, UnityReferences.Engine, UnityReferences.Editor);
        Assert.True(Assert.Single(await SuppressorTest.RunAsync([
            "namespace Game { public class Player : Framework.Intermediate { } }"
        ], UnityReferences.All.Add(library))).IsSuppressed);
    }

    [Theory]
    [InlineData("UnityEngine", "MonoBehaviour")]
    [InlineData("UnityEngine", "ScriptableObject")]
    [InlineData("UnityEditor.AssetImporters", "ScriptedImporter")]
    public async Task OnlyOneUnityBaseTypeAvailable_Suppresses(string namespaceName, string typeName)
    {
        var reference = UnityReferences.Compile("PartialUnity", $"namespace {namespaceName} {{ public class {typeName} {{ }} }}");
        var diagnostic = Assert.Single(await SuppressorTest.RunAsync([
            $"namespace Game {{ public class Player : {namespaceName}.{typeName} {{ }} }}"
        ], ImmutableArray.Create(UnityReferences.Runtime, reference)));
        Assert.True(diagnostic.IsSuppressed);
    }

    [Fact]
    public async Task NoUnityReferences_DoesNotSuppress()
    {
        var diagnostic = Assert.Single(await SuppressorTest.RunAsync([
            "namespace Game { public class MonoBehaviour { } public class Player : MonoBehaviour { } }"
        ], ImmutableArray.Create(UnityReferences.Runtime)));
        Assert.False(diagnostic.IsSuppressed);
    }

    [Theory]
    [InlineData("namespace Game; public class Player : UnityEngine.MonoBehaviour { }")]
    [InlineData("public class Player : UnityEngine.MonoBehaviour { }")]
    public async Task NoBlockNamespace_HasNoIde0161(string source)
    {
        Assert.Empty(await SuppressorTest.RunAsync([source]));
    }

    [Theory]
    [InlineData("public class Player : MissingBase { }")]
    [InlineData("public class A : B { } public class B : A { }")]
    public async Task UnresolvedOrCyclicInheritance_DoesNotCrashOrSuppress(string declarations)
    {
        var diagnostics = await SuppressorTest.RunAsync([$"namespace Game {{ {declarations} }}"], allowCompilerErrors: true);
        Assert.False(Assert.Single(diagnostics).IsSuppressed);
    }

    [Fact]
    public async Task SuppressionCanBeDisabled()
    {
        var diagnostics = await SuppressorTest.RunAsync([
            "namespace Game { public class Player : UnityEngine.MonoBehaviour { } }"
        ], disableSuppression: true);
        Assert.False(Assert.Single(diagnostics).IsSuppressed);
    }

    [Fact]
    public async Task SyntaxTreeOptionsWithoutSettings_UsesDefaultSuppression()
    {
        var diagnostics = await SuppressorTest.RunAsync([
            "namespace Game { public class Player : UnityEngine.MonoBehaviour { } }",
            "namespace Game { public class Plain { } }"
        ], useSyntaxTreeOptions: true);
        Assert.Equal(2, diagnostics.Length);
        Assert.True(diagnostics.Single(static diagnostic => diagnostic.Location.SourceTree!.FilePath == "Test0.cs").IsSuppressed);
        Assert.False(diagnostics.Single(static diagnostic => diagnostic.Location.SourceTree!.FilePath == "Test1.cs").IsSuppressed);
    }

    [Theory]
    [InlineData("none", false)]
    [InlineData("NONE", false)]
    [InlineData("none", true)]
    [InlineData("NONE", true)]
    public async Task EditorConfig_CanDisableSuppressionPerFile(string severity, bool useSyntaxTreeOptions)
    {
        var diagnostics = await SuppressorTest.RunAsync([
            "namespace Game { public class Player : UnityEngine.MonoBehaviour { } }",
            "namespace Game { public class Settings : UnityEngine.ScriptableObject { } }"
        ], suppressionSettings: new Dictionary<string, string> { ["Test0.cs"] = severity }, useSyntaxTreeOptions: useSyntaxTreeOptions);
        Assert.Equal(2, diagnostics.Length);
        Assert.False(diagnostics.Single(static diagnostic => diagnostic.Location.SourceTree!.FilePath == "Test0.cs").IsSuppressed);
        Assert.True(diagnostics.Single(static diagnostic => diagnostic.Location.SourceTree!.FilePath == "Test1.cs").IsSuppressed);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task GlobalConfig_CanBeOverriddenPerFile(bool useSyntaxTreeOptions)
    {
        var diagnostics = await SuppressorTest.RunAsync([
            "namespace Game { public class Player : UnityEngine.MonoBehaviour { } }",
            "namespace Game { public class Settings : UnityEngine.ScriptableObject { } }"
        ], suppressionSettings: new Dictionary<string, string>
        {
            [""] = "none",
            ["Test1.cs"] = "default"
        }, useSyntaxTreeOptions: useSyntaxTreeOptions);
        Assert.Equal(2, diagnostics.Length);
        Assert.False(diagnostics.Single(static diagnostic => diagnostic.Location.SourceTree!.FilePath == "Test0.cs").IsSuppressed);
        Assert.True(diagnostics.Single(static diagnostic => diagnostic.Location.SourceTree!.FilePath == "Test1.cs").IsSuppressed);
    }

    [Theory]
    [InlineData(ReportDiagnostic.Hidden)]
    [InlineData(ReportDiagnostic.Info)]
    [InlineData(ReportDiagnostic.Warn)]
    [InlineData(ReportDiagnostic.Error)]
    public async Task ConfiguredSeverity_IsSuppressed(ReportDiagnostic severity)
    {
        var diagnostic = Assert.Single(await SuppressorTest.RunAsync([
            "namespace Game { public class Player : UnityEngine.MonoBehaviour { } }"
        ], severity: severity));
        Assert.Equal(severity switch
        {
            ReportDiagnostic.Hidden => DiagnosticSeverity.Hidden,
            ReportDiagnostic.Info => DiagnosticSeverity.Info,
            ReportDiagnostic.Error => DiagnosticSeverity.Error,
            _ => DiagnosticSeverity.Warning
        }, diagnostic.Severity);
        Assert.True(diagnostic.IsSuppressed);
    }

    [Fact]
    public void SuppressionDescriptor_HasLocalizedJustification()
    {
        var rule = Assert.Single(new UnityNamespaceSuppressor().SupportedSuppressions);
        Assert.Equal("UNITYSUP001", rule.Id);
        Assert.Equal("IDE0161", rule.SuppressedDiagnosticId);
        Assert.StartsWith("Allow block-scoped namespaces", rule.Justification.ToString(CultureInfo.GetCultureInfo("en-US")));
        Assert.StartsWith("Unity の MonoScript", rule.Justification.ToString(CultureInfo.GetCultureInfo("ja-JP")));
    }
}