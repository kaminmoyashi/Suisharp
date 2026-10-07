using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Routing;
using Suisharp;
using Suisharp.Web;

var checks = 0;
void Check(bool condition, string name)
{
    if (!condition) throw new Exception($"FAIL: {name}");
    Console.WriteLine($"PASS: {name}");
    checks++;
}
void Reject(Action action, string name)
{
    try { action(); }
    catch (InvalidOperationException) { Check(true, name); return; }
    throw new Exception($"FAIL: {name}");
}

var defaultStyle = Styles.Default;
var separateDefaultStyle = Styles.Default;
Check(defaultStyle["background-color"] == "#d9edf9" && defaultStyle["--suisharp-component-background"] == "#ffffff",
    "Styles.Default defines a blue page and white component cards");
Check(Styles.Heading["font-weight"] == "700" && Styles.Subheading["font-size"] is not null &&
      Styles.Muted["color"] is not null, "text styles define visual hierarchy");
defaultStyle["color"] = "#ff0000";
Check(separateDefaultStyle["color"] == "#263b4b", "Styles.Default returns a fresh mutable Style");
Check(Layouts.Default["flex-direction"] == "column" && Layouts.Row["flex-direction"] == "row",
    "Layouts.Default stacks children and Layouts.Row places them horizontally");
Check(WebRenderer.JavaScript.Contains("textContent", StringComparison.Ordinal) &&
      !WebRenderer.JavaScript.Contains("innerHTML", StringComparison.Ordinal),
    "browser renderer treats component text as text, not HTML");

var routeBuilder = WebApplication.CreateBuilder();
await using var routeApp = routeBuilder.Build();
var routedId = -1;
var routedTab = "";
var routePageDisposed = false;
var postedName = "";
Routes.MapGet(routeApp, "/users/{id:int}", (int id, string? tab) =>
{
    routedId = id;
    routedTab = tab ?? "";
    return new RouteProbePage(id, routedTab, () => routePageDisposed = true);
}, "/demo.css?x=\"quoted");
Routes.MapGet(routeApp, "/", () => new Text("home"));
Routes.MapPost(routeApp, "/users", (NewUser request) =>
{
    postedName = request.Name;
    return Results.Ok();
});
var routeEndpoints = ((IEndpointRouteBuilder)routeApp).DataSources
    .SelectMany(source => source.Endpoints).OfType<RouteEndpoint>().ToArray();
var userEndpoint = routeEndpoints.Single(endpoint => endpoint.RoutePattern.RawText == "/users/{id:int}");
var userDocumentContext = new DefaultHttpContext { RequestServices = routeApp.Services };
userDocumentContext.Request.Method = "GET";
userDocumentContext.Request.Path = "/users/42";
userDocumentContext.Request.RouteValues["id"] = "42";
userDocumentContext.Response.Body = new MemoryStream();
var userDocumentRequest = userEndpoint.RequestDelegate ?? throw new Exception("user endpoint has no request delegate");
await userDocumentRequest(userDocumentContext);
userDocumentContext.Response.Body.Position = 0;
var userHtml = await new StreamReader(userDocumentContext.Response.Body).ReadToEndAsync();
Check(userHtml.Contains("href=\"/demo.css?x=&quot;quoted\"", StringComparison.Ordinal) && routedId == -1,
    "GET route serves shell without constructing a second Component");
using var routeSocket = new TestSocket();
using var routeStop = new CancellationTokenSource();
var userSocketContext = new DefaultHttpContext { RequestServices = routeApp.Services, RequestAborted = routeStop.Token };
userSocketContext.Request.Method = "GET";
userSocketContext.Request.Path = "/users/42";
userSocketContext.Request.QueryString = new QueryString("?tab=orders");
userSocketContext.Request.RouteValues["id"] = "42";
userSocketContext.Features.Set<IHttpWebSocketFeature>(new TestWebSocketFeature(routeSocket));
var routeRun = userDocumentRequest(userSocketContext);
var routeMount = await routeSocket.Next();
Check(routedId == 42 && routedTab == "orders" &&
      routeMount.GetProperty("node").GetProperty("children")[0].GetProperty("value").GetString() == "route id: 42; tab: orders",
    "typed route and query values create the WebSocket Component tree");
