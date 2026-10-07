using Suisharp.Demo;
using Suisharp;

var app = SuisharpApp.Create(args, configureWebSockets: options =>
{
    options.AllowedOrigins.Add("http://127.0.0.1:5080");
    options.AllowedOrigins.Add("http://localhost:5080");
});
app.MapGet("/", () => new SampleApp(), "/demo.css");
app.MapGet("/users/{id:int}", (int id) => new UserPage(id), "/demo.css");
app.Run();
