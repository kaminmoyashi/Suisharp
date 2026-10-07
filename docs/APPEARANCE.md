# Suisharp 外観仕様

この文書は実装済みの `IsVisible`、`CssClass`、`Style`、`Layout` の契約です。Suisharpは描画に必要な値だけをブラウザへ渡し、クラスのスタイルやレスポンシブ指定は通常のCSSファイルで管理します。

## Component API

- `bool IsVisible { get; set; }` は既定で `true`。設定後、そのComponentの `Update()` が反映された時点でDOMの表示が変わります。DOM要素は保持され、子の値やローカルな `IsVisible` は書き換えません。
- 親が非表示ならその子孫も表示されません。子自身を非表示にした状態で親を再表示すると、その子は非表示のままです。非表示はRemoveやDisposeではなく、タイマーやイベント購読も止めません。
- `string? CssClass { get; set; }` は空白区切りのクラスを受け取り、重複を除去します。`null`、空白のみはクラスなしです。`suisharp-*` はRenderer予約名としてアプリ指定から除外されます。
- `Style Style { get; set; }` はそのComponent自身に適用する標準CSS宣言の薄いラッパーです。`Style["font-size"]` のような文字列インデクサと `Width`、`Height`、`Margin`、`Padding` などの短縮プロパティを持ちます。任意CSSの値に独自の単位変換や型を設けません。
- `Styles.Default` は青いページ、白いComponentカード、控えめなボタン装飾をCSS値で設定した新しいStyleを返します。これまでの標準デザインを維持します。
- `Styles.Heading`、`Styles.Subheading`、`Styles.Muted` は、それぞれ見出し、小見出し、補足情報としてそのComponent自身を見せるCSS宣言を持つ新しいStyleを返します。文字サイズや色はCSSで上書きできます。
- `Layout Layout { get; set; }` は子Componentの配置を指定します。既定値は `Layouts.Default` で、子を縦に並べます。`Layouts.Row` は子を横に並べます。
- `Layout` も標準CSS宣言の薄いラッパーです。`Layout["align-items"]` のような文字列インデクサと `Display`、`FlexDirection`、`Gap` の短縮プロパティで子の配置を調整します。
- `Style` と `Layout` のどちらも、`null`、空文字列、空白のみを設定するとその宣言が削除されます。プロパティ名は通常名なら前後空白を除いて小文字化し、`--`で始まるカスタムプロパティなら大小文字を維持します。空の名前は `ArgumentException` です。
- StyleとLayoutのプリセットはいずれも毎回新しいインスタンスを返し、変更を他のComponentと共有しません。

```csharp
using Suisharp;

public class MyPage : Component
{
    public MyPage()
    {
        Style = Styles.Default;
        Add(new Text("こんにちは") { Style = Styles.Heading },
            new Text("小見出し") { Style = Styles.Subheading },
            new Text("補足情報") { Style = Styles.Muted },
            new Button("押す"));
    }
}

var toolbar = new Component { Layout = Layouts.Row };
toolbar.Layout.Gap = "1rem";
toolbar.Add(new Text("画面名"), new Button("操作"));
```

## UpdateとDOM

`IsVisible`、`CssClass`、`Style`、`Layout` の変更は、それだけでは描画されません。通常どおり `Update()` を呼んで反映します。既定の `Update()` は非表示のComponentでも子のvirtual `Update()` を呼びます。overrideが `base.Update()` を呼ばなければ、自身の属性を送信しません。

非表示にした要素には `hidden` を設定し、Rendererが `display:none!important` を適用します。再表示ではその予約宣言を解除し、最新の反映済みStyleを適用します。アプリが指定したStyleのデータは変更しません。フォーカスが隠れる要素にあればblurし、隠れたDOMへの古いクリックと入力はブラウザとサーバーの両方で抑制します。

ブラウザのCSSだけで非表示にした要素をSuisharpは追跡しません。作者CSSの `!important`、拡張機能やアプリスクリプトによる予約DOMの直接変更、非表示のdisplayアニメーションは保証対象外です。IME変換を強制確定せず、非表示後に届いた入力は受理しません。

## CSSとレイアウト

設計の分担は「Styleは自分、Layoutは子」です。Styleの宣言は対象のDOM要素自身に反映し、Layoutの宣言は子Componentを配置するCSSとして反映します。Componentの深さだけで枠やインデントを付けません。`Layouts.Default` は縦方向、`Layouts.Row` は横方向です。いずれも `Update()` 後に反映します。

StyleとLayoutはいずれも通常のCSS宣言です。`@media`、疑似クラス、細かな見た目はアプリ側CSSファイルに記述できます。デモは `Styles.Default` を使いますが、その指定を外せばブラウザ標準に近い外観になります。CSSファイルを使う場合は、ASP.NET Core標準の静的ファイル配信をアプリ側で設定します。Suisharp独自のCSSローダーはありません。

デモではComponentにCSSクラスを指定し、ASP.NET Coreの静的ファイル配信でCSSを読み込んでいます。

```csharp
panel.CssClass = "demo-component-frame";
```

```css
.demo-component-frame {
  border: 1px solid #c6dfed;
  border-radius: 14px;
  padding: 18px;
  background: rgb(255 255 255 / 78%);
  box-shadow: 0 8px 24px rgb(42 103 140 / 10%);
}
```

細かな見た目は、標準CSSプロパティ名をStyleの文字列インデクサに指定します。CssClassでアプリ側のCSSルールを適用する方法も使えます。

```csharp
using Suisharp;

var caption = new Text("補足") { Style = Styles.Muted };
caption.Style["font-size"] = "0.85rem";
caption.CssClass = "field-caption";
```

```css
.field-caption { letter-spacing: .02em; }
```

```csharp
using Suisharp;

class Header : Component
{
    public Header()
    {
        Layout = Layouts.Row;
        Layout.Gap = "1rem";
        Layout["align-items"] = "center";
        Add(new Text("Suisharp") { Style = Styles.Heading }, new Button("Menu"));
    }
}

panel.IsVisible = false;
panel.Update();
```

```css
.app-header { gap: 1rem; }
.app-header button:hover { background: #e8f1fa; }
@media (max-width: 600px) { .app-header { flex-direction: column; } }
```

## APIを選んだ理由

Style/Layoutの値型とプリセットを分け、`Style`・`Layout`のプロパティ型と `Styles`・`Layouts` の静的プリセットを使います。`using Suisharp;` を宣言すれば `Styles.Default`、`Layouts.Row` のように短く書けます。CSS値は標準文字列で指定し、独自のレイアウトDSLや状態機構は追加しません。
