namespace Suisharp;

/// <summary>よく使う子Component配置のCSSプリセット。</summary>
public static class Layouts
{
    /// <summary>子を上から下へ配置するLayoutを返します。</summary>
    public static Layout Default => Create("column");

    /// <summary>子を左から右へ配置するLayoutを返します。</summary>
    public static Layout Row => Create("row");

    private static Layout Create(string direction) => new()
    {
        ["display"] = "flex",
        ["flex-direction"] = direction,
        ["align-items"] = "flex-start",
        ["gap"] = "var(--suisharp-component-gap, 0)"
    };
}