routeStop.Cancel();
await routeRun.WaitAsync(TimeSpan.FromSeconds(3));
Check(userSocketContext.Response.Body.Length == 0, "WebSocket endpoint does not append an HTTP body after socket completion");
Check(routePageDisposed, "Routes disposes the root Component after a WebSocket ends");
var postEndpoint = routeEndpoints.Single(endpoint => endpoint.RoutePattern.RawText == "/users");
var postContext = new DefaultHttpContext { RequestServices = routeApp.Services };
postContext.Request.Method = "POST";
postContext.Request.Path = "/users";
postContext.Request.ContentType = "application/json";
var postBody = Encoding.UTF8.GetBytes("{\"name\":\"Mina\"}");
postContext.Request.Body = new MemoryStream(postBody);
postContext.Request.ContentLength = postBody.Length;
postContext.Features.Set<IHttpRequestBodyDetectionFeature>(new TestRequestBodyFeature());
postContext.Response.Body = new MemoryStream();
var postRequest = postEndpoint.RequestDelegate ?? throw new Exception("post endpoint has no request delegate");
await postRequest(postContext);
postContext.Response.Body.Position = 0;
var postResponse = await new StreamReader(postContext.Response.Body).ReadToEndAsync();
Check(postedName == "Mina" && postContext.Response.StatusCode == 200,
    $"POST delegates typed body binding and results to Minimal APIs (name={postedName}, status={postContext.Response.StatusCode}, body={postResponse})");

await using (var suisharpHost = SuisharpApp.Create())
{
    suisharpHost.MapGet("/hello", () => new Text("hello"));
    suisharpHost.MapPost("/hello", () => Results.Ok());
    var hostEndpoints = ((IEndpointRouteBuilder)suisharpHost.WebApplication).DataSources
        .SelectMany(source => source.Endpoints).OfType<RouteEndpoint>().ToArray();
    Check(hostEndpoints.Any(endpoint => endpoint.RoutePattern.RawText == "/hello") &&
          hostEndpoints.Any(endpoint => endpoint.RoutePattern.RawText == "/suisharp.js"),
        "SuisharpApp registers routes and the browser renderer endpoint without host setup in the caller");
}

var root = new Component();
var group = new Component();
group.Layout = Layouts.Row;
var text = new Text("initial");
text.Style = Styles.Heading;
var other = new Text("other");
var clicks = 0;
var button = new Button("click", () => { text.Value = $"click {++clicks}"; text.Update(); });
group.Add(text, button);
root.Add(group, other);
Reject(() => group.Add(root), "cycles rejected");
Reject(() => root.Add(text), "two parents rejected");
var extra = new Text("extra");
Reject(() => root.Add(extra, extra), "duplicate batch rejected");
Check(root.Children.Count == 2, "failed Add is atomic");
root.Remove(extra);
Check(root.Children.Count == 2, "Remove missing child is harmless");

using var socket = new TestSocket();
using var stop = new CancellationTokenSource();
var renderer = new WebRenderer(root);
var running = renderer.RunAsync(socket, stop.Token);
var mount = await socket.Next();
Check(mount.GetProperty("operation").GetString() == "mount", "initial mount");
var rootNode = mount.GetProperty("node");
var groupNode = rootNode.GetProperty("children")[0];
var groupId = groupNode.GetProperty("id").GetString();
var textId = groupNode.GetProperty("children")[0].GetProperty("id").GetString();
var buttonId = groupNode.GetProperty("children")[1].GetProperty("id").GetString();
Check(groupNode.GetProperty("children")[0].GetProperty("value").GetString() == "initial", "nested initial text");
Check(groupNode.GetProperty("layout").GetProperty("flex-direction").GetString() == "row",
    "initial render captures child layout separately from Style");
Check(groupNode.GetProperty("children")[0].GetProperty("style").GetProperty("font-size").GetString() == "1.75rem",
    "Styles.Heading is captured on its Text only");
