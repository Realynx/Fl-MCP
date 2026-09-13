# MCP tool reference

These are the **27 tools** currently declared in [FlTools.cs](https://github.com/Realynx/Fl-MCP/blob/master/src/FlMcp.Server/FlTools.cs). Inputs use the spelling shown below. A value after `=` is a default; inputs without a default are required. Your MCP client supplies the protocol envelope and cancellation token.

`fl_instances`, `fl_attach`, `fl_project_start`, and `fl_python_docs` are connection/setup entry points. Project inspection, authoring, and API discovery require an attached or disposable session. All output paths must be fresh and inside the workspace. See [session configuration](sessions.md) for full ownership and recovery rules.

## Connection and lifecycle

| Tool | Inputs | Behavior |
| --- | --- | --- |
| `fl_instances` | None | Lists discoverable enabled FL instances matching the configured executable; does not attach. |
| `fl_attach` | `processId` | Selects that process's current project and acquires an exclusive attachment lease. Supports untitled projects. |
| `fl_detach` | None | Releases an attached lease and leaves FL open; waits for prior execution to drain. |
| `fl_project_start` | `projectPath`, `timeoutSeconds=120`, `sourceProjectPath=null`, `background=false` | Starts a disposable copy of the configured template or an existing workspace snapshot. Interactive mode requires no other FL processes. Background mode runs on a private Windows desktop and supports parallel sessions from independent MCP clients. Maximum timeout: 300 seconds. |
| `fl_status` | None | Reads process/bridge identity, project metadata, tempo, and PPQ. A detected project change reports `requiresReattach`. |
| `fl_project_save` | `projectPath` | Writes a verified new `.flp` snapshot without changing active project identity. Attached saves preserve playback/song mode. |
| `fl_project_close` | `projectPath` | Saves to a new `.flp`, verifies it, then terminates the disposable editor. Refuses attached sessions. |
| `fl_project_render` | `outputPath`, `timeoutSeconds=600` | Saves, closes the disposable editor, exports to a new WAV, waits for renderer exit, and validates the WAV. Maximum timeout: 3600 seconds. Refuses attached sessions. |

Rendering ends the editing session. Its result includes an audio path and snapshot path; resume with `fl_project_start` and `sourceProjectPath`.

## Discovery

| Tool | Inputs | Behavior |
| --- | --- | --- |
| `fl_channels_list` | None | Lists zero-based channel rack indices and names. |
| `fl_patterns_list` | None | Lists one-based pattern indices and names. |
| `fl_playlist_list` | None | Lists playlist track indices/names; query before placing clips. |
| `fl_plugins_list` | `effects=false` | Lists installed generators; set `effects=true` for mixer effects. Use returned exact names. |
| `fl_parameters_list` | `channelOrTrack`, `slot=-1`, `filter=null` | Lists plugin parameters. `slot=-1` addresses a generator channel; slots 0–9 address a mixer effect. For large sets prefer Python paging. |

## Authoring

| Tool | Inputs | Behavior |
| --- | --- | --- |
| `fl_tempo_set` | `bpm` | Sets 10–522 BPM and returns its read-back value. |
| `fl_channel_add` | `name` | Loads an installed generator by exact catalog name; returns its zero-based channel index. |
| `fl_sample_add` | `path` | Adds an existing workspace WAV as a sampler channel. |
| `fl_pattern_create` | `name` | Creates/names an empty pattern; returns its one-based index. Author notes before creating the next empty pattern. |
| `fl_notes_add` | `pattern`, `notes` | Appends 1–2048 notes. Each note has `channel`, `key`, `startTick`, `lengthTick`, and `velocity`. |
| `fl_notes_read` | `pattern`, `channel=-1`, `offset=0` | Reads notes; `channel=-1` means all channels. Follow the returned continuation offset for paging. |
| `fl_clip_add` | `pattern`, `track`, `startTick`, `lengthTick` | Appends a pattern clip and selects song mode. Pattern and playlist track are one-based; track is 1–500. |

For `fl_notes_add`, `channel` is zero-based, `key` is MIDI 0–127, `startTick` is nonnegative, `lengthTick` is positive, and `velocity` is 1–127. Always query PPQ first. This argument object is a one-quarter-note example **only when the project reports PPQ 96**, pattern 1 exists, and channel 0 is the intended generator:

```json
{
  "pattern": 1,
  "notes": [
    {"channel": 0, "key": 60, "startTick": 0, "lengthTick": 96, "velocity": 90}
  ]
}
```

Notes and clips append. If a request fails or its response is lost, inspect the project before retrying so you do not create duplicates.

## Mixer and plugin parameters

| Tool | Inputs | Behavior |
| --- | --- | --- |
| `fl_mixer_set` | `track`, `volume`, `pan=6400` | Mixer volume/pan use 0–12800; roughly 7624 volume is 0 dB and 6400 pan is center. |
| `fl_channel_route` | `channel`, `track` | Routes a zero-based generator to Master (0) or an active ordinary mixer insert. |
| `fl_effect_add` | `track`, `slot`, `name` | Loads an exact installed effect name into slot 0–9, replacing any existing effect in that slot. |
| `fl_parameter_set` | `channelOrTrack`, `slot`, `parameter`, `value` | Sets a discovered parameter to normalized 0–1. `slot=-1` addresses a generator; 0–9 addresses a mixer effect. |

Discover active mixer indices with Python `fl.mixer.list()`. Master is 0; active ordinary inserts are within 1–500. Current/dormant slots are refused. Structural insertion can shift indices: query again before routing or editing effects.

## Python and SDK discovery

| Tool | Inputs | Behavior |
| --- | --- | --- |
| `fl_python_docs` | None | Returns execution conventions and lifecycle guidance, even before connecting to FL. |
| `fl_python_api` | `filter=null` | Returns operations, typed arguments, descriptions, and defaults from the shared SDK contract. Matches the optional filter against operation names/descriptions. |
| `fl_execute_python` | `code`, `timeoutSeconds=60` | Runs trusted Python inside FL with `fl` supplied as a `Studio`. Deadline range: 1–300 seconds; cancellation is cooperative. |

Use [the Python guide](python.md) for results, paging, automation, audio analysis, and links to the complete reusable SDK documentation.
