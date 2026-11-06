using System;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace UnityNamespaceSuppressor.Tests;

/// <summary>
/// Metadata-only stand-ins: tests exercise Roslyn symbol identity, not Unity runtime behavior.
/// </summary>
internal static class UnityReferences
{
    public static readonly MetadataReference Runtime = MetadataReference.CreateFromFile(typeof(object).Assembly.Location);

    public static readonly MetadataReference Engine = Compile("UnityEngine.CoreModule", """
        namespace UnityEngine
        {
            public class Object { }
            public class Component : Object { }
            public class Behaviour : Component { }
            public class MonoBehaviour : Behaviour { }
            public class ScriptableObject : Object { }
            public class StateMachineBehaviour : ScriptableObject { }
        }
        namespace UnityEngine.Playables
        {
            public abstract class PlayableAsset : UnityEngine.ScriptableObject { }
        }
        namespace UnityEngine.Rendering
        {
            public abstract class RenderPipelineAsset : UnityEngine.ScriptableObject { }
        }
        """);

    public static readonly MetadataReference Editor = Compile("UnityEditor", """
        namespace UnityEditor
        {
            public class Editor : UnityEngine.ScriptableObject { }
            public class EditorWindow : UnityEngine.ScriptableObject { }
            public class ScriptableWizard : EditorWindow { }
            public class ScriptableSingleton<T> : UnityEngine.ScriptableObject where T : UnityEngine.ScriptableObject { }
            public class AssetImporter : UnityEngine.Object { }
            public class AssetPostprocessor { }
            public class PropertyDrawer { }
            public class DecoratorDrawer { }
            public class SettingsProvider { }
        }
        namespace UnityEditor.EditorTools
        {
            public abstract class EditorTool : UnityEngine.ScriptableObject { }
        }
        namespace UnityEditor.AssetImporters
        {
            public abstract class ScriptedImporter : UnityEditor.AssetImporter { }
        }
        """, Engine);

    public static ImmutableArray<MetadataReference> All { get; } = ImmutableArray.Create(Runtime, Engine, Editor);

    public static MetadataReference Compile(string assemblyName, string source, params MetadataReference[] references)
    {
        var compilation = CSharpCompilation.Create(assemblyName,
            [CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(LanguageVersion.CSharp10))],
            new[] { Runtime }.Concat(references),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        using var stream = new MemoryStream();
        var result = compilation.Emit(stream);
        Assert.True(result.Success, string.Join(Environment.NewLine, result.Diagnostics));
        return MetadataReference.CreateFromImage(stream.ToArray());
    }
}