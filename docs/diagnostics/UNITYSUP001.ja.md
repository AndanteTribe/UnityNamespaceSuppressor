# UNITYSUP001: Unity スクリプトのブロック形式の名前空間を維持する

[English](UNITYSUP001.md) | 日本語

| 項目 | 値 |
| --- | --- |
| 種類 | 診断の抑制 |
| 抑制対象 | IDE0161: ファイル スコープの名前空間に変換 |
| 既定で有効 | はい |
| コード修正 | なし |

## 抑制理由

> Unity の MonoScript が MonoBehaviour／ScriptableObject／ScriptedImporter 派生クラスを認識できるよう、ブロック形式の名前空間を許可します。

## 背景

ファイルスコープ名前空間では、C# のコンパイルや Reflection が正常でも、Unity のソースと型の紐付けに失敗する場合があります。[概要](../../README_JA.md#概要)を参照してください。

本ルールは、既存の IDE0161 が指すブロック形式の名前空間に、基底型を辿ると `UnityEngine.MonoBehaviour`、`UnityEngine.ScriptableObject`、`UnityEditor.AssetImporters.ScriptedImporter` のいずれかに到達するトップレベルのクラスがある場合、その診断を抑制します。

## 抑制する例

```csharp
using Script = UnityEngine.MonoBehaviour;

namespace Game
{
    public abstract class Actor : Script { }
    public sealed class Player : Actor { }
}
```

`namespace Game` の IDE0161 を抑制します。`Actor` が別のソースファイルや参照アセンブリに定義されていても同様です。partial クラスは統合された型シンボルから継承関係を判定するため、その宣言に基底型の記述がなくても対象になります。

## IDE0161 が残る例

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

Unity 型のフィールドがあるだけ、あるいはクラス名が `MonoBehaviour` であるだけでは対象になりません。Unity 型のシンボルに対する継承関係が必要です。

## 範囲と制限

- 診断の単位は個々のクラスではなく名前空間です。無関係な型が混在していても、対象クラスが 1 つあれば抑制します。
- 名前空間直下のクラスだけを判定します。子の名前空間は探索しますが、入れ子の型やそのメンバーには入りません。なお、Roslyn は通常、ファイルスコープへの変換が可能な箇所にだけ IDE0161 を報告します。
- 同名の別の名前空間宣言は個別に判定します。別ファイルに Unity スクリプトがあるだけで、無関係なファイルの診断を抑制することはありません。
- 非 public、abstract、ジェネリックのクラスも継承関係で判定します。Unity でのアタッチやシリアライズに必要なすべての条件を検証する機能ではありません。
- Unity の基底型を 3 種類とも取得できない場合は何もしません。取得できた各基底型の派生クラスは、それぞれ独立して判定します。
- `UnityEngine.Object` 派生全体ではなく、上記 3 種類の基底型が対象です。`ScriptedImporter` は明示的に対象とし、`EditorWindow` は `ScriptableObject` の間接継承として対象です。`AssetImporter` 派生というだけでは対象になりません。
- 既存のファイルスコープ名前空間、コンパイルエラー、無関係な診断を抑制・修正することはありません。
- ホスト側が IDE0161 と本 Suppressor の両方を実行する必要があります。IDE 独自の名前空間変換コマンドは制御しません。

## 設定

```editorconfig
[*.cs]
csharp_style_namespace_declarations = file_scoped:warning
dotnet_diagnostic.IDE0161.severity = warning
```

抑制ルール自体に warning／error の重大度はありません。抑制を無効化する場合:

```editorconfig
[*.cs]
dotnet_diagnostic.UNITYSUP001.severity = none
```

これにより対象の名前空間でも IDE0161 が表示されます。設定を削除すると抑制が再び有効になります。

[設定](../../README_JA.md#設定) / [抑制ルール一覧](../../README_JA.md#抑制ルール)
