namespace Suisharp;

/// <summary>このComponentが持つ子Componentの配置を指定する薄いCSSラッパー。</summary>
public sealed class Layout
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

    public string? Display { get => this["display"]; set => this["display"] = value; }
    public string? FlexDirection { get => this["flex-direction"]; set => this["flex-direction"] = value; }
    public string? Gap { get => this["gap"]; set => this["gap"] = value; }
    internal Dictionary<string, string> Snapshot() => new(declarations, StringComparer.Ordinal);

    private static string Normalize(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        name = name.Trim();
        return name.StartsWith("--", StringComparison.Ordinal) ? name : name.ToLowerInvariant();
    }
}
