# Troubleshooting

Start with the last successful step: client connection, FL discovery, attachment, an SDK query, or rendering. `fl_python_docs` works before connecting to FL; `fl_status` helps inspect an established session. The companion writes diagnostics to stderr so its stdout remains an MCP protocol stream.

## Connection and discovery

| Symptom | Check and next step |
| --- | --- |
| No FL MCP tools in the app | Confirm the client configuration uses its expected schema and the correct companion DLL/command path. Restart the app after installer setup. The current adapter advertises 27 tools. |
| `dotnet` or runtime error | The companion targets .NET 10 and the plugin targets .NET 9. Check that the command and matching runtimes are available to the client; see [developer deployment](building.md). |
| `fl_instances` returns nothing | Open FL, enable **FL MCP** in FruityLink's Plugins menu, and check that `FL_MCP_FL_EXE` identifies that exact executable. Discovery is restricted to the configured executable and same user. |
| FL was open during installation | Restart FL, or enable the plugin in its Plugins menu. Restart FL after runtime/package updates before executing Python. |
| Attachment says another client owns the lease | Detach from the other companion first. One companion can hold the attachment lease at a time. |
| `requiresReattach` after attachment | Wait for project loading to finish, inspect the project you intend to select, and call `fl_attach` again. A detected project switch invalidates the prior identity. |

For exact environment settings and runtime overrides, read [connect an MCP client](sessions.md#connect-an-mcp-client). Do not copy private session tokens into client settings.

## Project and file operations

| Symptom | Check and next step |
| --- | --- |
| `fl_project_start` refuses to launch | Close other FL processes. Disposable startup does not reuse your personal session. Check that the executable and saved `.flp` template exist at absolute paths. |
| Snapshot/render path refused | Use a new path inside `FL_MCP_WORKSPACE` with the correct `.flp` or `.wav` extension. Existing files, traversal, device names, alternate data streams, and symlink/junction traversal are refused. |
| A sample cannot be loaded | Stage the existing WAV inside the configured workspace, then pass its workspace path. Confirm the selected FL project is still attached and ready. |
| Close/render is refused while attached | Save a snapshot, detach, close FL yourself, then start a disposable copy of the snapshot using `sourceProjectPath`. |
| A failed request left notes or clips | Read the current state before retrying. Add operations append and failures can occur after some edits already applied. |

Attached saves preserve active filename, playback, and song mode. Disposable saves stop playback and select song mode. All snapshot names must be fresh; repeated saves do not overwrite your original project.

## Python and capabilities

| Symptom | Check and next step |
| --- | --- |
| Python runtime/package cannot be loaded | Use a matching installer bundle or check `FL_MCP_PYTHON_RUNTIME` and `FL_MCP_PYTHON_PATH`. Restart FL after changing an initialized interpreter's configuration. |
| An operation or helper is unavailable | Read `fl_python_api` and `fl.capabilities()` for the actual installed SDK/build. Compare against [compatibility](capabilities.md#compatibility). |
| Response too large | Return selected fields, parameter pages, or smaller analysis windows. Avoid full parameter lists and raw PCM. |
| Script reaches its deadline | Wait for cooperative cancellation/native work to drain, then inspect state. A deadline does not undo completed edits or guarantee immediate interruption of blocking native code. |
| Save/open project operation refused in Python | Use the MCP project lifecycle tools. This adapter intentionally keeps project identity and snapshot policy in its companion. |
| Mixer routing points at the wrong insert after insertion | Requery `fl.mixer.list()` and channel routes. Structural edits can shift indices. |

If the bridge breaks before execution completion is acknowledged, FL is left running until a later serialized status reply confirms the interpreter is idle. Use [recovery guidance](sessions.md#boundaries-and-recovery) before attempting more lifecycle operations.

## Rendering and audio

Inspect FL's desktop for missing assets, plugin licensing, recovery, or export dialogs. Rendering uses FL's saved export settings and waits for process exit, with a default 600-second deadline and a maximum of 3600. A build that leaves its renderer open will time out even if a file appears stable.

If rendering fails, use the preserved snapshot path reported by the error. After explicit client cancellation, use your last known saved recovery snapshot. Existing/partially written outputs are preserved: choose a new output filename when retrying.

If the WAV is silent or incomplete, inspect the arrangement and sound sources. Notes need playlist placement for the intended song arrangement, instruments need their assets/licenses, and routing or automation can silence a signal. WAV structure validation does not establish musical correctness.

Offline analysis does not capture live channel audio. Smaller ranges help when true-peak/loudness work limits are exceeded. PSR is unavailable without a complete three-second context; a master render cannot be attributed to one instrument. See [Python analysis](python.md#analyze-a-rendered-file).

## Report a reproducible issue

Include the exact FL Studio build, Windows version, installer/SDK/adapter versions, whether the session was attached or disposable, the failing tool and arguments, and the error text. Describe the last successful operation and whether FL displayed a dialog. Use a small disposable project when possible; avoid sharing credentials, private paths, or licensed assets unnecessarily.

Compare your case with [known verification limits](live-verification.md#remaining-limits), then open an [issue in FL MCP](https://github.com/Realynx/Fl-MCP/issues). SDK/native behavior may need to be tracked in the [FruityLink framework](https://github.com/Realynx/FL-Automation/issues).
