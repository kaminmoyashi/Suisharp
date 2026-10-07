using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Channels;

namespace Suisharp.Web;

/// <summary>一つの部品ツリーを一つのWebSocketへ描画します。</summary>
public sealed class WebRenderer
{
    private Component? root;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly Dictionary<string, Component> displayedComponents = [];
    private readonly Dictionary<string, Node> displayedNodes = [];
    private readonly Dictionary<string, string?> displayedParents = [];
    private Channel<string>? outgoing;
    private CancellationTokenSource? connection;
    private int updateDepth;
    private readonly HashSet<string> pendingUpdates = [];

    public WebRenderer(Component root) => this.root = root;

    public static string Html => ReadResource("index.html");
    public static string JavaScript => ReadResource("suisharp.js");

    /// <summary>
    /// 初回描画・クリック受信・明示的Updateの送信を接続終了まで処理します。
    /// 同じツリーを別スレッドから操作する場合はlock(root)で変更とUpdateをまとめてください。
    /// クリック処理も同じロック内で実行します。
    /// </summary>
    public async Task RunAsync(WebSocket socket, CancellationToken cancellationToken = default)
    {
        var activeRoot = root ?? throw new InvalidOperationException("Rendererは接続ごとに新しく作成してください。");
        using var lifetime = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        lock (activeRoot)
        {
            if (activeRoot.Parent is not null || activeRoot.RenderRequested is not null || connection is not null)
                throw new InvalidOperationException("Rendererには未接続のルート部品を渡してください。");
            connection = lifetime;
            outgoing = Channel.CreateBounded<string>(256);
            activeRoot.RenderRequested = Render;
            Queue(new { operation = "mount", node = Capture(activeRoot) });
        }
        try
        {
            var send = SendAsync(socket, lifetime.Token);
            var receive = ReceiveAsync(socket, lifetime.Token);
            await Task.WhenAny(send, receive);
            var violation = receive.IsCompletedSuccessfully ? receive.Result : null;
            lifetime.Cancel();
            try { await Task.WhenAll(send, receive); }
            catch (OperationCanceledException) when (lifetime.IsCancellationRequested) { }
            catch (WebSocketException) { /* 切断後のツリーは接続単位で破棄する。 */ }
            if (violation is not null && socket.State == WebSocketState.Open)
                await socket.CloseOutputAsync(violation.CloseStatus, violation.Description, CancellationToken.None);
            else if (socket.State == WebSocketState.CloseReceived)
                await socket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "", CancellationToken.None);
        }
        finally
        {
            lock (activeRoot)
            {
                activeRoot.RenderRequested = null;
                outgoing!.Writer.TryComplete();
                outgoing = null;
                connection = null;
                displayedComponents.Clear();
                displayedNodes.Clear();
                displayedParents.Clear();
                pendingUpdates.Clear();
                root = null;
            }
            activeRoot = null!;
        }
    }

    private void Render(Component component, Action updateChildren)
    {
        var activeRoot = root;
        if (activeRoot is null) return;
        lock (activeRoot)
        {
            // 新規追加した子だけをUpdateしても親の構造は描画しない。
            if (!displayedNodes.ContainsKey(component.Id) || displayedParents[component.Id] != component.Parent?.Id)
            {
                updateChildren();
                return;
            }
            updateDepth++;
            try
            {
                // 親は自分の構造だけを扱う。既存の子の現在値を勝手に読み直さない。
                Reconcile(component);
                pendingUpdates.Add(component.Id);
                updateChildren();
                Reconcile(component);
            }
            finally
            {
                if (--updateDepth == 0) FlushUpdates();
            }
        }
    }

    private void Reconcile(Component component)
    {
        var previous = displayedNodes[component.Id];
        var children = component.Children.ToArray();
        var ids = children.Select(child => child.Id).ToHashSet();
        foreach (var oldChild in previous.Children)
            if (!ids.Contains(oldChild.Id) && displayedParents.GetValueOrDefault(oldChild.Id) == component.Id)
                Forget(oldChild);
        foreach (var child in children)
        {
            // 新しい部品には初期描画が必要。以後の更新は各部品のUpdateに任せる。
            if (!displayedNodes.ContainsKey(child.Id)) Capture(child);
            displayedParents[child.Id] = component.Id;
        }
        displayedNodes[component.Id] = previous with
        {
            Value = ValueOf(component),
            IsVisible = component.IsVisible,
            CssClass = component.CssClass,
            Style = component.Style.Snapshot(),
            Layout = component.Layout.Snapshot(),
            Children = children.Select(child => displayedNodes[child.Id]).ToArray()
        };
    }

    private void FlushUpdates()
    {
        foreach (var id in pendingUpdates)
        {
            if (!displayedNodes.ContainsKey(id)) continue;
            var parent = displayedParents[id];
            while (parent is not null && !pendingUpdates.Contains(parent))
                parent = displayedParents.GetValueOrDefault(parent);
            if (parent is null)
                Queue(new { operation = "update", node = Snapshot(id) });
        }
        pendingUpdates.Clear();
    }

    // 読み直すのは描画済みの記録だけ。overrideが更新しなかった値は保持する。
    private Node Snapshot(string id)
    {
        var node = displayedNodes[id];
        return node with { Children = node.Children
            .Where(child => displayedParents.GetValueOrDefault(child.Id) == id)
            .Select(child => Snapshot(child.Id)).ToArray() };
    }

    private static string? ValueOf(Component component) => component switch
    {
        Text text => text.Value, TextBox input => input.Value, Button button => button.Text, _ => null
    };

    private Node Capture(Component component)
    {
        var kind = component switch { Text => "text", TextBox => "textbox", Button => "button", _ => "component" };
        var value = ValueOf(component);
        var node = new Node(component.Id, kind, value, component.Children.Select(Capture).ToArray(),
            component.IsVisible, component.CssClass, component.Style.Snapshot(), component.Layout.Snapshot());
        displayedComponents[node.Id] = component;
        displayedNodes[node.Id] = node;
        displayedParents[node.Id] = component.Parent?.Id;
        return node;
    }

    private void Forget(Node node)
    {
        // 子だけのUpdateで更新された最新の構造を使う。
        // 親を描画した時点の古い子配列では、後から追加した孫を取り残す。
        if (!displayedNodes.TryGetValue(node.Id, out var latest)) return;
        foreach (var child in latest.Children)
            if (displayedParents.TryGetValue(child.Id, out var parentId) && parentId == node.Id)
                Forget(child);
        displayedComponents.Remove(node.Id);
        displayedNodes.Remove(node.Id);
        displayedParents.Remove(node.Id);
    }

    private void Queue(object message)
    {
        // Update時点の値を確定する。低速接続で更新を取り落とす代わりに切断する。
        if (!outgoing!.Writer.TryWrite(JsonSerializer.Serialize(message, JsonOptions)))
            connection!.Cancel();
    }

    private async Task SendAsync(WebSocket socket, CancellationToken cancellationToken)
    {
        await foreach (var message in outgoing!.Reader.ReadAllAsync(cancellationToken))
            await socket.SendAsync(Encoding.UTF8.GetBytes(message).AsMemory(),
                WebSocketMessageType.Text, true, cancellationToken);
    }

    private async Task<ProtocolViolation?> ReceiveAsync(WebSocket socket, CancellationToken cancellationToken)
    {
        var buffer = new byte[65536];
        while (!cancellationToken.IsCancellationRequested)
        {
            var length = 0;
            ValueWebSocketReceiveResult result;
            do
            {
                result = await socket.ReceiveAsync(buffer.AsMemory(length), cancellationToken);
                if (result.MessageType == WebSocketMessageType.Close) return null;
                if (result.MessageType != WebSocketMessageType.Text)
                    return new(WebSocketCloseStatus.InvalidMessageType, "Text messages are required.");
                length += result.Count;
                if (length == buffer.Length && !result.EndOfMessage)
                    return new(WebSocketCloseStatus.MessageTooBig, "Message exceeds the 65536 byte limit.");
            } while (!result.EndOfMessage);

            BrowserEvent? input;
            try { input = JsonSerializer.Deserialize<BrowserEvent>(buffer.AsSpan(0, length), JsonOptions); }
            catch (JsonException) { return new(WebSocketCloseStatus.InvalidPayloadData, "Invalid event message."); }
            if (input?.Id is null || string.IsNullOrWhiteSpace(input.Id) || input.Event is null)
                return new(WebSocketCloseStatus.InvalidPayloadData, "Invalid event message.");
            if (input.Event is not ("click" or "input"))
                return new(WebSocketCloseStatus.PolicyViolation, "Unknown event.");
            var activeRoot = root;
            if (activeRoot is null) return null;
            lock (activeRoot)
            {
                if (!displayedComponents.TryGetValue(input.Id, out var component)) continue;
                var owner = component;
                while (owner.Parent is not null) owner = owner.Parent;
                // 削除直後の古いブラウザイベントも実行しない。
                if (!ReferenceEquals(owner, activeRoot) || !IsDisplayedVisible(component.Id)) continue;
                if (input.Event == "click" && component is Button button)
                    button.OnClick?.Invoke();
                else if (input.Event == "input" && component is TextBox textBox && input.Value is not null)
                {
                    textBox.Value = input.Value;
                    // 入力でブラウザ自身が変更した表示も記録する。親の選択更新で巻き戻さない。
                    displayedNodes[textBox.Id] = displayedNodes[textBox.Id] with { Value = input.Value };
                    textBox.OnInput?.Invoke(input.Value);
                }
                else
                    return new(WebSocketCloseStatus.PolicyViolation, "Event does not match the component.");
            }
        }
        return null;
    }

    private static string ReadResource(string name)
    {
        using var stream = typeof(WebRenderer).Assembly.GetManifestResourceStream($"Suisharp.Web.Browser.{name}")!;
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    private bool IsDisplayedVisible(string id)
    {
        for (string? current = id; current is not null; current = displayedParents.GetValueOrDefault(current))
            if (!displayedNodes.TryGetValue(current, out var node) || !node.IsVisible) return false;
        return true;
    }

    private sealed record Node(string Id, string Kind, string? Value, Node[] Children,
        bool IsVisible, string CssClass, Dictionary<string, string> Style, Dictionary<string, string> Layout);
    [JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
    private sealed record BrowserEvent(string? Event, string? Id, string? Value);
    private sealed record ProtocolViolation(WebSocketCloseStatus CloseStatus, string Description);
}
