# Suisharp

Simple UI for C#

Build small web UIs with ordinary C# components. Suisharp keeps the component tree in C# while a browser connects over WebSocket.

> Don't learn Suisharp. Write C#.

## Hello World

```csharp
using Suisharp;

var app = SuisharpApp.Create(args);
app.MapGet("/", () => new Text("Hello, Suisharp!"));
app.Run();
```

## Run a single C# file

The CLI PoC runs a plain `.cs` file without asking you to create a project file or configure ASP.NET Core. The tool package is not published yet; once installed, the command is:

```sh
suisharp run Hello.cs
```

It requires the .NET 10 SDK. The CLI creates a temporary project outside the source directory, asks the SDK to build the original file, and runs the result. The SDK's incremental build output is reused on later runs. App arguments can follow `--`, for example `suisharp run Hello.cs -- --urls http://localhost:5080`.

To try the CLI from this checkout without installing a tool:

```sh
dotnet run --project src/Suisharp.Cli -- run samples/Hello.cs
```

See [`samples/HelloPage.cs`](samples/HelloPage.cs) for a Component with a button. Both samples use the public Suisharp API.

## What is included

- `Component`, `Text`, `TextBox`, and `Button` with `Add`, `Remove`, and explicit `Update()`.
- `Styles` for a few CSS presets and `Layouts` for vertical or horizontal child placement. Custom appearance uses ordinary CSS values and CSS files.
- A WebSocket renderer that keeps each connected Component tree alive, sends C# updates to the browser, and dispatches button and text input events to C#.
- `SuisharpApp` for the usual host setup, plus an ASP.NET Core escape hatch for advanced configuration.

The tree is created per WebSocket connection. Component fields hold live connection state; durable data belongs in the application's usual database or cache. Suisharp does not include its own Session, authentication, or dependency injection system.

## Run the demo

Install the .NET 10 SDK, then run `./run.sh` on Linux/macOS or `./run.ps1` on Windows. Open <http://127.0.0.1:5080>. The demo shows the welcome page, Component switching, explicit updates, text input, and CSS styling.

## Build and test

```sh
dotnet restore Suisharp.slnx --configfile NuGet.Config --disable-parallel
dotnet build Suisharp.slnx --no-restore -m:1
dotnet run --project tests/Suisharp.Tests --no-build
```

The executable test project has no external test-framework dependency. See [`docs/APPEARANCE.md`](docs/APPEARANCE.md) for the implemented appearance API and [`docs/SECURITY.md`](docs/SECURITY.md) for security boundaries and deployment responsibilities.

## License

MIT. See [LICENSE](LICENSE).