text.Value = "pending";
Check(await socket.Quiet(), "assignment alone sends nothing");
other.Value = "unpublished sibling";
text.Update();
var update = (await socket.Next()).GetProperty("node");
Check(update.GetProperty("id").GetString() == textId && update.GetProperty("value").GetString() == "pending", "leaf Update only sends leaf");
group.Add(extra);
extra.Update();
Check(await socket.Quiet(), "Add and undisplayed child Update do not publish structure");
group.Update();
update = (await socket.Next()).GetProperty("node");
Check(update.GetProperty("id").GetString() == groupId && update.GetProperty("children").GetArrayLength() == 3, "parent Update publishes Add");
socket.Click(buttonId!);
update = (await socket.Next()).GetProperty("node");
Check(clicks == 1 && update.GetProperty("value").GetString() == "click 1", "browser event invokes C# and C# Update returns");
button.Text = "changed label";
button.Update();
Check((await socket.Next()).GetProperty("node").GetProperty("value").GetString() == "changed label", "button label Update");
group.Remove(button);
socket.Click(buttonId!);
Check(await socket.Quiet() && clicks == 1, "removed button cannot execute even before parent Update");
group.Update();
Check((await socket.Next()).GetProperty("node").GetProperty("children").GetArrayLength() == 2, "parent Update publishes Remove");
button.Update();
Check(await socket.Quiet(), "detached Update does nothing");
group.Add(button);
group.Update();
await socket.Next();
socket.Click(buttonId!);
await socket.Next();
Check(clicks == 2, "re-added button works exactly once");
socket.Click("unknown");
socket.Click(buttonId!);
await socket.Next();
Check(clicks == 3, "stale component ids are ignored while valid events continue");
text.Value = "first";
text.Update();
text.Value = "second";
text.Update();
Check((await socket.Next()).GetProperty("node").GetProperty("value").GetString() == "first" &&
      (await socket.Next()).GetProperty("node").GetProperty("value").GetString() == "second", "Update snapshots are ordered and immutable");
root.Update();
Check((await socket.Next()).GetProperty("node").GetProperty("children")[1].GetProperty("value").GetString() == "unpublished sibling", "root Update publishes whole subtree");
var selective = new Selective();
root.Add(selective);
root.Update();
await socket.Next();
selective.Update();
Check((await socket.Next()).GetProperty("node").GetProperty("kind").GetString() == "text", "ordinary override selects child Update");
var destination = new Component();
selective.Selected.Value = "selected changed";
selective.Skipped.Value = "must stay unpublished";
var callsBefore = selective.Calls;
root.Update();
var selectiveNode = (await socket.Next()).GetProperty("node").GetProperty("children")[2];
Check(selective.Calls == callsBefore + 1, "parent invokes child override exactly once");
Check(selectiveNode.GetProperty("children")[0].GetProperty("value").GetString() == "selected changed", "parent respects selected child Update");
Check(selectiveNode.GetProperty("children")[1].GetProperty("value").GetString() == "original skipped", "parent does not publish a child skipped by override");
var skippedInputId = selectiveNode.GetProperty("children")[2].GetProperty("id").GetString()!;
socket.Raw(JsonSerializer.Serialize(new { @event = "input", id = skippedInputId, value = "user typed" }));
// 更新の受信順序をクリック結果で確認してから、親から選択更新する。
var barrier = new Button("barrier", () => other.Update());
root.Add(barrier);
root.Update();
var barrierId = (await socket.Next()).GetProperty("node").GetProperty("children").EnumerateArray().Last().GetProperty("id").GetString()!;
socket.Click(barrierId);
await socket.Next();
root.Update();
selectiveNode = (await socket.Next()).GetProperty("node").GetProperty("children")[2];
Check(selectiveNode.GetProperty("children")[2].GetProperty("value").GetString() == "user typed", "selective parent update preserves browser-owned input");
root.Remove(barrier);
root.Update();
await socket.Next();
var derived = new DerivedView();
var nested = new Component();
nested.Add(derived);
root.Add(nested);
derived.Model = "model changed";
root.Update();
var nestedNode = (await socket.Next()).GetProperty("node").GetProperty("children").EnumerateArray().Last();
Check(derived.Calls == 1 && nestedNode.GetProperty("children")[0].GetProperty("children")[0].GetProperty("value").GetString() == "model changed", "nested override prepares value then base.Update renders it");
root.Update();
await socket.Next();
Check(derived.Calls == 2 && derived.Model == "model changed", "repeated drawing does not advance application state");
root.Remove(nested);
root.Update();
await socket.Next();
var detached = new Component();
var detachedView = new DerivedView { Model = "offline" };
detached.Add(detachedView);
detached.Update();
Check(detachedView.Calls == 1 && detachedView.Display.Value == "offline", "virtual dispatch also works without renderer");
root.Add(destination);
root.Update();
await socket.Next();
group.Remove(button);
destination.Add(button);
button.Update();
Check(await socket.Quiet(), "moved child waits for structural parent Update");
destination.Update();
await socket.Next();
group.Update();
await socket.Next();
socket.Click(buttonId!);
await socket.Next();
Check(clicks == 4, "moving between parents preserves event registration");
root.Update();
await socket.Next();
socket.Click(buttonId!);
await socket.Next();
Check(clicks == 5, "whole-tree Update after a move remains valid");
var inputBox = new TextBox("initial input");
var notifications = 0;
inputBox.OnInput = value =>
{
    Check(inputBox.Value == value, "OnInput observes updated Value");
    notifications++;
};
root.Add(inputBox);
root.Update();
var inputNode = (await socket.Next()).GetProperty("node").GetProperty("children").EnumerateArray().Last();
var inputId = inputNode.GetProperty("id").GetString()!;
Check(inputNode.GetProperty("kind").GetString() == "textbox" && inputNode.GetProperty("value").GetString() == "initial input", "TextBox initial rendering");
inputBox.Value = "server input";
Check(await socket.Quiet() && notifications == 0, "TextBox assignment neither renders nor invokes OnInput");
inputBox.Update();
Check((await socket.Next()).GetProperty("node").GetProperty("value").GetString() == "server input", "TextBox explicit Update");
socket.Raw(JsonSerializer.Serialize(new { @event = "input", id = inputId, value = "日本語 <hello> 🎉" }));
socket.Click(buttonId!); // 同じ接続内で後続のクリックを処理したことを待つ。
await socket.Next();
Check(inputBox.Value == "日本語 <hello> 🎉" && notifications == 1 && await socket.Quiet(), "browser input updates C# without implicit render");
socket.Raw(JsonSerializer.Serialize(new { @event = "input", id = inputId, value = "" }));
socket.Click(buttonId!);
await socket.Next();
Check(inputBox.Value == "" && notifications == 2, "empty input is accepted");
root.Remove(inputBox);
socket.Raw(JsonSerializer.Serialize(new { @event = "input", id = inputId, value = "stale" }));
socket.Click(buttonId!);
await socket.Next();
Check(inputBox.Value == "" && notifications == 2, "removed TextBox ignores stale input");
var appearance = new Component { CssClass = "panel panel  card" };
var appearanceInput = new TextBox("kept");
var appearanceClicks = 0;
var appearanceButton = new Button("appearance click", () => appearanceClicks++);
appearance.Layout["display"] = "flex";
appearance.Layout.Gap = "8px";
appearance.Layout[" GAP "] = "1rem";
appearance.Style["--Tone"] = "blue";
appearance.Style["--tone"] = "green";
Check(appearance.CssClass == "panel card", "CSS class tokens normalized");
Check(appearance.Layout.Gap == "1rem", "layout aliases share normalized CSS property");
Check(appearance.Style["--Tone"] == "blue" && appearance.Style["--tone"] == "green", "custom CSS properties preserve case");
appearance.Layout.Gap = "  ";
Check(appearance.Layout["gap"] is null, "empty layout removes declaration");
try { appearance.Layout[" "] = "bad"; throw new Exception("empty name accepted"); }
catch (ArgumentException) { Check(true, "empty layout property rejected"); }
Check(new Component().Style["display"] is null && new Component().Layout["flex-direction"] == "column",
    "Style and Layout have separate defaults");
