# MCP tool reference

These are the **32 tools** currently declared in [FlTools.cs](https://github.com/Realynx/Fl-MCP/blob/master/src/FlMcp.Server/FlTools.cs). Inputs use the spelling shown below. A value after `=` is a default; inputs without a default are required. Your MCP client supplies the protocol envelope and cancellation token.

Embedded Python through `fl_execute_python` is the primary way to work: one request can run many operations with typed results across the helper classes. The single-operation authoring tools below are conveniences for quick edits and never define a second DAW API. See [the Python guide](python.md).

`fl_instances`, `fl_attach`, `fl_project_start`, and `fl_python_docs` are connection/setup entry points. Project inspection, authoring, and API discovery require an attached or disposable session. All output paths must be fresh and inside the workspace. See [session configuration](sessions.md) for full ownership and recovery rules.

## Connection and lifecycle

| Tool | Inputs | Behavior |
| --- | --- | --- |
| `fl_instances` | None | Lists discoverable enabled FL instances matching the configured executable; does not attach. |
| `fl_attach` | `processId` | Selects that process's current project and acquires an exclusive attachment lease. Supports untitled projects. |
| `fl_detach` | None | Releases an attached lease and leaves FL open; waits for prior execution to drain. |
| `fl_project_start` | `projectPath`, `timeoutSeconds=120`, `sourceProjectPath=null`, `background=false` | Starts a disposable copy of the configured template or an existing workspace snapshot. Interactive mode requires no other FL processes. Background mode runs on a private Windows desktop and supports parallel sessions from independent MCP clients. Maximum timeout: 300 seconds. The reported tempo and PPQ are settled values: after the bridge first answers, the tool polls until tempo, PPQ and title have stayed unchanged for two seconds (bounded by twenty seconds and the deadline); `settle.stable=false` with a `ProjectUnsettled` warning means re-read `fl_status`. |
| `fl_status` | None | Reads process/bridge identity, project metadata, tempo, and PPQ. A detected project change reports `requiresReattach`. |
| `fl_project_save` | `projectPath` | Writes a verified new `.flp` snapshot without changing active project identity. Attached saves preserve playback/song mode. |
| `fl_project_close` | `projectPath` | Saves to a new `.flp`, verifies it, then terminates the disposable editor. Refuses attached sessions. |
| `fl_project_render` | `outputPath`, `timeoutSeconds=600`, `startBar=null`, `endBar=null`, `cutClips=false`, `tailBeats=0` | Saves, closes the disposable editor, exports to a new WAV, waits for renderer exit, and validates the WAV. Maximum timeout: 3600 seconds per attempt. Refuses attached sessions. A nonzero renderer exit, an invalid WAV, or a WAV shorter than the project's span moves the partial file to `<name>.failed-attempt1.wav` and retries once from the same snapshot; the result lists every `attempts` entry and a second failure reports both. A spent deadline is not retried. `startBar`/`endBar` (one-based, inclusive) render only that section; `tailBeats` keeps that many beats of tail after `endBar`. |

Rendering ends the editing session. Its result includes an audio path, snapshot path, the WAV length in `seconds`, the `expectedSeconds` the project spans (the later of the last clip end and the last marker at the current tempo; `null` on a plugin build without the `song` operation) and the `attempts` evidence; resume with `fl_project_start` and `sourceProjectPath`. A retried render carries a `RenderRetried` warning; a WAV that is shorter than expected on both attempts by the same amount is accepted with `RenderShorterThanExpected` (a constant-tempo estimate cannot follow tempo automation).

### Section renders

FL's command-line exporter has no range option: it always renders from bar 1 to the later of the last clip end and the last time marker, so auditioning one section normally costs a full-length render. With `startBar` and `endBar` (for example 49 and 64 for sixteen bars) the tool first saves the untrimmed project as `<name>-full.flp` next to the render snapshot, then trims the live disposable project with the SDK's `fruitylink.audition.isolate_bars`: clips outside the span are deleted, clips running past `endBar` are shortened, the survivors move to bar 1, every time marker is removed and the loop selection is cleared. The trimmed project is then snapshotted and rendered as usual. The result adds `fullProject` (resume it with `fl_project_start` and `sourceProjectPath`) and `range` (kept, deleted and cut clip counts).

A clip that begins before `startBar` must be cut at the boundary. The SDK's slice restarts a *pattern* clip's second half from the pattern's first beat (audio and automation clips keep their source offset), so such clips are refused unless `cutClips=true`; start on a clip boundary for exact auditions. Because the markers are removed, FL stops the render at the last surviving clip end: bars 49-64 give exactly sixteen bars and reverb or release tails are cut (live, 2026-09-14). Pass `tailBeats` (for example 8 for two bars of 4/4) to place an `End` marker that many beats past the span; the result's `range.tail_ticks` reports what was added. If isolation fails, the editing session stays open, the error names the preserved `-full.flp`, and playlist edits made before the failure are not rolled back. Requires an installed `fruitylink` package that provides `fruitylink.audition`.

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
| `fl_mixer_set` | `track`, `volume`, `pan=0` | Mixer volume is a raw native integer on FL's fader scale 0–16000: 12800 (fader 0.8, every insert's default) is 0 dB, 16000 is the fader top (+5.6 dB) and 0 is silence. The law is not linear in dB; the SDK models it as dB = 20 × 2.889 × log10(raw / 12800), so 6400 is about -17 dB and 3200 about -35 dB (values below about -20 dB are ±3 dB estimates). For dB values use Python: `fl.mixer[t].set_volume(db=-6.0)`, `fl.mixer[t].volume_db`, or `fruitylink.levels.mixer_volume_from_db` / `mixer_volume_to_db`; channel-rack volume is a different scale (0–12800, 10240 = 0 dB, `fl.channels[i].set_volume(db=...)`). Earlier revisions documented the volume as 0–12800 with no dB conversion; that was the channel scale. Pan is a signed mixer value from -6400 to 6400: 0 is center, negative is left, 6400 is hard right. This differs from channel-rack pan (0–12800, 6400 center); earlier revisions documented the channel scale here, which parked tracks hard right. |
| `fl_markers_list` | None | Lists song time markers with zero-based indices and tick positions. FL extends renders and the play range to the last marker. |
| `fl_marker_delete` | `index` | Deletes one marker by its zero-based index. Remove trailing markers to shorten an audition render; list again afterwards because indices shift. |
| `fl_channel_route` | `channel`, `track` | Routes a zero-based generator to Master (0) or an active ordinary mixer insert. |
| `fl_effect_add` | `track`, `slot`, `name` | Loads an exact installed effect name into slot 0–9, replacing any existing effect in that slot. |
| `fl_parameter_set` | `channelOrTrack`, `slot`, `parameter`, `value` | Sets a discovered parameter to normalized 0–1. `slot=-1` addresses a generator; 0–9 addresses a mixer effect. |

