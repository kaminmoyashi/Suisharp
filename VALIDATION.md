# Validation

Verified with the .NET 10 SDK:

- The full solution builds with no warnings or errors.
- All 77 executable checks pass, including route binding, WebSocket events, component updates, and disconnect cleanup.
- The `Suisharp.Cli` project packs as a .NET tool and installs from a local package source.
- The installed `suisharp` command builds and starts both `samples/Hello.cs` and `samples/HelloPage.cs`; arguments after `--` reach `SuisharpApp.Create(args)`.
- A deliberately invalid sample reports the original `.cs` file and line/column in the compiler diagnostic.

The CLI's build cache is under the operating system temporary directory at `suisharp/<source-path-hash>`. It is kept between runs for SDK incremental builds and can be removed with that directory; normal OS temporary-file cleanup may also remove it. The CLI does not write generated project files beside the user's source.

Browser interaction should be checked by running `./run.sh` or `./run.ps1` and opening the demo in a browser. Automated WebSocket checks use an in-memory socket and do not replace browser testing.