appearance.Add(appearanceInput, appearanceButton);
root.Add(appearance);
root.Update();
var appearanceNode = (await socket.Next()).GetProperty("node").GetProperty("children").EnumerateArray().Last();
var appearanceInputId = appearanceNode.GetProperty("children")[0].GetProperty("id").GetString()!;
var appearanceButtonId = appearanceNode.GetProperty("children")[1].GetProperty("id").GetString()!;
Check(appearanceNode.GetProperty("isVisible").GetBoolean() && appearanceNode.GetProperty("layout").GetProperty("display").GetString() == "flex", "initial appearance and layout captured");
async Task InputBarrier()
{
    socket.Click(buttonId!);
    await socket.Next();
}
appearance.IsVisible = false;
Check(await socket.Quiet(), "visibility assignment does not render");
socket.Click(appearanceButtonId);
await InputBarrier();
Check(appearanceClicks == 1, "unpublished visibility does not suppress click");
appearance.Update();
appearanceNode = (await socket.Next()).GetProperty("node");
Check(!appearanceNode.GetProperty("isVisible").GetBoolean() && appearanceInput.IsVisible, "parent hidden retains child local visibility");
socket.Click(appearanceButtonId);
socket.Raw(JsonSerializer.Serialize(new { @event = "input", id = appearanceInputId, value = "stale hidden input" }));
await InputBarrier();
Check(appearanceClicks == 1 && appearanceInput.Value == "kept", "hidden ancestor blocks stale click and input");
appearanceInput.IsVisible = false;
appearanceInput.Update();
await socket.Next();
appearance.IsVisible = true;
appearance.Update();
appearanceNode = (await socket.Next()).GetProperty("node");
Check(appearanceNode.GetProperty("isVisible").GetBoolean() && !appearanceNode.GetProperty("children")[0].GetProperty("isVisible").GetBoolean(), "showing parent preserves hidden child");
appearance.Layout["display"] = null;
appearance.CssClass = null;
appearance.Update();
appearanceNode = (await socket.Next()).GetProperty("node");
Check(!appearanceNode.GetProperty("layout").TryGetProperty("display", out _) && appearanceNode.GetProperty("cssClass").GetString() == "", "layout and CSS class removal transmitted");
appearance.Style.Padding = "10px";
appearance.Update();
appearance.Style.Padding = "90px";
Check((await socket.Next()).GetProperty("node").GetProperty("style").GetProperty("padding").GetString() == "10px", "style snapshot copied at Update");
selective.Skipped.Style.Width = "999px";
selective.Skipped.CssClass = "unpublished";
selective.Skipped.IsVisible = false;
root.Update();
var skippedNode = (await socket.Next()).GetProperty("node").GetProperty("children")[2].GetProperty("children")[1];
Check(skippedNode.GetProperty("isVisible").GetBoolean() && skippedNode.GetProperty("cssClass").GetString() == "" && !skippedNode.GetProperty("style").TryGetProperty("width", out _), "parent respects skipped appearance attributes");
selective.IsVisible = false;
root.Update();
selectiveNode = (await socket.Next()).GetProperty("node").GetProperty("children")[2];
Check(selectiveNode.GetProperty("isVisible").GetBoolean(), "override without base does not publish own appearance");
var invisible = new DerivedView { IsVisible = false, Model = "hidden model" };
root.Add(invisible);
root.Update();
var invisibleNode = (await socket.Next()).GetProperty("node").GetProperty("children").EnumerateArray().Last();
Check(invisible.Calls == 1 && invisibleNode.GetProperty("children")[0].GetProperty("value").GetString() == "hidden model", "hidden components still run virtual Update");
stop.Cancel();
await running;
text.Update();
Check(await socket.Quiet(), "disconnect detaches renderer");
using (var reuseSocket = new TestSocket())
{
    try { await renderer.RunAsync(reuseSocket); throw new Exception("renderer reused after disconnect"); }
    catch (InvalidOperationException) { Check(true, "renderer releases root and is single-connection"); }
}

