using Suisharp;

namespace Suisharp.Demo;

/// <summary>公開用サンプルの起点。ボタンで表示するComponentを切り替える。</summary>
public sealed class SampleApp : Component, IAsyncDisposable
{
    private readonly WelcomePage welcome;
    private readonly DemoPage demo;
    private Component activePage;
    private readonly CancellationTokenSource lifetime = new();
    private readonly Task clock;

    public SampleApp()
    {
        Style = Styles.Default;
        Layout.Gap = "14px";

        welcome = new WelcomePage(ShowDemo);
        demo = new DemoPage(ShowWelcome);
        activePage = welcome;
        Add(activePage);
        clock = TickAsync(lifetime.Token);
    }

    public void Tick()
    {
        if (ReferenceEquals(activePage, demo))
            demo.Tick();
    }

    private void ShowDemo() => SwitchTo(demo);
    private void ShowWelcome() => SwitchTo(welcome);

    private void SwitchTo(Component page)
    {
        if (ReferenceEquals(activePage, page)) return;
        Remove(activePage);
        Add(page);
        activePage = page;
        Update();
    }

    public async ValueTask DisposeAsync()
    {
        lifetime.Cancel();
        try { await clock; }
        finally { lifetime.Dispose(); }
    }

    private async Task TickAsync(CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
        try
        {
            while (await timer.WaitForNextTickAsync(cancellationToken))
                lock (this) Tick();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
    }
}

/// <summary>最初に表示する、最小のSuisharp画面。</summary>
public sealed class WelcomePage : Component
{
    public WelcomePage(Action showDemo)
    {
        Add(
            new Text("こんにちは、Suisharpです") { Style = Styles.Heading },
            new Text("C#のComponentからWeb画面を作るサンプルです") { Style = Styles.Muted },
            new Button("サンプル画面を見る", showDemo));
    }
}
