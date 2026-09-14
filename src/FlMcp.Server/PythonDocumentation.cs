namespace FlMcp.Server;

public static class PythonDocumentation
{
    /// <summary>The conventions text with the installed package named up front.</summary>
    public static string For(string installedPackage) =>
        $"Installed SDK package: {installedPackage}. Its installed surface is authoritative; the fl_python_api catalog\n" +
        "and repository sources can be newer. Inspect fl.ops attributes or the wheel before coding against a new name.\n\n" + Text;

    public const string Text = """
        Embedded Python is the primary way to work with FL MCP. One fl_execute_python request can run
        many operations, keep typed results, and move between managed and attached projects. The
        single-operation MCP tools (notes, clips, mixer, parameters) are conveniences for quick edits,
        not the only route. Use fl_instances then fl_attach(processId) for your current FL project, or
        start a disposable project with fl_project_start. Then call fl_python_api to discover this
        SDK build's operations. The global fl is a Studio with helpers: fl.project, fl.transport,
        fl.channels, fl.patterns (fl.patterns[n].notes), fl.clips / fl.playlist, fl.mixer
        (fl.mixer[t].effects[s].parameters), fl.automation, fl.plugins, fl.analysis, and fl.ops for
        every generated operation. Optional Serum support imports as fruitylink_serum (see below).

        Naming: Python arguments are keyword-only snake_case even though the catalog's wire names are
        camelCase: fl.ops.query_plugin_parameters(channel_or_track=4, slot=-1, offset=199, limit=1).
        Typed query results are dataclasses with snake_case attributes (item.display_value,
        page.next_offset); raw fl.ops.invoke results keep camelCase keys. get_channel_name takes
        index=, other channel getters take channel=. Use fl_python_api's pythonSignature and
        pythonName fields rather than retyping wire names.
        Example: print(fl.ops.get_tempo()); result = fl.ops.query_channels()
        Prefer the SDK's high-level helpers when available; their documentation ships with
        fruitylink-python. The MCP adapter does not maintain a second DAW API. Every native call
        targets the explicitly selected project.

        Results: assign a JSON-compatible value to result; dict keys must be strings (str(index)).
        A raised exception discards the entire result dict, including readings gathered before the
        failure, so wrap risky steps in try/except and record errors inside result. Keep results
        small: write large dumps to a file from inside FL (json.dump) and return a summary.
        Verify writes in a later request: a plugin's display readback can lag several rapid writes to
        the same parameter within one request. Parameter filters are single-token substrings; read
        one parameter with offset=<index>, limit=1 instead of a filter containing spaces.

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

        Normal framework installs select Serum support by default. When installed, use
        from fruitylink_serum import discover_serum_roots, query_index, describe_wav
        without modifying sys.path. query_index(root, text="future bass", limit=12) searches
        Serum 2's local descriptive index; choose a discovered root with System/presets.db.
        describe_wav(path, end_seconds=4) measures supplied audition audio (at most 15 seconds
        and 2 million scalar samples). Metadata search does not load or audition a preset;
        audio traits do not identify oscillator waveforms. If the component was deselected,
        rerun the framework installer to add it. No system Python or pip is needed inside FL.

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

        Song time markers extend renders: FL renders to the later of the last clip end and the last
        marker, so a playlist reduced to one clip still renders to the final marker. The SDK exposes
        list_markers and add_marker but no marker deletion yet; plan audition lengths accordingly.
        Mixer pan: measured readbacks show 0 at centre and 6400 hard right for mixer tracks, while
        channel-rack pan uses 0..12800 with 6400 centre. Confirm the current SDK docstring for
        set_mixer_pan before writing, and verify stereo placement by rendering, not by readback.

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