using var secondSocket = new TestSocket();
using var secondStop = new CancellationTokenSource();
var secondRun = new WebRenderer(root).RunAsync(secondSocket, secondStop.Token);
Check((await secondSocket.Next()).GetProperty("operation").GetString() == "mount", "tree can attach again after disconnect");
secondStop.Cancel();
await secondRun;

async Task ProtocolCase(string payload, WebSocketCloseStatus expected, string name, bool binary = false)
{
    var protocolRoot = new Component();
    var called = 0;
    protocolRoot.Add(new Button("protocol", () => called++));
    using var protocolSocket = new TestSocket();
    var protocolRun = new WebRenderer(protocolRoot).RunAsync(protocolSocket);
    await protocolSocket.Next();
    if (binary) protocolSocket.RawBinary(Encoding.UTF8.GetBytes(payload));
    else protocolSocket.Raw(payload);
    await protocolRun.WaitAsync(TimeSpan.FromSeconds(3));
    Check(protocolSocket.CloseStatus == expected && called == 0, name);
}

await ProtocolCase("{broken", WebSocketCloseStatus.InvalidPayloadData, "malformed JSON closes with invalid payload");
await ProtocolCase("{\"event\":\"unknown\",\"id\":\"x\"}", WebSocketCloseStatus.PolicyViolation,
    "unknown event closes with policy violation");
await ProtocolCase("{\"event\":\"click\",\"id\":\"x\",\"extra\":true}", WebSocketCloseStatus.InvalidPayloadData,
    "unexpected protocol fields are rejected");
await ProtocolCase(new string('x', 65537), WebSocketCloseStatus.MessageTooBig,
    "oversized message closes at the byte limit");
await ProtocolCase("binary", WebSocketCloseStatus.InvalidMessageType, "binary protocol message is rejected", binary: true);
Console.WriteLine($"All {checks} checks passed.");

