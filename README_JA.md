[![dotnet-test](https://github.com/AndanteTribe/UnityNamespaceSuppressor/actions/workflows/dotnet-test.yml/badge.svg)](https://github.com/AndanteTribe/UnityNamespaceSuppressor/actions/workflows/dotnet-test.yml) [![nuget](https://img.shields.io/nuget/v/AndanteTribe.UnityNamespaceSuppressor.svg)](https://www.nuget.org/packages/AndanteTribe.UnityNamespaceSuppressor/) [![Releases](https://img.shields.io/github/release/AndanteTribe/UnityNamespaceSuppressor.svg)](https://github.com/AndanteTribe/UnityNamespaceSuppressor/releases) [![GitHub license](https://img.shields.io/github/license/AndanteTribe/UnityNamespaceSuppressor.svg)](./LICENSE)

[English](./README.md) | 日本語

## 概要

**UnityNamespaceSuppressor** は、通常の型にはファイルスコープ名前空間を推奨しつつ、**MonoBehaviour／ScriptableObject／ScriptedImporter 派生クラスにはブロック形式の名前空間を許可する** C# Roslyn `DiagnosticSuppressor` です。

**Unity 2022.3.22f1（Windows、`-langversion:10`、Editor パッチなし）** では、ファイルスコープ名前空間でソースファイルと型の `MonoScript` による紐付けが失われます。コードから生成できても、保存した Unity オブジェクトを復元できない場合があります。本ライブラリは **IDE0161: ファイル スコープの名前空間に変換** を抑制し、対象のブロック形式を維持できるようにします。

## インストール

### 要件

- **Roslyn 4.3 以降**と DiagnosticSuppressor に対応したコンパイラー／IDE。
- IDE0161 の対象となる、解析先プロジェクトの C# 10 以降の言語設定。
- 解析先プロジェクトから Unity アセンブリへの参照。

### NuGet

MSBuild／NuGet で管理するプロジェクトでは、次のコマンドでインストールします。

```sh
dotnet add package AndanteTribe.UnityNamespaceSuppressor
```

```powershell
Install-Package AndanteTribe.UnityNamespaceSuppressor
```

### Unity プロジェクト

1. [NuGetForUnity](https://github.com/GlitchEnzo/NuGetForUnity) をインストールします。
2. `NuGet > Manage NuGet Packages` から NuGet ウィンドウを開き、`AndanteTribe.UnityNamespaceSuppressor` パッケージを検索してインストールします。

## 使い方

`.editorconfig` でファイルスコープ名前空間を推奨します。

```editorconfig
[*.cs]
csharp_style_namespace_declarations = file_scoped:warning
dotnet_diagnostic.IDE0161.severity = warning
```

Unity スクリプトではブロック形式を使用します。

```csharp
// Player.cs
namespace Game
{
    public sealed class Player : UnityEngine.MonoBehaviour
    {
    }
}
```

この名前空間の IDE0161 は抑制されます。`ScriptableObject`／`UnityEditor.AssetImporters.ScriptedImporter` 派生クラスも、間接継承を含めて同様です。通常の型にはファイルスコープ名前空間の推奨が適用されます。

```csharp
// PlayerData.cs
namespace Game;

[System.Serializable]
public sealed class PlayerData
{
    public int Score;
}
```

## 抑制ルール

| ID | 抑制対象 | ルール |
| --- | --- | --- |
| [UNITYSUP001](docs/diagnostics/UNITYSUP001.ja.md) | IDE0161 | トップレベルの MonoBehaviour／ScriptableObject／ScriptedImporter 派生クラスを含むブロック形式の名前空間を許可します。 |

## 解析範囲

- partial 宣言を含め、直接・間接継承に対応します。
- アクセシビリティや abstract の有無を問わず、トップレベルのクラスを対象とします。入れ子のクラスは対象外です。
- 抑制単位は**クラスを含む名前空間宣言**です。通常の型が同じ宣言内に混在していても、その名前空間の診断は抑制されます。別ファイルや同名の別の名前空間宣言は個別に判定します。
- `EditorWindow`、`Editor`、`ScriptableSingleton<T>`、`EditorTool`、`ScriptableWizard`、`StateMachineBehaviour`、`PlayableAsset`、`RenderPipelineAsset` の派生クラスは、`ScriptableObject` の間接継承として対象になります。
- `UnityEditor.AssetImporters.ScriptedImporter` 派生クラスも明示的に対象とし、独自のインポーター基底クラスを経由する間接継承にも対応します。`UnityEngine.Object`／`UnityEditor.AssetImporter` 派生というだけでは対象になりません。
- 通常の Serializable クラス、Drawer、Postprocessor、Unity 型と単純名が同じだけのクラスでは抑制しません。

既存のファイルスコープ名前空間は書き換えません。IDE 独自のリファクタリングや Rider／ReSharper の独自検査は、IDE0161 の抑制対象には含まれません。

## 設定

本ライブラリの抑制を無効化し、通常どおり IDE0161 を表示する場合:

```editorconfig
[*.cs]
dotnet_diagnostic.UNITYSUP001.severity = none
```

## ライセンス

本プロジェクトは [MIT License](./LICENSE) の下で公開しています。
