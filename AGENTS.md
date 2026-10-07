# Contributor guidance

- Keep the public API small and use normal C# and .NET features where they fit.
- `Update()` is explicit: do not add automatic state observation or rendering.
- `Style` describes a component; `Layout` describes placement of its children.
- `Remove` detaches a component. The application owns timers, subscriptions, and disposal of its resources.
- Keep the WebSocket renderer's per-connection state isolated and release it when the connection ends.
- Build the solution and run `tests/Suisharp.Tests` after implementation changes.
