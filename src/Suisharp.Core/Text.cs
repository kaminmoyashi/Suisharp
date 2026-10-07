namespace Suisharp;

/// <summary>文字列を表示する部品。</summary>
public class Text(string value) : Component
{
    /// <summary>表示する文字列。変更後にUpdateを呼ぶと反映されます。</summary>
    public string Value { get; set; } = value;
}
