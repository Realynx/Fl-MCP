# Building and developer deployment

## Build and install

The new SDK 0.2.0 dependencies are prepared locally and are **not assumed published** to NuGet or PyPI. Build against an explicit SDK checkout containing `src/FruityLink.Scripting` and `python`:

Use the SDK commit recorded in the repository's `SDK_REF` file for a reproducible starting point. That commit must exist in `Realynx/FL-Automation` before another machine or CI can fetch it. Run these commands from the FL-MCP repository root; replace the SDK path with your checkout. Building needs the .NET 10 SDK, .NET 9 runtime for plugin tests, and `uv` for the Python package build. These developer requirements do not mean end users need system Python.

```powershell
./scripts/codefactor-check.ps1 -FruityLinkSdkRoot C:\source\FL-Automation
uv run --directory C:\source\FL-Automation\python --locked python -m build
./scripts/package.ps1 -FruityLinkSdkRoot C:\source\FL-Automation -PythonWheel C:\source\FL-Automation\python\dist\fruitylink_python-0.2.0-py3-none-any.whl
```

The raw developer package contains separate `server`, `plugin/fl-mcp`, and `python` directories. The FruityLink installer adds the pinned official CPython runtime and installs the plugin, companion, wheel, and runtime together. Use that installer for an offline end-user installation.

For manual developer deployment, copy `plugin/fl-mcp` to `<FL>/FruityLink/plugins/fl-mcp` and the companion directories to `<FL>/FruityLink/tools/fl-mcp`. Place the official Windows x64 CPython 3.14.6 embeddable payload under `python/runtime`; the SDK loads its private `python314.dll`. Keep the wheel alongside that runtime directory. The raw developer package does not download or supply this runtime. Keep `FlMcp.Plugin.dll`, its `.deps.json`, and `FlMcp.Protocol.dll` together. The matching FruityLink host shares its `FruityLink.Scripting` assembly to keep one interpreter owner across plugin reloads. Do not copy the stdio server into the host plugins folder.

Selecting MCP clients in the FruityLink installer enables **FL MCP** for that user and writes the selected client settings. For manual deployment, enable FL MCP once in FruityLink's Plugins menu. FruityLink persists enabled IDs in `%LocalAppData%\FruityLink\plugins.json`. An ordinary FL session then advertises itself for explicit attachment. If FL was already running when the installer updated that file, enable FL MCP in its Plugins menu or restart FL. Close existing FL sessions before starting a disposable managed project. Building this repository alone changes neither your FL setup nor client settings.

FL MCP hosts the shared scripting dispatcher itself. The SDK's separate **Python Scripting** plugin is for standalone Python connections and is not required for MCP-managed scripts.

For a direct source build instead of package references:

```powershell
dotnet build FlMcp.slnx -c Release -p:FruityLinkSdkRoot=C:\source\FL-Automation -p:NuGetLockFilePath=obj/source-sdk.packages.lock.json -p:ShouldUnsetParentConfigurationAndPlatform=false
```

`FruityLinkSdkRoot` must point to the SDK repository root containing `src/FruityLink.Plugins.Abstractions`. The source-build lock file stays under each project's ignored `obj` directory, preserving the package-reference locks. The configuration flag keeps external SDK project references in the requested build configuration.

The tracked NuGet locks currently describe the exact locally prepared SDK 0.2.0 packages. Before enabling a public package-based build or release, regenerate and verify these locks against the actual published SDK artifacts; a later package build may have different content hashes. Source-checkout builds use their separate `obj` locks throughout.

## Verification and release status

`scripts/codefactor-check.ps1` performs a Release build with all warnings as errors, CA1502 complexity at most 15, CS0105 duplicate-using detection, and the test suite. The gate uses locked restore in package-reference mode; source builds keep separate locks under `obj`. Adapter tests use an official MCP stdio client, real local named pipes, and an injected embedded-runtime contract to verify direct routing and native-drain cancellation. The SDK validates its real CPython embedding separately in a disposable test host. FL orchestration and SDK calls use explicit fakes; fixtures exercise file envelopes, not musical correctness.

To include this adapter's real CPython integration test, set `FRUITYLINK_TEST_PYTHON_RUNTIME` to the private 3.14.6 runtime directory before running the gate. Set `FRUITYLINK_TEST_PYTHON_PACKAGE` to the rebuilt wheel or SDK `python/src` directory; the source-build gate defaults it to that SDK source directory. The test embeds Python in the isolated .NET test process, checks the process ID, and exercises direct fake-FL callbacks through the plugin. It never starts FL. Without the runtime variable, this one integration test is explicitly skipped.

CI runs against the SDK commit in the checked-in `SDK_REF` by default. A manual workflow `sdk_ref` or repository variable `FRUITYLINK_SDK_REF` can explicitly override it. Publish the referenced SDK commit before pushing an MCP revision that needs it; an unavailable commit must fail rather than silently use an older SDK. CI builds a distribution artifact only. It does not create a GitHub release, publish packages, or install into FL. Before publishing a stable release, follow the [live verification checklist](live-verification.md) on each exact supported build.
