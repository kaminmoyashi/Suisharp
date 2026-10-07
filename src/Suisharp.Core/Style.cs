namespace Suisharp;

/// <summary>CSS宣言を標準CSS文字列で指定する薄いラッパー。</summary>
public sealed class Style
{
    private readonly Dictionary<string, string> declarations = new(StringComparer.Ordinal);

    public string? this[string propertyName]
    {
        get => declarations.GetValueOrDefault(Normalize(propertyName));
        set
        {
            var name = Normalize(propertyName);
            if (string.IsNullOrWhiteSpace(value)) declarations.Remove(name);
            else declarations[name] = value;
        }
    }
    public string? Width { get => this["width"]; set => this["width"] = value; }
    public string? Height { get => this["height"]; set => this["height"] = value; }
    public string? Margin { get => this["margin"]; set => this["margin"] = value; }
    public string? Padding { get => this["padding"]; set => this["padding"] = value; }
    internal Dictionary<string, string> Snapshot() => new(declarations, StringComparer.Ordinal);
    private static string Normalize(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        name = name.Trim();
        return name.StartsWith("--", StringComparison.Ordinal) ? name : name.ToLowerInvariant();
    }
}
