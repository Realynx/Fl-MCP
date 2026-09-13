# Getting started

This guide takes you from an installed MCP connection to your first project query, then to a small disposable song. Complete [installation](installation.md) first. The `fl_...` names below are **MCP tools called by your AI app**, not Python functions or shell commands.

## Read an open project

Open FL Studio, enable FL MCP in FruityLink's Plugins menu, and wait for the project to finish loading. Saved and untitled projects both work.

You can give your MCP client this prompt:

```text
Read fl_python_docs. Use fl_instances to discover FL Studio and show me the
available projects. Attach to the project I select, then report its tempo,
PPQ, channels, and patterns. Read only; make no musical edits.
```

The tool sequence is:

1. `fl_python_docs` explains the installed adapter's execution conventions; it works before connecting to FL.
2. `fl_instances` returns discoverable FL processes matching your configured executable.
3. `fl_attach` with the selected `processId` connects to that project's current identity. Expect `ownership: "attached"` and the effective workspace.
4. `fl_status`, `fl_channels_list`, and `fl_patterns_list` report the loaded project. Tempo and PPQ come from FL.

To try the SDK, call `fl_python_api`, then pass this code to `fl_execute_python`:

```python
print(fl.project.info)
result = {
    "tempo": fl.transport.tempo,
    "channels": fl.channels.list(),
}
```

The response separates printed text from `result`. Finish with `fl_detach`; FL stays open with its current project. If discovery is empty, follow [connection troubleshooting](troubleshooting.md#connection-and-discovery).

## Save your first snapshot

While attached, call `fl_project_save` with this argument object:

```json
{"projectPath": "versions/first-inspection.flp"}
```

This creates a new file inside `FL_MCP_WORKSPACE`. It preserves the open project's filename, playback, and song mode. The path must not already exist; use another name on a second run. It is useful to take a snapshot before asking the agent to make edits.

An attached session belongs to you. The companion refuses `fl_project_close` and `fl_project_render` for it. The next walkthrough uses a separate disposable session so the companion can manage rendering.

## Create and render a disposable project

Set `FL_MCP_TEMPLATE` to a saved `.flp` and close all other FL processes. Start with an empty template to make the result easy to inspect. All output paths below are relative to your workspace and must be new.

1. Call `fl_project_start` with `{"projectPath": "sessions/four-beats.flp"}`. Wait for readiness and inspect the returned tempo and PPQ.
2. Call `fl_plugins_list` to discover generators. Choose an exact installed name, and query `fl_playlist_list` to choose a playlist track.
3. Call `fl_python_api` and send the script below to `fl_execute_python`. Replace `GENERATOR` and `TRACK` with values you discovered.

```python
from fruitylink import NoteSpec

GENERATOR = "3x Osc"  # Use an exact name from fl_plugins_list.
TRACK = 1            # Use a track from fl_playlist_list.

fl.transport.tempo = 120
channel = fl.channels.add(GENERATOR, name="MCP melody")
pattern = fl.patterns.create("Four beats")
timebase = fl.timebase
pattern.notes.add([
    NoteSpec(channel.index, key, timebase.ticks(beat), timebase.ticks(0.75), 90)
    for beat, key in enumerate((60, 64, 67, 72))
])
fl.playlist.add_pattern(pattern.index, track=TRACK, start_beats=0, length_beats=4)
fl.transport.song_mode = True
result = {
    "channel": channel.index,
    "pattern": pattern.index,
    "ppq": timebase.ppq,
    "notes": pattern.notes.list(channel=channel.index),
}
```

4. Inspect the returned four notes. If a script partially fails, inspect the channels, pattern, and playlist before retrying: additions append and can duplicate content.
5. Call `fl_project_save` with `{"projectPath": "versions/four-beats-v1.flp"}` to keep a recovery copy.
6. Call `fl_project_render` with `{"outputPath": "audio/four-beats.wav"}`. Rendering saves another snapshot, closes the disposable editor, and waits for FL's WAV export to finish.

The successful render result includes the WAV path and snapshot path. Listen to the file to check the sound. FL's saved export settings determine sample rate and tail handling, and plugin dialogs can require desktop attention. A nonempty WAV is not proof that every external asset loaded correctly.

To keep editing, call `fl_project_start` with a new destination and the workspace-relative saved snapshot:

```json
{
  "projectPath": "sessions/four-beats-v2.flp",
  "sourceProjectPath": "versions/four-beats-v1.flp"
}
```

To finish without a render, use `fl_project_close` with a fresh snapshot path. See [sessions and recovery](sessions.md) for what happens on cancellation or failure.

## Next steps

- Learn [channels, patterns, clips, and timing](concepts.md#music-objects-and-timing).
- Find individual operations in the [MCP tool reference](tools.md).
- Use [Python](python.md) for mixer insertion, linked automation, parameter paging, and offline analysis.
- Check [current capabilities](capabilities.md) before planning a larger workflow.
