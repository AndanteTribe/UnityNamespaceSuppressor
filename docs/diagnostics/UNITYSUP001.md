# UNITYSUP001: Preserve block-scoped namespaces for Unity scripts

English | [日本語](UNITYSUP001.ja.md)

| Property | Value |
| --- | --- |
| Kind | Diagnostic suppression |
| Suppressed diagnostic | IDE0161: Convert to file-scoped namespace |
| Enabled by default | Yes |
| Code fix | None |

## Justification

> Allow block-scoped namespaces so Unity MonoScript can recognize MonoBehaviour, ScriptableObject, and ScriptedImporter subclasses.

## Cause

Unity's source-to-type association can fail for file-scoped namespaces even though C# compilation and reflection work. See the [overview](../../README.md#overview).

This rule suppresses an existing IDE0161 diagnostic when its block-scoped namespace contains a top-level class whose base-type chain includes `UnityEngine.MonoBehaviour`, `UnityEngine.ScriptableObject`, or `UnityEditor.AssetImporters.ScriptedImporter`.

## Suppressed example

```csharp
using Script = UnityEngine.MonoBehaviour;

namespace Game
{
    public abstract class Actor : Script { }
    public sealed class Player : Actor { }
}
```

IDE0161 is suppressed on `namespace Game`. The same applies when `Actor` is defined in another source file or referenced assembly. If the class is partial, the base type is resolved from the combined type symbol, so a part without its own base list also qualifies.

## Example that still reports IDE0161

```csharp
namespace Game
{
    [System.Serializable]
    public sealed class PlayerData
    {
        public int Score;
    }
}
```

Having a Unity-typed field or the name `MonoBehaviour` does not qualify a class. The inheritance relationship must resolve to the Unity type symbol.

## Scope and limitations

- The diagnostic belongs to the namespace, not to an individual class. One matching class is sufficient, even if unrelated types are also present.
- Only classes directly within namespace declarations are considered. Namespace descendants can be examined; nested types and their members are not traversed. Roslyn itself normally offers IDE0161 only where conversion to a file-scoped namespace is legal.
- Other declarations of the same namespace are independent. A Unity script in another file does not suppress IDE0161 in an unrelated file.
- Non-public, abstract, and generic classes qualify by ancestry. The rule does not attempt to validate every Unity requirement for attaching or serializing a particular class.
- If none of the three Unity base types is available, the suppressor does nothing. Each available base type's subclasses are recognized independently.
- The rule targets the three base types above, not every `UnityEngine.Object` subclass. `ScriptedImporter` is explicitly included; `EditorWindow` is included through `ScriptableObject`. `AssetImporter` ancestry alone does not qualify.
- Existing file-scoped namespaces, compiler errors, and unrelated diagnostics are not suppressed or corrected.
- The host must run both IDE0161 and this suppressor. IDE-specific namespace conversion commands are not controlled by it.

## Configuration

```editorconfig
[*.cs]
csharp_style_namespace_declarations = file_scoped:warning
dotnet_diagnostic.IDE0161.severity = warning
```

Suppressions do not have a warning/error severity of their own. To turn this suppression off:

```editorconfig
[*.cs]
dotnet_diagnostic.UNITYSUP001.severity = none
```

This restores IDE0161 reporting for matching namespaces. Remove the setting to re-enable suppression.

[Configuration](../../README.md#configuration) / [Suppression index](../../README.md#suppressions)