sealed class Selective : Component
{
    public readonly Text Selected = new("selective");
    public readonly Text Skipped = new("original skipped");
    public readonly TextBox SkippedInput = new();
    public int Calls;
    public Selective() => Add(Selected, Skipped, SkippedInput);
    public override void Update() { Calls++; Selected.Update(); }
}

sealed class DerivedView : Component
{
    public string Model = "initial model";
    public readonly Text Display = new("initial display");
    public int Calls;
    public DerivedView() => Add(Display);
    public override void Update()
    {
        Calls++;
        Display.Value = Model;
        base.Update();
    }
}

sealed class TestSocket : WebSocket
{
    private readonly Channel<(byte[] Data, WebSocketMessageType Type)> input = Channel.CreateUnbounded<(byte[], WebSocketMessageType)>();
    private readonly Channel<string> output = Channel.CreateUnbounded<string>();
    private (byte[] Data, WebSocketMessageType Type)? pending;
    private int offset;
    private WebSocketState state = WebSocketState.Open;
    private WebSocketCloseStatus? closeStatus;
    public override WebSocketCloseStatus? CloseStatus => closeStatus;
    public override string? CloseStatusDescription => null;
    public override WebSocketState State => state;
    public override string? SubProtocol => null;
    public void Click(string id) => Raw(JsonSerializer.Serialize(new { @event = "click", id }));
    public void Raw(string message) => RawBinary(Encoding.UTF8.GetBytes(message), WebSocketMessageType.Text);
    public void RawBinary(byte[] data) => RawBinary(data, WebSocketMessageType.Binary);
    private void RawBinary(byte[] data, WebSocketMessageType type) => input.Writer.TryWrite((data, type));
    public async Task<JsonElement> Next()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        using var json = JsonDocument.Parse(await output.Reader.ReadAsync(timeout.Token));
        return json.RootElement.Clone();
    }
    public async Task<bool> Quiet()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromMilliseconds(120));
        try { await output.Reader.ReadAsync(timeout.Token); return false; }
        catch (OperationCanceledException) { return true; }
    }
    public override async Task<WebSocketReceiveResult> ReceiveAsync(ArraySegment<byte> buffer, CancellationToken cancellationToken)
    {
        pending ??= await input.Reader.ReadAsync(cancellationToken);
        var remaining = pending.Value.Data.Length - offset;
        var count = Math.Min(remaining, buffer.Count);
        pending.Value.Data.AsSpan(offset, count).CopyTo(buffer.AsSpan());
        offset += count;
        var endOfMessage = offset == pending.Value.Data.Length;
        var type = pending.Value.Type;
        if (endOfMessage) { pending = null; offset = 0; }
        return new WebSocketReceiveResult(count, type, endOfMessage);
    }
    public override Task SendAsync(ArraySegment<byte> buffer, WebSocketMessageType messageType, bool endOfMessage, CancellationToken cancellationToken)
    {
        output.Writer.TryWrite(Encoding.UTF8.GetString(buffer));
        return Task.CompletedTask;
    }
    public override void Abort() { }
    public override void Dispose() { }
    public override Task CloseAsync(WebSocketCloseStatus closeStatus, string? statusDescription, CancellationToken cancellationToken) => Task.CompletedTask;
    public override Task CloseOutputAsync(WebSocketCloseStatus closeStatus, string? statusDescription, CancellationToken cancellationToken)
    {
        this.closeStatus = closeStatus;
        state = WebSocketState.Closed;
        return Task.CompletedTask;
    }
}

sealed class NewUser(string name)
{
    public string Name { get; } = name;
}

sealed class RouteProbePage : Component, IAsyncDisposable
{
    private readonly Action disposed;
    public RouteProbePage(int id, string tab, Action disposed)
    {
        this.disposed = disposed;
        Add(new Text($"route id: {id}; tab: {tab}"));
    }
    public ValueTask DisposeAsync()
    {
        disposed();
        return ValueTask.CompletedTask;
    }
}

sealed class TestWebSocketFeature(TestSocket socket) : IHttpWebSocketFeature
{
    public bool IsWebSocketRequest => true;
    public Task<WebSocket> AcceptAsync(WebSocketAcceptContext context) => Task.FromResult<WebSocket>(socket);
}

sealed class TestRequestBodyFeature : IHttpRequestBodyDetectionFeature
{
    public bool CanHaveBody => true;
}
