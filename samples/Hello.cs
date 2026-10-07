using Suisharp;

var app = SuisharpApp.Create(args);
app.MapGet("/", () => new Text("Hello, Suisharp!"));
app.Run();
