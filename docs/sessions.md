# Sessions, configuration and recovery

## Connect an MCP client

For a first connection, follow [installation](installation.md) and [getting started](getting-started.md). This page is the detailed reference for ownership, configuration, and recovery.

Use [examples/mcp-settings.json](https://github.com/Realynx/Fl-MCP/blob/master/examples/mcp-settings.json) as the client configuration. Replace the executable, template, workspace, and companion DLL paths with your own absolute paths. The server uses stdio and puts logs on stderr. It does not need an AI provider key or the production AI gateway.

| Setting | Purpose |
| --- | --- |
| `FL_MCP_FL_EXE` | Exact FL executable; no implicit version discovery |
| `FL_MCP_TEMPLATE` | Existing absolute FLP path copied for each new project |
| `FL_MCP_WORKSPACE` | Absolute output/sample workspace; defaults to `%LocalAppData%\FlMcp\Projects` |
| `FL_MCP_PYTHON_RUNTIME` | Private CPython runtime directory; defaults to `<FL>/FruityLink/tools/fl-mcp/python/runtime` |
| `FL_MCP_PYTHON_PATH` | SDK wheel or development source directory; defaults to the installed wheel under `<FL>/FruityLink/tools/fl-mcp/python` |

The current installer can configure the framework-owned runtime under `<FL>/FruityLink/python` explicitly. The table describes this adapter's fallback paths when those environment overrides are absent. Do not replace an active interpreter's runtime or package in place; restart FL after an update.

Session and attachment tokens are generated privately. Do not add `FL_MCP_SESSION_TOKEN` to client settings. Discovery and control use Windows named pipes restricted to the same user; the client verifies the pipe server's actual Windows process ID before sending or reading credentials. Tokens never appear in discovery/attach tool results. There is no network listener.

## Assist your current project

1. Open FL normally and enable FL MCP in FruityLink's Plugins menu. Call `fl_instances` to discover matching instances; discovery only returns the exact executable configured by `FL_MCP_FL_EXE`.
2. Call `fl_attach` with the returned `processId`. This explicitly selects the current project and returns `ownership: "attached"`, its status, and the effective snapshot/sample workspace. The project itself may be outside that workspace or untitled. No launch template is needed for attachment.
3. Inspect `fl_status`, `fl_python_api` and channels, then use the authoring tools or embedded Python to assist your work. `fl_project_save` creates a new workspace snapshot without changing the active filename, playback or song mode.
4. Call `fl_detach` when finished. Detach and closing the MCP client leave FL open. `fl_project_close` and `fl_project_render` refuse attached sessions; save a snapshot, detach, and close FL yourself before starting a disposable render session from that snapshot.

Only one companion may hold an instance's attachment lease. Explicit detach releases it. A companion process that exits leaves a stale lease which the next attachment can replace; Windows process creation time prevents PID reuse from inheriting it. The same companion can retry an interrupted attachment. Plugin reload changes discovery identity and requires attachment again.

The lease uses this client's configured workspace, returned by `fl_attach`. Runtime/package overrides are passed during attachment because an already-running FL cannot inherit the companion's environment. The default paths are relative to the actual FL executable. Once the private interpreter has initialized, changing its runtime configuration requires restarting FL.

Project identity is **best effort**: typed title, path and untitled state are checked before operations and embedded SDK callbacks. Wait for startup/project loading to finish before attaching; if loading changes the initial project identity, attach again. After a detectable project switch, `fl_status` reports `requiresReattach` and edits/saves require `fl_attach` again. FL exposes no reliable generation ID, so replacing one untitled project with another bearing identical fields cannot be detected. Human edits can occur concurrently; checks and batches are not transactions and cannot prevent a project switch between a check and a native call.

## Author and render

1. Call `fl_project_start` with a fresh workspace path, such as `sessions/first.flp`. It refuses to reuse any already-running FL process. It waits for a responding bridge and the exact copied project path before returning readiness, tempo, and PPQ.
2. Read channels, installed plugins, and patterns; load the desired generators or workspace WAV samples. Create and name a pattern, then immediately author its notes before creating the next empty pattern.
3. Use the returned PPQ to express note and clip timing. At PPQ 96, one quarter note is 96 ticks and one 4/4 bar is 384 ticks. Place each pattern into the playlist with `fl_clip_add`; notes in an unarranged pattern alone do not make a rendered song.
4. Set routing, mixer levels, effects, and plugin parameters using discovered indices. Save a known recovery path with `fl_project_save`, for example `versions/first-v1.flp`.
5. Call `fl_project_render` with a fresh output path such as `audio/first.wav`. This saves another snapshot, checks its FLP envelope and complete data chunk, ends only the disposable managed editor, then runs FL's documented command-line WAV export. It requires successful process exit and a structurally nonempty WAV before returning the audio and snapshot paths. A header check is not proof that every FL event or external asset is valid.
6. After rendering, restart from the template or pass `sourceProjectPath` to `fl_project_start` to copy a previous workspace snapshot into a fresh session. Use `fl_project_close` to save and close without rendering.

The `fl_` names are suitable alongside a Blender MCP server in the same client. Hand Blender the returned WAV path (or copy the file to its machine if needed) and use the same planned duration/tempo. This repository does not couple to a particular Blender MCP implementation.

## Boundaries and recovery

Python project lifecycle calls `new_project`, `open_project`, `save_project`, `save_project_as`, and `save_new_version` are refused under this adapter. Use the MCP lifecycle tools. Python `save_copy` requires a fresh workspace `.flp` path, and native sample operations require existing workspace files. These are SDK-call policies; arbitrary trusted Python retains the account's ordinary file permissions. Batch policy is checked before any batch item runs, but batches do not provide transactions or rollback.

`fl_execute_python` accepts up to 128 KiB of code. Its default deadline is 60 seconds, configurable from 1 to 300. Cancellation is checked immediately at script entry and SDK-call boundaries and periodically during traced Python execution. Numerical loops are polled once per 1024 trace events. This bounds callback overhead, not wall-clock response time; blocking native work must still finish before cleanup.

Output overwrite, path traversal, Windows device names, alternate data streams, and workspace symlink/junction traversal are refused. Stage WAV samples inside the workspace. The workspace is a path guard, not an OS sandbox against other processes running as your user.

Every edit and save rechecks the selected project identity. Keep hands off a disposable managed FL window while authoring; attached sessions support concurrent human edits with the limitations described above. The server serializes its own changes. Tools may partially apply if FL fails between operations; read state before retrying note/clip additions because they append.

Launch defaults to 120 seconds (maximum 300), ordinary bridge calls to 30 seconds, save to 60, and render to 600 (maximum 3600). Embedded execution cancellation signals the plugin and waits for its completion acknowledgement while preserving the pipe and session gate. If the bridge breaks before acknowledgement, FL is left running until a later serialized status reply confirms the interpreter is idle. Already-issued changes cannot be rolled back. Render/launch cancellation stops only processes created by this companion. A render error includes its preserved snapshot path; after explicit client cancellation, use your last known saved recovery path. Existing or partially written outputs are preserved, so retry with a new filename.

Closing the MCP client ends its disposable FL session only after embedded execution has drained. A session whose execution completion is unknown remains open. An attached FL process is never terminated by the companion, including after a lost connection. Save frequently. The typed `fl` API exposes no raw-memory or reflection dispatch; arbitrary in-process Python still has the host's privileges.

Managed launch/render preserve an extra input copy and inspect process-owned dialogs. The exact invalid-note load prompt can be recovered on that disposable copy; a startup save containing invalid notes is declined because its destination is unconfirmed. Other actionable dialogs fail with a diagnostic and preserved project path. Successful recovery returns warnings that must be reviewed before further authoring. See [dialog recovery](dialog-recovery.md) for exact messages, limits and verified builds. Attached windows never receive automated dialog responses.

Hidden launch is a request to Windows, not a guarantee that FL never shows UI. Render completion requires FL to exit; if a particular build leaves its render process open, the call times out rather than guessing that a stable file is complete. WAV RIFF output below 4 GiB is supported; RF64 and non-WAV exports are not validated in this initial version. Audio quality, sample rate, and tail handling come from FL's saved export settings.