Discover active mixer indices with Python `fl.mixer.list()`. Master is 0; active ordinary inserts are within 1–500. Current/dormant slots are refused. Structural insertion can shift indices: query again before routing or editing effects.

## Python and SDK discovery

| Tool | Inputs | Behavior |
| --- | --- | --- |
| `fl_python_docs` | None | Returns execution conventions, helper classes, naming and result rules, and lifecycle guidance, even before connecting to FL. Names the installed fruitylink wheel up front. |
| `fl_python_api` | `filter=null` | Returns operations, typed arguments, descriptions, and defaults from the shared SDK contract. Matches the optional filter against operation names/descriptions. Wire names stay camelCase; each operation carries `pythonSignature` and each argument/result field carries `pythonName` (snake_case) for use in scripts. |
| `fl_execute_python` | `code`, `timeoutSeconds=60` | Runs trusted Python inside FL with `fl` supplied as a `Studio`. Deadline range: 1–300 seconds; cancellation is cooperative. Exceptions return the captured output, traceback and any partial `result`; responses over `FL_MCP_PYTHON_RESPONSE_LIMIT` are saved under `<workspace>/results/` and summarized with head, tail and path (see [Errors and large results](python.md#errors-and-large-results)). |

Use [the Python guide](python.md) for results, paging, automation, audio analysis, and links to the complete reusable SDK documentation.

## Hearing without a full render

Three tools give an agent audio understanding without waiting for a whole-song render. They run the SDK's `fruitylink.analysis.describe_audio` / `fruitylink.capture` helpers through the same embedded-Python path as `fl_execute_python`, so they need a connected session (`fl_project_start` or `fl_attach`) and the fruitylink build that ships `fl.analysis.describe`, `fl.samples` and `fl.audio` (an older wheel fails with an `AttributeError`/`ImportError` and the error says so). Results share the `fl_execute_python` size bound: a response over `FL_MCP_PYTHON_RESPONSE_LIMIT` is saved under `<workspace>/results/` and summarized with `ok: true`, `oversized: true`, head, tail and path. Nested records keep the SDK's snake_case keys; the fields the server adds are camelCase.

| Tool | Inputs | Behavior |
| --- | --- | --- |
| `fl_audio_describe` | `path=null`, `channel=null`, `detail="normal"`, `compareWith=null` | Describes one WAV as compact text (about 20 lines) plus the same numbers as JSON: level (peak, RMS, crest, integrated LUFS), an ASCII envelope sketch, onsets as `bar:beat` at the project tempo, attack/decay/tail, head and tail silence, spectral segments with centroid, tilt, flatness and seven band levels, tonality with root note, stereo width, loop hints and character tags (`sub-heavy`, `harsh 2-4 kHz`, `bright`, `dark`, `wide`, `clicky attack`, `one-shot`, `loopable`, ...). Exactly one of `path` (an existing `.wav`; relative paths resolve inside the workspace) or `channel` (a zero-based sampler channel whose file this session loaded through `fl_sample_add` / `fl.channels.add_sample`; FL exposes no sampler-file query, so other channels need `path`). `detail` is `brief`, `normal` or `full` (16/32/64 envelope slices, 0/4/8 spectral segments on long audio, 6/8/24 onsets listed). `compareWith` names a baseline WAV; the result then also carries `comparison` (`compare_audio(baseline, subject)`: deltas of the subject minus the baseline for level, decay, centroid, tilt, bands, width, tags added/removed and verdict phrases). Read-only; never edits FL. Returns `source`, `kind`, `channel`, `bpm`, `ppq`, `detail`, `text`, `tags`, `data`, `comparison`, `python_seconds`, `elapsedSeconds`. |
| `fl_audio_capture` | `inserts="master"`, `startBar`, `endBar`, `tailBeats=0`, `name=null`, `armRefresh=true`, `keepOriginals=false` | Records what mixer inserts actually output (post-FX) for bars `startBar..endBar` (one-based, `endBar` inclusive: 33..36 is four bars) with FL's own disk recording: the inserts are armed, song mode is selected, any loop selection is cleared for the pass and restored, the playhead is seeked to `startBar`, record+play run once until the playhead passes `endBar` (+`tailBeats`), the transport stops and the inserts armed here are disarmed; each insert's WAV in FL's recorded-audio folder is then measured offline. `inserts` is `"master"` or an array of mixer track indices (0 = Master). `name` copies the recordings as `<name>-<track>.wav` beside FL's originals and, unless `keepOriginals`, deletes FL's auto-named originals once the copies verify (`removed_originals`); without `name` FL's files are the result. `armRefresh` applies the FL quirk workaround (arm and disarm one extra non-requested insert so FL registers the recording set; a single armed insert otherwise writes no file). Real time: a 16-bar section at 100 BPM plays for 38 s plus about 10 s of overhead; the deadline is the span plus 90 s (120..900). Nothing is rendered and the session stays open, but the pass edits the project: see below. Returns the SDK's `CaptureResult`: `method` (`fl_disk_recording`), `plan` (ticks, seconds, bpm, ppq), `files` (track, name, path, sample_rate, channel_count, frames, seconds), `captured_start_tick`, `captured_end_tick`, `complete`, `stop_tick`, `measurements` per track (`peak_dbfs`, `rms_dbfs`, `integrated_lufs`, `short_term_lufs`, `full_db`, `bands`, per-bar rows with rms/peak/crest/centroid/correlation/bands/lufs, `covered_bars`, `partial`; a failed measurement is `{error: ...}`), `descriptions` (brief `fl_audio_describe` text per WAV), `recorded_folder`, `warnings` (arm-refresh applied or skipped, early FL stop, deadline, disarm failure, cleanup problems), `deleted_clips`, `retired_channels`, `removed_originals`, `python_seconds`, plus `elapsedSeconds` and `timeoutSeconds`. Errors return the SDK's `CaptureError` text verbatim (arm routines unresolved on this FL build, recorded-audio folder missing, no file written because of the recording filter, unmatched files). |
| `fl_section_measure` | `startBar`, `endBar`, `inserts=null`, `prefer="auto"`, `tailBeats=0`, `timeoutSeconds=600` | Measures bars `startBar..endBar` (`endBar` inclusive) by the cheaper of two routes chosen by the SDK's `CapturePolicy`: a live capture (`fl_audio_capture` semantics) for short sections or an offline section render for long ones (live costs the span in real time plus about 8 s; a render about 45 s plus an eighth of the span; live is capped at 120 s of music, so the break-even is about 42 s). `prefer=auto` lets the policy decide; `live` or `render` forces a route. Omit `inserts` to measure the master; an explicit array measures those inserts and forces the live route (a render only yields the master). Returns one record either way: `method` (`live` or `render`) plus `sdkMethod` (the SDK's own name, `fl_disk_recording` or `offline_render`, as `fl.audio.measure_section` reports it), `decision` (both cost estimates and the reason), `inserts`, `bpm`, `ppq`, `files`, `measurements` and `descriptions` per track, `capture` (live: the `fl_audio_capture` record without its measurements, including `deleted_clips`, `retired_channels`, `removed_originals`) or `render` (the `fl_project_render` result) details, `sessionClosed`, `sessionReopenedFrom`, `resumedProject`, `resumedStatus`, `timing` (`totalSeconds` plus `captureSeconds`, or `decideSeconds`/`renderSeconds`/`reopenSeconds`/`measureSeconds`, and `pythonSeconds`, for tuning the policy defaults) and `warnings`. **The render route closes the managed editing session**, exactly as `fl_project_render` does; see below. |

**FL prerequisites for the live route** (`fl_audio_capture`, and `fl_section_measure` when it captures; live-verified on FL 26.1.3): the record button's *Recording filter* (right-click the record button) must include *Audio*, and *mixer menu > Disk recording > Auto-create audio clip* should be off. With the filter wrong FL writes no file and the `CaptureError` names both settings. A capture edits the project even so: FL adds one sample channel and one playlist clip per recording; the SDK deletes the clips (`deleted_clips`) and retires the channels (`retired_channels`: renamed "(unused) ...", muted, routed to Master) because FL has no channel delete, so the retired channels remain in the channel rack. Save a snapshot first if that matters, and expect `fl_channels_list` to grow by one entry per recording.

The render route of `fl_section_measure` runs `fl_project_render(startBar, endBar, cutClips=true, tailBeats)`: the untrimmed project is saved as `<name>-full.flp`, the live project is trimmed to the span, snapshotted and rendered to `measure/<project>-bars<start>-<end>-<stamp>.wav`, which ends the editing session. Because the measurement itself needs the embedded SDK, the tool then reopens the `-full.flp` as a new managed project (`measure/...-resumed.flp`, reported as `resumedProject` with its settled `resumedStatus`) and measures the WAV there, so later calls work in that reopened copy; `sessionClosed: true` and `sessionReopenedFrom` record what happened. If the reopen or the measurement fails, the render result is still returned with `measurements: null` and a `MeasurementUnavailable` or `MeasurementFailed` warning naming the snapshot to resume with `fl_project_start(sourceProjectPath=...)`. Attached (user-owned) sessions are never rendered or closed: the render route is refused for them, so pass `prefer=live`. `timeoutSeconds` is the render deadline per attempt (1..3600); the reopen uses at most 300 s. Python itself never renders: when the SDK's `fl.audio.measure_section` decides on a render it raises `RenderRequired`, which this tool turns into the render route.

A typical loop: `fl_audio_capture(inserts=[5], startBar=33, endBar=40)` to hear what Serum produces after its FX chain, change the chain, capture again with `name`, then `fl_audio_describe(path=<second WAV>, compareWith=<first WAV>)` to read what the change did. `fl_audio_describe(path=...)` also reads Splice samples before `fl_sample_add`, and any `fl_project_render` output.
