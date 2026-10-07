using Suisharp;

namespace Suisharp.Demo;

// アプリの部品にはRenderer・通信・DOMの知識を渡さない。
public class DemoPage : Component
{
    private readonly Text title = new("Suisharp デモ");
    private readonly Text intro = new("C#のComponent、Style、Layoutをブラウザで試せます") { Style = Styles.Muted };
    private readonly Text clock = new("サーバーからの更新: 0") { Style = Styles.Muted };
    private readonly ChatList chats = new();
    private readonly ManualUpdateExample manual = new();
    private int ticks;

    public DemoPage(Action showWelcome)
    {
        // This component is shown inside SampleApp, whose root owns the page theme.
        // Keep this wrapper visually flat so the existing demo keeps its card layout.
        CssClass = "demo-screen";
        Layout.Gap = "14px";
        title.Style = Styles.Heading;
        Add(title, intro,
            clock,
            new Button("最初の画面へ戻る", showWelcome),
            manual,
            new TextBoxExample(),
            new UpdateExample(),
            new AppearanceExample(),
            chats,
            new Button("チャットを追加", chats.AddChat),
            new Button("ページ全体をUpdate", Update));
    }

    public void Tick()
    {
        clock.Value = $"サーバーからの更新: {++ticks}";
        clock.Update();
    }
}

public class ManualUpdateExample : Component
{
    private readonly Text value = new("明示更新: 0");
    private int count;

    public ManualUpdateExample()
    {
        Add(value,
            new Button("値だけ変更（まだ描画しない）", () => value.Value = $"明示更新: {++count}"),
            new Button("Text.Updateで反映", value.Update));
    }
}

public class TextBoxExample : Component
{
    private readonly TextBox input = new();
    private readonly Text received = new("C#で受け取った入力: （空）");
    private readonly Text submitted = new("送信内容: （未送信）");

    public TextBoxExample()
    {
        input.OnInput = value =>
        {
            received.Value = $"C#で受け取った入力: {value}";
            received.Update();
        };
        Add(new Text("TextBox — 入力して送信できます") { Style = Styles.Subheading }, input, received,
            new Button("入力内容を送信", () =>
            {
                submitted.Value = $"送信内容: {input.Value}";
                submitted.Update();
            }),
            new Button("C#から入力欄を変更", () =>
            {
                input.Value = "C#から設定しました";
                input.Update();
            }), submitted);
    }
}

public class UpdateExample : Component
{
    private readonly ModelView child = new();
    private int count;

    public UpdateExample()
    {
        Add(new Text("親Update → 子override → Text.Update") { Style = Styles.Subheading }, child,
            new Button("モデルだけ変更", () => child.Count = ++count),
            new Button("親から描画更新", Update));
    }

    private class ModelView : Component
    {
        public int Count;
        private readonly Text value = new("子の描画: 0");
        public ModelView() => Add(value);
        public override void Update()
        {
            value.Value = $"子の描画: {Count}";
            base.Update();
        }
    }
}

public class ChatList : Component
{
    private int nextNumber;

    public ChatList() => AddChat();

    public void AddChat()
    {
        var item = new ChatItem(++nextNumber, RemoveChat);
        Add(item);
        Update();
    }

    private void RemoveChat(ChatItem item)
    {
        Remove(item);
        Update();
    }
}

public class ChatItem : Component
{
    private readonly Text message = new("メッセージ: 0");
    private int count;

    public ChatItem(int number, Action<ChatItem> remove)
    {
        Add(new Text($"チャット {number}") { Style = Styles.Subheading }, message,
            new Button("メッセージを更新", () =>
            {
                message.Value = $"メッセージ: {++count}";
                message.Update();
            }),
            new Button("この部品をUpdate", Update),
            new Button("削除", () => remove(this)));
    }
}
