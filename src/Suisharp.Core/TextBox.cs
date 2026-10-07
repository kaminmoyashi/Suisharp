namespace Suisharp;

/// <summary>ブラウザから文字列を入力する一行の部品。</summary>
public class TextBox(string value = "", Action<string>? onInput = null) : Component
{
    /// <summary>現在の入力値。C#から変更した場合はUpdateで表示に反映します。</summary>
    public string Value { get; set; } = value;

    /// <summary>
    /// ブラウザ入力でValueを更新した後に呼ぶ通常のAction。
    /// C#からの代入では呼びません。他の部品の描画は明示的にUpdateしてください。
    /// 日本語などのIME入力は変換確定後に通知します。
    /// </summary>
    public Action<string>? OnInput { get; set; } = onInput;
}
