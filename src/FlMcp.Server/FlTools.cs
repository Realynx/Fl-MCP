using System.ComponentModel;
using System.Text.Json;
using FlMcp.Protocol;
using ModelContextProtocol.Server;

namespace FlMcp.Server;

[McpServerToolType]
public sealed class FlTools(ManagedSession session)
{
    [McpServerTool(Name = "fl_project_start"), Description("Start a fresh isolated FL project copied from the configured template. Requires no other FL processes; plugin must have been installed and enabled. Returns readiness, tempo and PPQ. Paths are relative to workspace. Default deadline 120 seconds.")]
    public Task<SessionStatus> Start(string projectPath, CancellationToken ct, int timeoutSeconds = 120,
        [Description("Optional existing workspace FLP snapshot to resume; copied to projectPath. Omit for a fresh template project.")] string? sourceProjectPath = null) =>
        session.LaunchAsync(projectPath, timeoutSeconds, ct, sourceProjectPath);

    [McpServerTool(Name = "fl_status", ReadOnly = true), Description("Read managed FL process identity, bridge readiness, project metadata, tempo and PPQ. Timing uses this project's PPQ, never a guessed default.")]
    public Task<JsonElement> Status(CancellationToken ct) => session.CallAsync("status", new { }, ct);

    [McpServerTool(Name = "fl_channels_list", ReadOnly = true), Description("List channel rack indices (zero-based) and names.")]
    public Task<JsonElement> Channels(CancellationToken ct) => session.CallAsync("channels", new { }, ct);

    [McpServerTool(Name = "fl_patterns_list", ReadOnly = true), Description("List patterns with one-based indices and names.")]
    public Task<JsonElement> Patterns(CancellationToken ct) => session.CallAsync("patterns", new { }, ct);

    [McpServerTool(Name = "fl_playlist_list", ReadOnly = true), Description("List playlist track names and indices. Query before placing a clip.")]
    public Task<JsonElement> Playlist(CancellationToken ct) => session.CallAsync("playlist", new { }, ct);

    [McpServerTool(Name = "fl_plugins_list", ReadOnly = true), Description("List installed FL plugins. effects=false lists generators; true lists mixer effects. Choose exact names returned by FL.")]
    public Task<JsonElement> Plugins(CancellationToken ct, bool effects = false) => session.CallAsync("plugins", new CatalogArgs(effects), ct);

    [McpServerTool(Name = "fl_tempo_set"), Description("Set tempo, 10..522 BPM, and return its read-back value.")]
    public Task<JsonElement> Tempo(double bpm, CancellationToken ct) => session.CallAsync("tempo", new TempoArgs(bpm), ct);

    [McpServerTool(Name = "fl_channel_add"), Description("Load an installed generator by its exact catalog name; returns the new zero-based channel index. Plugin licensing or missing content dialogs can require user action.")]
    public Task<JsonElement> Channel(string name, CancellationToken ct) => session.CallAsync("add_channel", new NameArgs(name), ct);

    [McpServerTool(Name = "fl_sample_add"), Description("Add a WAV sample already staged inside the workspace as a new sampler channel. Returns its zero-based index.")]
    public Task<JsonElement> Sample(string path, CancellationToken ct) => session.CallAsync("add_sample", new PathArgs(path), ct);

    [McpServerTool(Name = "fl_pattern_create"), Description("Create and name an empty pattern; returns its one-based index.")]
    public Task<JsonElement> Pattern(string name, CancellationToken ct) => session.CallAsync("create_pattern", new NameArgs(name), ct);

    [McpServerTool(Name = "fl_notes_add"), Description("Append 1..2048 notes to a one-based pattern. Each note uses a zero-based channel, MIDI key 0..127, PPQ startTick >=0, lengthTick >0, velocity 1..127. Query fl_status for PPQ. This appends; retries can duplicate notes.")]
    public Task<JsonElement> Notes(int pattern, Note[] notes, CancellationToken ct) => session.CallAsync("add_notes", new NotesArgs(pattern, notes), ct);

