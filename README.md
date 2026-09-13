# FL MCP

**Make and edit music in FL Studio through an MCP client.** Attach to a project you already have open, or let an agent start a disposable project, author it, save it, and render a WAV.

FL MCP provides **27 MCP tools**, including embedded Python, API discovery, project snapshots, and rendering. The reusable Python API belongs to the [FruityLink SDK](https://github.com/Realynx/FL-Automation), not this adapter. A small plugin runs inside FL; a separate stdio MCP server manages the connection and project lifecycle. Private, bundled CPython runs **inside FL Studio's process** and calls the shared SDK directly. FL MCP itself needs no system Python installation, pip, or AI provider key, and does not use the production AI gateway.

**Live verified:** FL Studio **26.1.3.5570**, including saved/untitled attachment, full musical authoring, save/reopen, and WAV rendering. The locally prepared installer **0.1.22** also verifies mixer insertion, linked automation, and audio analysis. FL Studio 2025 has exact-binary analysis and automated coverage, but **no live validation** in this verification run. Support is tied to exact native builds. See [verification results and remaining limits](docs/live-verification.md).

## Get started

You need Windows x64, licensed FL Studio, and a matching FruityLink installation. FL remains a desktop application and needs a logged-in Windows session. Here, “headless” means no FL MCP plugin window and unattended operation where FL permits it; it does not mean a service, container, or guaranteed invisible application.

The tested installer is a **locally prepared build, not a public release offered here**. Obtain a matching offline installer from the maintainer, or follow the [source build and developer deployment guide](docs/building.md). SDK 0.2.0 packages are not assumed available on NuGet or PyPI. No FL license, project, instrument, or sample is bundled by this repository.

With the offline FruityLink installer:

1. Select your FL installation and MCP clients. Client setup enables FL MCP for your user and registers the companion. The installer supplies private CPython 3.14.6 and the matching SDK wheel.
2. Restart the selected MCP clients. If FL was already running during installation, enable **FL MCP** in FruityLink's Plugins menu or restart FL so it loads the plugin.
3. Call `fl_python_docs`, then choose an attached or disposable session below.

Manual client configuration starts from [examples/mcp-settings.json](examples/mcp-settings.json). Adapt its `mcpServers` wrapper to your client's schema and replace every example path. The companion targets .NET 10; the FruityLink plugin host targets .NET 9. [Configuration and runtime details](docs/sessions.md#connect-an-mcp-client).

## Choose a session

| | Assist an open project | Author and render a disposable project |
| --- | --- | --- |
| Connect | `fl_instances`, then `fl_attach(processId)` | `fl_project_start(projectPath)` |
| Project | Your current saved or untitled project | A fresh copy of the configured template or workspace snapshot |
| Save | `fl_project_save(projectPath)` writes a new snapshot | The same tool saves a recovery snapshot |
| Finish | `fl_detach` leaves FL open | `fl_project_close(projectPath)` saves and closes, or `fl_project_render(outputPath)` saves, closes, and renders |
| Ownership | FL is never terminated by the companion | Only this companion's disposable processes may be stopped |

For a disposable session, close other FL processes first. `FL_MCP_TEMPLATE` must point to a saved `.flp`; returned tempo and PPQ describe the loaded project. Output paths such as `sessions/first.flp`, `versions/first.flp`, and `audio/first.wav` are relative to `FL_MCP_WORKSPACE` and must not already exist. After rendering, resume a saved snapshot with `fl_project_start(projectPath, sourceProjectPath=...)`.

Attached sessions cannot be closed or rendered by the companion. To render one, save a workspace snapshot, detach, close FL yourself, and start a disposable session from that snapshot. Wait for FL to finish loading before attaching. If project identity changes, `fl_status` reports `requiresReattach`; call `fl_attach` again. [Ownership, loading, and recovery details](docs/sessions.md).

## Use the Python SDK

After attaching or starting a project, call `fl_python_api` to discover the current SDK contract, then send Python to `fl_execute_python`. The supplied `fl` object is a typed `Studio`:

```python
print(fl.project.info)
print(fl.transport.tempo)
result = fl.channels.list()
```

The SDK provides channels, patterns, notes, playlist clips, mixer routing/effects, plugin parameters, arrangements, and automation. Current additions include:

- `fl.mixer.add(name="New insert", after=3)` and `fl.mixer.list()` for ordinary inserts. Structural edits shift indices; requery tracks and routes afterward.
- `fl.automation.create(AutomationTarget.channel_volume(3), track=1, start_tick=0, length_tick=1536)` and `fl.automation[channel].set_points(...)` for linked automation. Import `AutomationTarget` and `AutomationPointSpec` from `fruitylink`. Playlist tracks are one-based; placement uses ticks and curve points use beats.
- `fl.analysis.wav(path, start_seconds=..., end_seconds=...)` followed by `.summary()`, `.windows(...)`, `.spectral()`, or `.spectral_windows(...)` for supplied audio. RMS, energy, crest, occupancy, gated loudness, and true-peak/PSR estimates have explicit ranges and work limits. This does not capture live channel audio; a master render is not an isolated instrument.

For large parameter sets, return a page such as `fl.channels[0].parameters.page(filter="cutoff", limit=32)` rather than every parameter. Assign a JSON-compatible value to `result`; ordinary printing is captured separately. Discover capabilities with `fl.capabilities()` and use `fl.ops` for generated operations without a high-level helper. See [the authoring example](examples/author.py) and the [SDK Python guide](https://github.com/Realynx/FL-Automation/tree/master/python).

The adapter owns start/save/close/render policy; the SDK owns DAW behavior. Use MCP project tools for lifecycle changes. Scripts run with your account's permissions inside FL's memory space, **not in a sandbox**. Results are bounded to 512 KiB, each captured stream to 64 KiB, and the full response to 1 MiB. The default Python deadline is 60 seconds, configurable from 1–300 seconds. Cancellation is cooperative and waits for active native work to drain; completed edits are not rolled back. [Execution and recovery limits](docs/sessions.md#boundaries-and-recovery).

A rendered WAV can be passed to another MCP server, including Blender MCP. There is no dependency on a particular Blender integration. [Design background](docs/blender-mcp-design.md).

## Development and license

[Build and test instructions](docs/building.md) explain explicit SDK source references, private runtime setup, locked package caveats, and CI configuration. CI prepares an artifact; it does not publish a release or install into FL.

FL MCP is **source-available under [PolyForm Noncommercial 1.0.0](LICENSE)**. Its noncommercial restrictions mean it is not described as OSI open source. The license is the verbatim [official text](https://polyformproject.org/licenses/noncommercial/1.0.0.txt). Dependencies retain their own terms; see [third-party notices](THIRD-PARTY-NOTICES.md).
