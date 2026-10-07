namespace Suisharp;

/// <summary>よく使うStyleのCSSプリセット。</summary>
public static class Styles
{
    /// <summary>中央寄せの青いページと白いComponentカードを持つStyleを返します。</summary>
    public static Style Default => new()
    {
        ["background-color"] = "#d9edf9",
        ["color"] = "#263b4b",
        ["box-sizing"] = "border-box",
        ["width"] = "100%",
        ["max-width"] = "960px",
        ["min-height"] = "calc(100vh - 48px)",
        Margin = "24px auto",
        Padding = "24px",
        ["border"] = "none",
        ["border-radius"] = "0",
        ["box-shadow"] = "none",
        ["--suisharp-page-background"] = "#d9edf9",
        ["--suisharp-component-max-width"] = "920px",
        ["--suisharp-component-background"] = "#ffffff",
        ["--suisharp-component-border"] = "1px solid #d7e3eb",
        ["--suisharp-component-radius"] = "10px",
        ["--suisharp-component-padding"] = "16px",
        ["--suisharp-component-gap"] = "10px",
        ["--suisharp-component-shadow"] = "0 1px 3px rgb(32 64 84 / 8%)",
        ["--suisharp-control-background"] = "#ffffff",
        ["--suisharp-foreground"] = "#263b4b",
        ["--suisharp-control-border"] = "1px solid #b7c9d5",
        ["--suisharp-border-color"] = "#b7c9d5",
        ["--suisharp-control-radius"] = "7px",
        ["--suisharp-control-padding"] = "8px 13px",
        ["--suisharp-control-font"] = "inherit",
        ["--suisharp-control-hover"] = "#edf6fb",
        ["--suisharp-control-shadow"] = "0 1px 2px rgb(32 64 84 / 10%)",
        ["--suisharp-control-hover-shadow"] = "0 2px 5px rgb(32 64 84 / 16%)",
        ["--suisharp-control-transition"] = "background-color .14s ease, border-color .14s ease, box-shadow .14s ease",
        ["--suisharp-control-focus"] = "3px solid rgb(48 145 195 / 32%)",
        ["--suisharp-control-focus-offset"] = "2px",
        ["--suisharp-button-cursor"] = "pointer",
        ["--suisharp-textbox-width"] = "min(360px, 100%)"
    };

    /// <summary>見出しとして表示するStyleを返します。</summary>
    public static Style Heading => new()
    {
        ["display"] = "block",
        ["font-size"] = "1.75rem",
        ["font-weight"] = "700",
        ["line-height"] = "1.2",
        ["margin"] = "0"
    };

    /// <summary>小見出しとして表示するStyleを返します。</summary>
    public static Style Subheading => new()
    {
        ["display"] = "block",
        ["font-size"] = "1.2rem",
        ["font-weight"] = "600",
        ["line-height"] = "1.35",
        ["margin"] = "0"
    };

    /// <summary>補足情報として控えめに表示するStyleを返します。</summary>
    public static Style Muted => new()
    {
        ["color"] = "#647b8c",
        ["font-size"] = ".9rem"
    };
}
