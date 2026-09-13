namespace FlMcp.Server;

public static class PythonDocumentation
{
    public const string Text = """
        Use fl_instances then fl_attach(processId) for your current FL project, or start a disposable
        project with fl_project_start. Then call fl_python_api to discover this
        SDK build's operations. fl_execute_python runs the fruitylink-python package embedded
        inside FL Studio's process. The global fl is a Studio; fl.ops.<operation>(snake_case=value)
        invokes an operation from the catalog. Use fl.capabilities() to inspect supported features.
        Example: print(fl.ops.get_tempo()); result = fl.ops.query_channels()
        Prefer the SDK's high-level project/channels/patterns/playlist/mixer/automation helpers when
        available; their documentation ships with fruitylink-python. The MCP adapter does not
        maintain a second DAW API. Every native call targets the explicitly selected project.

        fl.mixer.add(name="New insert") adds an ordinary mixer insert; after=3 inserts after
        track 3 and after=0 inserts after Master. The returned MixerTrack exposes index.
        fl.mixer.list() returns typed Master/active-insert snapshots; Current and dormant slots
        are excluded. Never infer track IDs from the low-level native count. Requery track
        indices and channel routes after insertion. Adding an insert does not
        load an effect or configure a send. Naming is a separate edit; if it fails, inspect
        mixer state before retrying because the insert remains.

        Create linked automation with AutomationTarget and AutomationPointSpec from fruitylink:
        made = fl.automation.create(AutomationTarget.channel_volume(3), track=1,
            start_tick=0, length_tick=1536, name="Volume curve")
        fl.automation[made.channel].set_points([AutomationPointSpec(0, 0.2), AutomationPointSpec(16, 0.8)])
        Target factories support channel volume/pan/pitch, mixer volume/pan, and plugin_parameter
        (slot=-1 generator; 0..9 mixer effect). Playlist tracks are 1..500; curve times are BEATS,
        placement times TICKS. Creation links its initial target; additional target linking is not
        supported. A failed creation may leave a channel or clip: inspect before retrying.

        Offline audio analysis does not change FL or capture live audio. Example:
        audio = fl.analysis.wav(r"C:\Music\render.wav", start_seconds=48, end_seconds=56,
            source_kind="master_render")
        result = audio.summary(loudness=True, true_peak=True)
        Or return audio.windows(window_seconds=0.02, hop_seconds=0.01, limit=32),
        audio.spectral(), or audio.spectral_windows(limit=16). Results report source/frame provenance.
        RMS/energy/crest are per audio channel. PSR uses true-peak estimate minus LUFS over the
        same trailing THREE SECONDS, even for shorter transient RMS windows; shorter context
        returns null. Master audio is not an isolated instrument. True-peak analysis is bounded
        to 2 million scalar samples; summary/loudness to 32 million. Select smaller ranges or pages
        when work limits reject input. Return result pages, not the analysis object or raw PCM.

        Large plugins may expose thousands of parameters. Prefer bounded reads:
        result = fl.channels[0].parameters.page(filter="cutoff", offset=0, limit=32)
        Continue in another request with the returned nextOffset and the same filter/limit until
        nextOffset is null. Offsets count raw slots; an empty filtered page may still continue.
        Parameter iteration is lazy; list() still gathers every page and can exceed result limits.
        find(name) uses native filtering but requires an exact unique name. Return selected fields
        or smaller pages if names/values are unusually long; do not return several full lists.

        Assign result to a JSON-compatible value; print and stderr are captured separately.
        result is bounded to 512 KiB, stdout/stderr to 64 KiB each, and the full response to 1 MiB. Code is limited
        to 128 KiB. The 1..300 second deadline requests cooperative cancellation; Python and
        native calls already in progress must drain before another session operation can proceed.
        Blocking native code may delay cancellation indefinitely. Completed edits are not rolled back.

        fl_execute_python is trusted arbitrary Python with your account's file/network permissions,
        not a sandbox. It shares FL's memory space: unsafe native extensions can crash FL.
        SDK calls use direct managed callbacks; there is no separate worker or Python-to-FL pipe.
        Keep lifecycle in fl_attach/detach/project_start/save/close/render. Python rejects
        new_project, open_project, save_project, save_project_as, save_new_version; save_copy uses
        a new workspace FLP path and sample paths must be staged in the workspace. Batch policy is
        checked before any item runs, but batches are not transactions. Do not switch projects
        manually during execution. Inspect state and use a fresh snapshot path before retrying.

        Attached FL sessions are user-owned: detach/client exit never closes FL, and close/render
        are refused. Snapshot saves preserve playback, song mode and active project filename.
        Untitled projects are supported. Title/path/untitled identity is best effort, not a stable
        project generation ID; concurrent human changes are not transactional. After a detectable
        project switch, fl_status reports requiresReattach and you must fl_attach again.

        Rendering closes the authoring session and returns a WAV path suitable for another MCP,
        such as Blender. Resume a preserved snapshot using fl_project_start's sourceProjectPath.
        FL still needs a licensed Windows installation and an interactive desktop session.
        """;
}
