using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Hosting;
using Suisharp.Web;

namespace Suisharp;

/// <summary>SuisharpのWebアプリを構成・実行する、接続先を明示したホスト。</summary>
public sealed class SuisharpApp : IAsyncDisposable
{
    private readonly WebApplication application;

    private SuisharpApp(WebApplication application) => this.application = application;

    /// <summary>
    /// Suisharp用の標準Webホストを作成します。
    /// ASP.NET Coreのサービスを追加する場合はconfigureBuilderを指定できます。
    /// </summary>
    public static SuisharpApp Create(
        string[]? args = null,
        Action<WebApplicationBuilder>? configureBuilder = null,
        Action<WebSocketOptions>? configureWebSockets = null)
    {
        var builder = WebApplication.CreateBuilder(args ?? []);
        configureBuilder?.Invoke(builder);
        var application = builder.Build();

        application.UseStaticFiles();
        var webSocketOptions = new WebSocketOptions();
        configureWebSockets?.Invoke(webSocketOptions);
        application.UseWebSockets(webSocketOptions);
        application.MapGet("/suisharp.js", () =>
            Results.Content(WebRenderer.JavaScript, "text/javascript; charset=utf-8"));

        return new SuisharpApp(application);
    }

    /// <summary>Componentを返すGET routeを登録します。</summary>
    public RouteHandlerBuilder MapGet(string pattern, Delegate pageFactory, string? stylesheetHref = null) =>
        Routes.MapGet(application, pattern, pageFactory, stylesheetHref);

    /// <summary>通常のMinimal API POST endpointを登録します。</summary>
    public RouteHandlerBuilder MapPost(string pattern, Delegate handler) =>
        Routes.MapPost(application, pattern, handler);

    /// <summary>必要な場合にASP.NET Coreの標準設定へアクセスします。</summary>
    public WebApplication WebApplication => application;

    /// <summary>Webアプリを実行します。</summary>
    public void Run() => application.Run();

    /// <summary>Webアプリを非同期で実行します。</summary>
    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        await application.StartAsync(cancellationToken);
        await application.WaitForShutdownAsync(cancellationToken);
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync() => application.DisposeAsync();
}
