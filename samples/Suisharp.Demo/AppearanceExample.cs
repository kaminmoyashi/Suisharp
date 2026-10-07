using Suisharp;

namespace Suisharp.Demo;

public class AppearanceExample : Component
{
    private readonly Component panel = new();
    private readonly Text details = new("子の表示も独立して切り替えられます") { Style = Styles.Muted };
    private readonly TextBox input = new("非表示にしても残る入力");
    private readonly Text status = new("外観の変更はUpdateで反映します") { Style = Styles.Muted };
    private bool shaded;
    private bool spaced;

    public AppearanceExample()
    {
        var controls = new Component();
        controls.Add(
            new Button("親パネルの表示切替", () => { panel.IsVisible = !panel.IsVisible; panel.Update(); }),
            new Button("子の表示切替", () => { details.IsVisible = !details.IsVisible; details.Update(); }),
            new Button("背景色を変更・解除", () =>
            {
                shaded = !shaded;
                panel.Style["background-color"] = shaded ? "#f2f2f2" : null;
                panel.Update();
            }),
            new Button("余白を変更・解除", () =>
            {
                spaced = !spaced;
                panel.Style.Padding = spaced ? "32px" : null;
                panel.Update();
            }),
            new Button("非表示を予約（Updateなし）", () =>
            {
                panel.IsVisible = false;
                status.Value = "まだ表示中です。「外観を反映」で非表示になります";
                status.Update();
            }),
            new Button("外観を反映", panel.Update));
        panel.Layout.Gap = "12px";
        panel.CssClass = "demo-component-frame";
        var row = new Component { Layout = Layouts.Row };
        row.Layout.Gap = "8px";
        row.Add(new Text("Layouts.Row"), new Button("横並びの例"));
        panel.Add(new Text("外観と表示") { Style = Styles.Subheading }, input, details,
            row, new Button("入力内容を確認", () => { status.Value = input.Value; status.Update(); }));
        Add(new Text("外観と表示のデモ") { Style = Styles.Subheading }, controls, panel, status);
    }
}
