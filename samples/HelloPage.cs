using Suisharp;

var app = SuisharpApp.Create(args);
app.MapGet("/", () => new HelloPage());
app.Run();

class HelloPage : Component
{
    private readonly Text message = new("Hello, Suisharp!");
    private int clickCount;

    public HelloPage()
    {
        Add(message, new Button("Click", Click));
    }

    private void Click()
    {
        message.Value = $"Clicked {++clickCount} time(s)";
        message.Update();
    }
}
