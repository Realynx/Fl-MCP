# How FL MCP works

Think of FL MCP as a connection to **one explicitly selected FL project**. The tools operate on that project, using the same FruityLink SDK available to C# plugins and Python applications.

## Four pieces

```text
Your AI app
  │  MCP tool requests over stdio
  ▼
FL MCP companion process
  │  Local, same-user Windows named pipe
  ▼
FL MCP plugin inside FL Studio
  │  FruityLink SDK calls / embedded Python
  ▼
Your selected FL project
```

The **AI app** chooses tools and presents results. The **companion** handles session ownership, workspace paths, snapshots, and the render process. The **plugin** connects the requests to the **FruityLink SDK** running inside FL. There is no network listener or separate Python worker in this path.

Python submitted through MCP runs in FL Studio's own process using private bundled CPython. The supplied `fl` object is the SDK's typed `Studio`. This is trusted code with your account's permissions; the workspace rules protect SDK operations and output paths, but they do not sandbox arbitrary Python.

## Two kinds of session

| | Attached session | Disposable session |
| --- | --- | --- |
| Start | Discover with `fl_instances`, select with `fl_attach(processId)` | `fl_project_start(projectPath)` copies a template or workspace snapshot |
| Who owns FL? | You | The companion that launched this process |
| What project? | Your current saved or untitled project | A fresh workspace copy |
| Save | New snapshot; active filename, playback, and song mode stay as they were | New snapshot; playback stops and song mode is selected |
| Finish | `fl_detach` leaves FL open | `fl_project_close` saves and ends the disposable editor |
| Render | Save, detach, close FL yourself, then start a disposable copy | `fl_project_render` saves, ends the editor, and runs WAV export |

A disposable project is a working copy, not a promise that edits vanish. Saved snapshots and audio remain in the workspace. FL MCP refuses to overwrite existing output files, so use a fresh name for every save or render.

Attachment is deliberate. A detected title/path/untitled change makes the connection require reattachment. FL has no reliable project generation ID, so identical untitled replacements and concurrent human changes cannot always be detected. See [session identity and recovery](sessions.md#assist-your-current-project).

## Music objects and timing

| Object | Meaning | Index/unit convention |
| --- | --- | --- |
| Channel | A generator or sample in the channel rack | Zero-based |
| Pattern | A collection of notes for channels | One-based |
| Note | A pitched event within a pattern | MIDI key 0–127; timing in ticks for MCP tools |
| Playlist clip | Places a pattern in the song arrangement | Playlist track 1–500; position/duration in ticks |
| Mixer track | Master or an active ordinary insert | Master is 0; discover inserts with `fl.mixer.list()` |
| Effect slot | An effect position on a mixer track | 0–9; loading replaces the occupied slot |
| Plugin parameter | A discoverable control on a generator/effect | Discover its index; MCP setting uses normalized 0–1 |
| Automation curve | Values linked to a parameter over time | Curve points use beats; placement uses ticks |

**PPQ means ticks per quarter note.** Query `fl_status` or `fl.timebase` for the loaded project's value. At PPQ 96, one quarter note is 96 ticks and a 4/4 bar is 384 ticks. At another PPQ those tick values change. Use the Python timebase helpers when expressing musical beats.

Notes inside a pattern do not automatically arrange a song. Add a playlist clip to put the pattern on the timeline. Requery indices after structural edits such as inserting a mixer track: index values are positions, not permanent identifiers.

## Tools versus Python

Use the [27 MCP tools](tools.md) for common edits and all project lifecycle actions. Use [embedded Python](python.md) for loops, typed helpers, automation, and broader SDK capabilities. Call `fl_python_api` to discover the installed build's actual operation contract, then `fl.capabilities()` to inspect supported features.

FL MCP owns the lifecycle rules. The SDK owns DAW behavior. Keep start/save/close/render in the MCP tools; do not use Python to switch the project under the active connection. A batch can partially apply, and cancellation does not roll back completed edits.

Continue with [getting started](getting-started.md) or the detailed [session reference](sessions.md).
