# Design

Suisharp builds a browser UI from ordinary C# objects. A `Component` owns child components; `Add` and `Remove` change that tree, while `Update()` explicitly sends the selected component's current UI to the browser.

The WebSocket renderer keeps one component tree for each live connection. It transports UI events and update snapshots; application state remains in ordinary C# fields or in application-owned storage. There is no reactive state store or automatic component reconstruction.

`Style` affects a component itself. `Layout` affects how that component arranges its children. Both accept standard CSS values, so detailed styling remains in CSS rather than a Suisharp-specific layout language.

Page routes use ASP.NET Core Endpoint Routing and Minimal API parameter binding. `SuisharpApp` supplies the usual WebSocket and browser-resource setup; advanced applications can access its underlying `WebApplication`.

The CLI is a thin SDK client. It creates a temporary project that references the CLI's Suisharp assemblies and includes the user's source file directly, then delegates compilation to the .NET SDK.