    [McpServerTool(Name = "fl_notes_read", ReadOnly = true), Description("Read pattern notes, optionally by channel (-1=all). Follow FL's continuation offset for paging.")]
    public Task<JsonElement> ReadNotes(int pattern, CancellationToken ct, int channel = -1, int offset = 0) => session.CallAsync("notes", new ReadNotesArgs(pattern, channel, offset), ct);

    [McpServerTool(Name = "fl_clip_add"), Description("Place a pattern in the playlist and select song mode for rendering. Pattern is one-based; track is one-based (1..500), using fl_playlist_list; positions and duration are PPQ ticks. Appends a clip.")]
    public Task<JsonElement> Clip(int pattern, int track, int startTick, int lengthTick, CancellationToken ct) => session.CallAsync("add_clip", new ClipArgs(pattern, track, startTick, lengthTick), ct);

    [McpServerTool(Name = "fl_mixer_set"), Description("Set mixer track volume 0..12800 (about 7624=0 dB), pan 0..12800 (6400=center). Track 0=master, 1..125=inserts.")]
    public Task<JsonElement> Mixer(int track, int volume, CancellationToken ct, int pan = 6400) => session.CallAsync("mixer", new MixerArgs(track, volume, pan), ct);

    [McpServerTool(Name = "fl_channel_route"), Description("Route a zero-based generator channel to mixer track 0..125 (0=master).")]
    public Task<JsonElement> Route(int channel, int track, CancellationToken ct) => session.CallAsync("route", new ChannelRouteArgs(channel, track), ct);

    [McpServerTool(Name = "fl_effect_add", Destructive = true), Description("Load an installed effect by exact name into mixer track 0..125, slot 0..9. Replaces any effect already in the slot.")]
    public Task<JsonElement> Effect(int track, int slot, string name, CancellationToken ct) => session.CallAsync("add_effect", new EffectArgs(track, slot, name), ct);

    [McpServerTool(Name = "fl_parameters_list", ReadOnly = true), Description("List parameter indices and names. slot=-1 addresses a generator channel; 0..9 addresses a mixer effect slot and channelOrTrack is its mixer track.")]
    public Task<JsonElement> Parameters(int channelOrTrack, CancellationToken ct, int slot = -1, string? filter = null) => session.CallAsync("parameters", new ParamListArgs(channelOrTrack, slot, filter), ct);

    [McpServerTool(Name = "fl_parameter_set"), Description("Set a catalogued plugin parameter to normalized 0..1. slot=-1 for a generator, 0..9 for a mixer effect. Discover indices before use.")]
    public Task<JsonElement> Parameter(int channelOrTrack, int slot, int parameter, double value, CancellationToken ct) => session.CallAsync("set_parameter", new ParamArgs(channelOrTrack, slot, parameter, value), ct);

    [McpServerTool(Name = "fl_project_save"), Description("Stop playback, select song mode, and save a verified FLP snapshot inside the workspace without changing project identity. Existing files are refused; use a fresh path.")]
    public Task<object> Save(string projectPath, CancellationToken ct) => session.SaveAsync(projectPath, ct);

    [McpServerTool(Name = "fl_project_close", Destructive = true), Description("Save to a fresh workspace FLP path, verify the snapshot, then terminate only the disposable managed editor. Use MCP request cancellation to interrupt an active launch or render first.")]
    public Task<object> Close(string projectPath, CancellationToken ct) => session.CloseAsync(projectPath, ct);

    [McpServerTool(Name = "fl_project_render", Destructive = true), Description("Save a verified snapshot, end the disposable editing session, and render it to a new WAV using FL's documented CLI. Output must be inside workspace and not exist. Uses FL's saved render settings; requires a working licensed GUI installation. Waits for renderer exit and validates WAV; default deadline 600s (max3600). A successful result includes audio path and snapshot path for other MCPs such as Blender. This closes the managed editing session.")]
    public Task<object> Render(string outputPath, CancellationToken ct, int timeoutSeconds = 600) => session.RenderAsync(outputPath, timeoutSeconds, ct);
}
