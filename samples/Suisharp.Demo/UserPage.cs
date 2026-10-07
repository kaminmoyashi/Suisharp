using Suisharp;

namespace Suisharp.Demo;

/// <summary>Route parameterを通常のC#値として受け取るサンプルページ。</summary>
public sealed class UserPage : Component
{
    public UserPage(int id)
    {
        Style = Styles.Default;
        Add(
            new Text("ユーザー情報") { Style = Styles.Heading },
            new Text($"Routeから受け取ったID: {id}"),
            new Text("URLやRouteValuesをComponentから参照する必要はありません") { Style = Styles.Muted });
    }
}
