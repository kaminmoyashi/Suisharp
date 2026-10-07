# Web host and routes

`SuisharpApp` owns an ASP.NET Core `WebApplication` and registers the WebSocket middleware, static file middleware, and the embedded browser script endpoint. `MapGet` and `MapPost` delegate to the existing `Routes` wrappers over Endpoint Routing and Minimal APIs.

```csharp
using Suisharp;

var app = SuisharpApp.Create(args);
app.MapGet("/", () => new HomePage());
app.MapGet("/users/{id:int}", (int id) => new UserPage(id));
app.Run();
```

A normal HTTP GET returns the HTML shell. The browser then opens a WebSocket at the same path; the GET handler is invoked for that connection and its returned `Component` becomes the connection's live tree. ASP.NET Core binds route and query values to ordinary handler parameters.

`MapGet` returns ASP.NET Core's `RouteHandlerBuilder`, so standard endpoint metadata such as authorization can be configured. The `WebApplication` property and `Create` configuration callbacks are escape hatches for standard ASP.NET Core setup.

Applications that already own an `IEndpointRouteBuilder` can use `Routes.MapGet(endpoints, ...)` and `Routes.MapPost(endpoints, ...)` directly. `WebRenderer` exposes its embedded HTML and JavaScript for such lower-level integrations.

The root component is disposed when its WebSocket ends if it implements `IDisposable` or `IAsyncDisposable`. Child ownership and disposal remain ordinary application code; `Remove` only detaches a child from the tree.
