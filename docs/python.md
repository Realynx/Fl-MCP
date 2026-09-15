# Python in FL MCP

`fl_execute_python` runs the reusable **FruityLink Python SDK inside FL Studio**. The adapter supplies `fl`, a typed `Studio` object, so you can inspect and edit the connected project without constructing another connection. The private runtime is installed with FL MCP; no system Python or pip setup is required for this workflow.

**Embedded Python is the primary way to work with FL MCP.** One request can run dozens of operations, keep typed results, and move between managed and attached projects. The single-operation MCP tools (`fl_notes_add`, `fl_clip_add`, `fl_mixer_set`, `fl_parameter_set`, …) are conveniences for quick edits, not the only route. The helper classes are `fl.project`, `fl.transport`, `fl.channels` (`fl.channels[i].parameters`), `fl.patterns` (`fl.patterns[n].notes`), `fl.clips` / `fl.playlist`, `fl.mixer` (`fl.mixer[t].effects[s].parameters`), `fl.automation`, `fl.plugins`, `fl.analysis` (offline WAV analysis and describe/compare), `fl.samples`, `fl.audio` (live capture and the section policy), and `fl.ops` for every generated operation. The optional `fruitylink_serum` extension adds preset inventory and audition measurement (see [Serum support](#serum-support)).

`fl_python_docs` names the installed fruitylink wheel first. That installed surface is authoritative: the `fl_python_api` catalog and the SDK repository can be newer than the wheel FL loads, so check `dir(fl.ops)` or the wheel before coding against a name you have not used yet.

## Naming and result rules

- Python arguments are keyword-only **snake_case** even though the catalog's wire names are camelCase: `fl.ops.query_plugin_parameters(channel_or_track=4, slot=-1, offset=199, limit=1)`. `fl_python_api` returns `pythonSignature` per operation and `pythonName` per argument and result field, so you never have to convert names yourself. `get_channel_name` takes `index=`; the other channel getters take `channel=`.
- Typed `query_*` results are dataclasses with snake_case attributes (`item.display_value`, `page.next_offset`). Raw `fl.ops.invoke(...)` results keep camelCase keys.
- `result` must be JSON-compatible and dict keys must be strings (`str(index)`).
- A raised exception returns `ok:false` with the traceback, the output captured before the failure, and whatever `result` already held (`resultPartial:true`). Build `result` incrementally so a late failure still reports earlier readings; see [Errors and large results](#errors-and-large-results).
- Keep results small. A response over `FL_MCP_PYTHON_RESPONSE_LIMIT` (default 64 KiB) is saved under `<workspace>/results/` and summarized with its head, tail and path; page queries or return summaries when you can.
- Verify writes in a later request. A plugin's display readback can lag several rapid writes to the same parameter within one request; a single write followed by a read in the next request was consistent.
- Parameter `filter` values are single-token substrings. Read one parameter with `offset=<index>, limit=1` instead of a filter containing spaces.
- Song time markers extend renders: FL renders to the later of the last clip end and the last marker. Use `fl_markers_list` / `fl_marker_delete` (or `fl.ops.delete_marker(index=...)` on SDK builds that expose it) to shorten an audition.
- Mixer pan is a signed value: `set_mixer_pan(track, value)` takes -6400..6400 with 0 at centre and 6400 hard right. Channel-rack pan stays 0..12800 with 6400 at centre. Verify stereo placement by rendering, not by readback.
- Volume scales: mixer track volume is raw 0..16000 with 12800 (fader 0.8, every insert's default) = 0 dB and 16000 = +5.6 dB; channel-rack volume is raw 0..12800 with 10240 = 0 dB (FL's default 10000 is about -0.6 dB); send levels are 0..1 with 0.8 = 0 dB. The fader law is not linear in dB (the SDK models dB = 20 × 2.889 × log10(position / 0.8); mixer 6400 is about -17 dB, values below about -20 dB are ±3 dB estimates). Write dB rather than raw guesses: `fl.mixer[t].set_volume(db=-6.0)`, `fl.mixer[t].volume_db`, `fl.channels[i].set_volume(db=-3.0)`, `fl.mixer[t].send_to(dest, db=-6.0)`, or convert with `fruitylink.levels.mixer_volume_from_db` / `mixer_volume_to_db` / `channel_volume_from_db` / `channel_volume_to_db` / `send_level_from_db`.
- Hearing without a full render: `fl.analysis.describe(path, bpm=..., ppq=...)` returns an `AudioDescription` whose `.text` an agent can read (level, envelope sketch, onsets as `bar:beat`, decay, silence, spectral segments, tonality, width, loop hints, tags); `fl.analysis.compare(a, b)` reports `b` minus `a`; `fl.samples.describe(channel)` describes a sampler channel's file when this session loaded it; `fl.audio.capture(inserts, start_bar, end_bar)` records the inserts' post-FX output with FL's disk recording; `fl.audio.measure_section(start_bar, end_bar)` applies the live-vs-render policy and raises `RenderRequired` instead of rendering. The MCP tools `fl_audio_describe`, `fl_audio_capture` and `fl_section_measure` wrap these (see [the tool reference](tools.md#hearing-without-a-full-render)); only `fl_section_measure` renders.

The complete SDK belongs to FruityLink. Use its [Python overview](https://github.com/Realynx/FL-Automation/blob/master/docs/python/index.md), [API guide](https://github.com/Realynx/FL-Automation/blob/master/docs/python/api.md), and [examples](https://github.com/Realynx/FL-Automation/blob/master/docs/python/examples.md) for reusable DAW behavior. This page explains the MCP host's execution model and restrictions.

## First script

1. Read `fl_python_docs` in your MCP client.
2. Connect with `fl_attach` or `fl_project_start`.
3. Call `fl_python_api` to discover the installed SDK contract. Use `filter` to narrow it, for example `{"filter": "tempo"}`.
4. Pass this code to `fl_execute_python`:

```python
print(fl.project.info)
result = {
    "tempo": fl.transport.tempo,
    "channels": fl.channels.list(),
}
```

Assign the value you want returned to `result`. Printed output and stderr are captured separately. Prefer small dictionaries, lists, and scalar values; the host converts supported SDK return values to JSON-compatible output. Return selected fields or pages rather than a large collection or a live SDK helper object.

Discover native capabilities with `fl.capabilities()`. High-level helpers cover `fl.project`, `fl.transport`, `fl.channels`, `fl.patterns`, `fl.playlist`, `fl.mixer`, and `fl.automation`. Generated operations are available through `fl.ops.<operation>(snake_case=value)`; use `fl_python_api` for exact names, types, and defaults.

## Author a pattern

The [getting-started walkthrough](getting-started.md#create-and-render-a-disposable-project) creates four notes, places a playlist clip, and renders a WAV. The repository also includes [examples/author.py](https://github.com/Realynx/Fl-MCP/blob/master/examples/author.py).

Use `fl.timebase` to convert beats to ticks. MCP note/clip tools use ticks, while some Python convenience methods accept beats. Discover channel, pattern, playlist, and mixer indices instead of assuming they are fixed.

## Read large parameter sets

Some plugins expose thousands of parameters. After discovering the intended generator channel, return one page:

```python
# Replace 0 with the intended channel's discovered index.
result = fl.channels[0].parameters.page(filter="cutoff", offset=0, limit=32)
```

Continue with the returned `nextOffset` and the same filter/limit until it is `null`. Offsets count raw parameter slots, so an empty filtered page may still have a continuation. Iteration is lazy, but converting everything to a list gathers every page and can exceed the response limit. `find(name)` requires an exact, unique parameter name.

## Errors and large results

A successful run returns `{ok:true, result, stdout, stderr, stdoutTruncated, stderrTruncated}`; that shape is unchanged.

**Exceptions keep partial work.** When the script raises, the response is `ok:false` with `error` and `traceback`, the `stdout`/`stderr` captured up to the failure, and the value `result` held at that moment under `result` with `resultPartial:true` (or `resultPartialError` when it could not be serialized). Assign readings to `result` as you go rather than wrapping every step in `try`/`except`:

```json
{"ok": false, "result": {"tempo": 140.0, "channels": 6}, "resultPartial": true,
 "error": "KeyError: 'Serum'", "traceback": "Traceback (most recent call last):\n  File \"<fruitylink-embedded>\", line 4 ...",
 "stdout": "tempo read\n", "stderr": "", "stdoutTruncated": false, "stderrTruncated": false}
```

**Large responses are saved, not lost.** The SDK caps each captured stream at 64 KiB and `result` at 512 KiB (a larger `result` fails with `ok:false` and the streams intact). On top of that, the server keeps a tool response within `FL_MCP_PYTHON_RESPONSE_LIMIT` bytes (default 65536, minimum 4096) so it fits the MCP client's display budget. A response over the limit is written as indented JSON to `<workspace>/results/<yyyyMMdd-HHmmss>-<id>.json` and replaced by:

```json
{"ok": true, "oversized": true, "totalBytes": 412903, "limitBytes": 65536,
 "path": "C:\\Users\\me\\AppData\\Local\\FlMcp\\Projects\\results\\20260914-153000-3f2a9c1d.json",
 "head": "{\n  \"ok\": true,\n  \"result\": [\n    {\n      \"index\": 0, ...",
 "tail": "...\n  \"stdoutTruncated\": false,\n  \"stderrTruncated\": false\n}",
 "note": "The response is 412903 bytes, over the 65536-byte FL_MCP_PYTHON_RESPONSE_LIMIT. ..."}
```

`head` holds the first quarter of the limit and `tail` the last eighth; `error`, `resultPartial`, `stdoutTruncated` and `stderrTruncated` are carried over when present. Read the file with your client's file tool for the rest, or return a page or summary next time. The files are ordinary workspace artifacts and are never deleted automatically.

## Serum support

The framework installer selects the separate Serum support component by default.
After installation and restarting FL, its Python package imports directly:

```python
from dataclasses import asdict
from fruitylink_serum import discover_serum_roots, query_index

roots = discover_serum_roots()
root = next(p for p in roots if (p / "System/presets.db").is_file())
result = [asdict(p) for p in query_index(root, text="future bass", limit=12)]
```

For a supplied isolated audition WAV, call
`fruitylink_serum.describe_wav(path, end_seconds=4)`. Results include amplitude,
spectrum, envelope, and stereo evidence. Each call is bounded to 15 seconds and
two million scalar samples. Use the same note/chord, velocity, and effects policy
when comparing patches. A full mix cannot identify an individual instrument's
waveform, and preset descriptions are not evidence of how a patch sounds.

Preset loading remains a separate capability under development. This package
does not translate a catalog match into changes to a live Serum instance. If the
component was deselected, rerun the framework installer to add it; no system
Python or pip installation is required for embedded use.

## Use more of the SDK

For an ordinary mixer insert, run:

```python
insert = fl.mixer.add(name="New insert")
result = {"insert": insert.index, "tracks": fl.mixer.list()}
```

`after=3` inserts after track 3; `after=0` inserts after Master. Requery tracks and channel routes after insertion because indices can shift. Adding an insert does not load an effect or configure a send. Naming is a separate edit, so inspect the mixer before retrying a failure.

For linked channel-volume automation, first choose an existing channel and playlist track. This example makes a four-beat curve:

```python
from fruitylink import AutomationPointSpec, AutomationTarget

CHANNEL = 0  # Replace with a discovered channel index.
TRACK = 1    # Replace with a discovered playlist track.
made = fl.automation.create(
    AutomationTarget.channel_volume(CHANNEL),
    track=TRACK,
    start_tick=0,
    length_tick=fl.timebase.ticks(4),
    name="Volume rise",
)
fl.automation[made.channel].set_points([
    AutomationPointSpec(0, 0.2),
    AutomationPointSpec(4, 0.8),
])
result = {"automation_channel": made.channel}
```

Placement uses **ticks**; curve points use **beats**. Target factories include channel volume/pan/pitch, mixer volume/pan, and plugin parameters. Creation establishes the initial target link; adding further target links is not supported. A failed creation may leave a channel or clip: inspect before retrying.

## Analyze a rendered file

Audio analysis measures supplied WAV data. It does not record live channels. Pass an actual returned render path and identify what the audio represents:

```python
audio = fl.analysis.wav(
    r"C:\Music\McpProjects\audio\four-beats.wav",  # Replace with your WAV path.
    start_seconds=0,
    end_seconds=1,
    source_kind="master_render",
)
result = audio.summary(loudness=True, true_peak=True)
```

`fl_project_render` ends the disposable session. To analyze its file through `fl_execute_python`, first reconnect or start another disposable session; alternatively use the SDK's standalone analysis path from a Python application. The one-second example is deliberately small; PSR is `null` when a full three-second context is unavailable.

You can also return `audio.windows(window_seconds=0.02, hop_seconds=0.01, limit=32)`, `audio.spectral()`, or `audio.spectral_windows(limit=16)`. Results include source/frame provenance. Return result pages, not raw PCM or the analysis object.

RMS, energy, and crest are measured per audio channel. True peak is labelled an estimate. PSR compares estimated true peak to loudness over the same trailing three seconds; it is not peak minus RMS. A master render contains the complete mix, and a mixer stem can contain multiple instruments and sends.

## Execution and results

| Bound | Current value |
| --- | --- |
| Code size | 128 KiB |
| `result` size | 512 KiB |
| Captured stdout / stderr | 64 KiB each |
| Complete response | 1 MiB |
| Python deadline | 60 seconds by default; 1–300 seconds accepted |
| True-peak analysis work | 2 million scalar samples |
| Summary/loudness work | 32 million scalar samples |

Choose smaller ranges or pages when an output/work limit rejects a request. Cancellation is cooperative: Python checks it at script entry, SDK-call boundaries, and periodically while tracing execution. Blocking native calls and unfinished child threads can delay cleanup. Completed edits remain, and batches are not transactions.

Python runs with your account's file/network permissions in FL's memory space. Unsafe native extensions can crash FL. Use trusted scripts, save snapshots, and inspect state after a failure.

## Keep lifecycle in MCP

Use `fl_project_start`, `fl_project_save`, `fl_project_close`, and `fl_project_render` for project management. The adapter refuses Python operations `new_project`, `open_project`, `save_project`, `save_project_as`, and `save_new_version`. Python `save_copy` requires a fresh workspace `.flp`, and native sample operations require existing workspace files.

These are SDK-call policies, not a general Python sandbox. Batch policy is checked before any batch item executes, but errors or cancellation do not roll back completed work. See [sessions and recovery](sessions.md#boundaries-and-recovery).

For Python outside MCP, follow the [standalone SDK getting-started guide](https://github.com/Realynx/FL-Automation/blob/master/docs/python/getting-started.md) and [execution guide](https://github.com/Realynx/FL-Automation/blob/master/docs/python/execution.md). C# plugin authors should start with the [C# SDK](https://github.com/Realynx/FL-Automation/blob/master/docs/csharp/index.md).
