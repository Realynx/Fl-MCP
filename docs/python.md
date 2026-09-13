# Python in FL MCP

`fl_execute_python` runs the reusable **FruityLink Python SDK inside FL Studio**. The adapter supplies `fl`, a typed `Studio` object, so you can inspect and edit the connected project without constructing another connection. The private runtime is installed with FL MCP; no system Python or pip setup is required for this workflow.

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
