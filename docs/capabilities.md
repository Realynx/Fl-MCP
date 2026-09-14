# Capabilities and compatibility

FL MCP exposes 29 tools and the shared FruityLink Python SDK. Tool availability describes the adapter's interface; a successful operation also depends on the selected FL build, installed plugins, loaded project, and native SDK capabilities. Query `fl_python_api` and `fl.capabilities()` after connecting.

## What you can do

| Area | Current workflow | Where to start |
| --- | --- | --- |
| Inspect projects | Discover an FL process, attach to a saved or untitled project, read tempo, PPQ, channels, patterns, and playlist tracks | [Getting started](getting-started.md) |
| Author music | Add installed generators or workspace WAV samples, create patterns, append/read notes, arrange pattern clips | [Tool reference](tools.md#authoring) |
| Mix and control plugins | Set mixer levels/pan, route channels, load effects, discover/set plugin parameters; use Python to create ordinary mixer inserts | [Python guide](python.md#use-more-of-the-sdk) |
| Automate parameters | Create an automation channel with its initial target link, edit its points, and place automation clips through Python | [Python guide](python.md#use-more-of-the-sdk) |
| Save and render | Save fresh workspace snapshots; close or render a disposable project; resume a saved snapshot | [Sessions](sessions.md#author-and-render) |
| Analyze audio | Measure supplied/rendered WAV regions, amplitude, windows, spectrum, loudness, and estimated true peak/PSR through Python | [Analysis example](python.md#analyze-a-rendered-file) |
| Extend workflows | Execute trusted Python with the reusable SDK, discover generated operations, hand rendered WAV paths to another tool | [Python guide](python.md) |

Use the [C# SDK documentation](https://github.com/Realynx/FL-Automation/blob/master/docs/csharp/index.md) to build a FruityLink plugin. FL MCP's Python execution is one host for the SDK, with its own project lifecycle policy.

## Compatibility

The documented checkpoint is **September 12, 2026**: FL MCP **0.2.0**, SDK/Python **0.2.0**, and locally prepared installer **0.1.22**. These version numbers identify separate components and do not imply a public release.

| FL Studio build | Evidence |
| --- | --- |
| **26.1.3.5570, Windows x64** | Live installed-plugin verification: saved/untitled attachment, authoring, snapshots, save/reopen, mixer insertion, linked automation, offline analysis, cancellation recovery, and WAV rendering |
| **25.2.5.5319, Windows x64** | Exact-binary inspection and native/managed regression tests; no live run in this verification cycle |
| Other builds | Not established by these results; check the exact build and its capabilities |

The [live verification record](live-verification.md) contains measurements, reproducible checks, and outstanding discrepancies. Support is tied to exact native builds, not just “FL 2025” or “FL 2026.”

## Limits to plan around

- **Session ownership:** attached FL processes stay user-owned. Close/render require a disposable session. Interactive startup requires other FL processes to be closed; background startup uses a private Windows desktop and can run beside independent sessions.
- **Project identity:** detectable project changes require reattachment. Concurrent human edits and identical untitled replacements cannot be made transactional.
- **Execution:** Python runs inside FL with the account's permissions. Deadlines request cooperative cancellation; blocking native calls can delay completion. Completed edits remain.
- **Output:** use fresh paths inside the workspace. Rendering validates WAV RIFF files below 4 GiB, not RF64 or other export formats, and waits for the renderer process to exit.
- **Automation:** creation links its initial target; additional target linking is not supported. Creation failure can leave a channel or clip, so inspect before retrying.
- **Audio analysis:** supplied WAV analysis does not capture live channel PCM. Master audio is a complete mix, and mixer stems may include multiple instruments/sends. True peak is an estimate; PSR needs a complete three-second context.
- **Note inspection:** one verified saved project contained 5,341 serialized notes while a later live enumeration reported 5,076. The preserved files match, but the query discrepancy remains unresolved.

For API/result bounds and failure handling, read [Python execution limits](python.md#execution-and-results) and [session recovery](sessions.md#boundaries-and-recovery).
