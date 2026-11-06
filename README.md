[![dotnet-test](https://github.com/AndanteTribe/UnityNamespaceSuppressor/actions/workflows/dotnet-test.yml/badge.svg)](https://github.com/AndanteTribe/UnityNamespaceSuppressor/actions/workflows/dotnet-test.yml) [![nuget](https://img.shields.io/nuget/v/AndanteTribe.UnityNamespaceSuppressor.svg)](https://www.nuget.org/packages/AndanteTribe.UnityNamespaceSuppressor/) [![Releases](https://img.shields.io/github/release/AndanteTribe/UnityNamespaceSuppressor.svg)](https://github.com/AndanteTribe/UnityNamespaceSuppressor/releases) [![GitHub license](https://img.shields.io/github/license/AndanteTribe/UnityNamespaceSuppressor.svg)](./LICENSE)

English | [日本語](./README_JA.md)

## Overview

**UnityNamespaceSuppressor** is a C# Roslyn `DiagnosticSuppressor` that allows **block-scoped namespaces for MonoBehaviour, ScriptableObject, and ScriptedImporter subclasses**, while keeping the file-scoped namespace preference for ordinary types.

In the reported investigation on **Unity 2022.3.22f1 (Windows, `-langversion:10`, unpatched Editor)**, file-scoped namespaces broke the association between source files and types through `MonoScript`. Even when code could instantiate a type, saved Unity objects could fail to restore. This suppressor preserves appropriate block-scoped namespaces by suppressing **IDE0161: Convert to file-scoped namespace**.

## Installation

### Requirements

- A compiler or IDE supporting **Roslyn 4.3 or later** and diagnostic suppressors.
- C# 10 or later in the analyzed project for IDE0161 to apply.
- References to Unity's assemblies in the analyzed project.

### NuGet

For projects managed through MSBuild/NuGet:

```sh
dotnet add package AndanteTribe.UnityNamespaceSuppressor
```

```powershell
Install-Package AndanteTribe.UnityNamespaceSuppressor
```

### Unity projects

1. Install [NuGetForUnity](https://github.com/GlitchEnzo/NuGetForUnity).
2. Open `NuGet > Manage NuGet Packages` and search for and install the `AndanteTribe.UnityNamespaceSuppressor` package.

## Getting Started

Prefer file-scoped namespaces in `.editorconfig`:

```editorconfig
[*.cs]
csharp_style_namespace_declarations = file_scoped:warning
dotnet_diagnostic.IDE0161.severity = warning
```

Keep Unity scripts in block-scoped namespaces:

```csharp
// Player.cs
namespace Game
{
    public sealed class Player : UnityEngine.MonoBehaviour
    {
    }
}
```

IDE0161 on this namespace is suppressed. `ScriptableObject` and `UnityEditor.AssetImporters.ScriptedImporter` subclasses behave the same way, including indirect inheritance. Ordinary types retain the file-scoped namespace preference:

```csharp
// PlayerData.cs
namespace Game;

[System.Serializable]
public sealed class PlayerData
{
    public int Score;
}
```

## Suppressions

| ID | Suppressed diagnostic | Rule |
| --- | --- | --- |
| [UNITYSUP001](docs/diagnostics/UNITYSUP001.md) | IDE0161 | Allow block-scoped namespaces containing top-level MonoBehaviour, ScriptableObject, or ScriptedImporter subclasses. |

## Analysis Scope

- Direct and indirect inheritance, including partial declarations, is supported.
- Top-level classes are considered regardless of accessibility or whether they are abstract. Nested classes are excluded.
- Suppression applies to the **namespace declaration containing the class**. If it also contains ordinary types, the namespace diagnostic is still suppressed. Separate files and other declarations of the same namespace are evaluated independently.
- `EditorWindow`, `Editor`, `ScriptableSingleton<T>`, `EditorTool`, `ScriptableWizard`, `StateMachineBehaviour`, `PlayableAsset`, and `RenderPipelineAsset` subclasses are covered through their `ScriptableObject` ancestry.
- `UnityEditor.AssetImporters.ScriptedImporter` subclasses are explicitly covered, including indirect inheritance through custom importer base classes. `UnityEngine.Object` or `UnityEditor.AssetImporter` ancestry alone is insufficient.
- Ordinary serializable classes, drawers, postprocessors, and similarly named non-Unity classes do not trigger suppression.

Existing file-scoped namespaces are not rewritten. IDE-specific refactorings and Rider/ReSharper inspections are outside the scope of IDE0161 suppression.

## Configuration

To disable this suppression and restore normal IDE0161 reporting:

```editorconfig
[*.cs]
dotnet_diagnostic.UNITYSUP001.severity = none
```

## License

This project is licensed under the [MIT License](./LICENSE).
