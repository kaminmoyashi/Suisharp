namespace Suisharp;

/// <summary>クリック時に通常のC# Actionを呼ぶ部品。</summary>
public class Button(string text, Action? onClick = null) : Component
{
    /// <summary>ボタンの文字列。変更後にUpdateを呼ぶと反映されます。</summary>
    public string Text { get; set; } = text;
    /// <summary>クリック時の処理。描画は必要な場所で明示的にUpdateします。</summary>
    public Action? OnClick { get; set; } = onClick;
}
