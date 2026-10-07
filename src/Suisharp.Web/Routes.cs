using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Suisharp.Web;

namespace Suisharp;

/// <summary>ASP.NET Core Endpoint RoutingにComponentページを登録する薄いAPI。</summary>
public static class Routes
{
    /// <summary>
    /// GET endpointをComponentページとして登録します。通常のGETにはHTMLシェルを返し、
    /// 同じURLへのWebSocket要求ではMinimal APIの標準バインドを使ってComponentを構築します。
    /// </summary>
    public static RouteHandlerBuilder MapGet(
        IEndpointRouteBuilder endpoints,
        string pattern,
        Delegate pageFactory,
        string? stylesheetHref = null)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        ArgumentException.ThrowIfNullOrWhiteSpace(pattern);
        ArgumentNullException.ThrowIfNull(pageFactory);

        // HTTP/1.1 WebSocket upgrades use GET; HTTP/2 extended CONNECT uses CONNECT.
        var endpoint = endpoints.MapMethods(pattern, ["GET", "CONNECT"], pageFactory);
        endpoint.AddEndpointFilter(new ComponentPageEndpointFilter(stylesheetHref));
        return endpoint;
    }

    /// <summary>POST endpointをASP.NET Core Minimal APIへそのまま登録します。</summary>
    public static RouteHandlerBuilder MapPost(IEndpointRouteBuilder endpoints, string pattern, Delegate handler)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        ArgumentException.ThrowIfNullOrWhiteSpace(pattern);
        ArgumentNullException.ThrowIfNull(handler);
        return endpoints.MapPost(pattern, handler);
    }

    private sealed class ComponentPageEndpointFilter(string? stylesheetHref) : IEndpointFilter
    {
        public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext invocation, EndpointFilterDelegate next)
        {
            var context = invocation.HttpContext;
            if (!context.WebSockets.IsWebSocketRequest)
            {
                var html = WebRenderer.Html;
                if (!string.IsNullOrWhiteSpace(stylesheetHref))
                {
                    var href = HtmlEncoder.Default.Encode(stylesheetHref);
                    html = html.Replace("</head>", $"  <link rel=\"stylesheet\" href=\"{href}\">\n</head>", StringComparison.Ordinal);
                }
                return Results.Content(html, "text/html; charset=utf-8");
            }

            var result = await next(invocation);
            if (result is not Component page)
                throw new InvalidOperationException("Routes.MapGetのWebSocketハンドラはComponentを返してください。");

            try
            {
                using var socket = await context.WebSockets.AcceptWebSocketAsync();
                await new WebRenderer(page).RunAsync(socket, context.RequestAborted);
            }
            finally
            {
                if (page is IAsyncDisposable asyncDisposable)
                    await asyncDisposable.DisposeAsync();
                else if (page is IDisposable disposable)
                    disposable.Dispose();
            }
            return null;
        }
    }
}
