using System.Text.Json;
using FlMcp.Protocol;
using FruityLink.Core.Abstractions;
using FruityLink.Scripting;

namespace FlMcp.Plugin;

/// <summary>Explicit operation allowlist: only the public typed FruityLink contract crosses IPC.</summary>
public sealed partial class CommandDispatcher : IAsyncDisposable
{
    private readonly FlScriptingDispatcher scripting;
    private readonly WorkspacePaths paths;
    private readonly ScriptingSessionPolicy scriptingPolicy;
    private readonly bool structuredQueries;
    private readonly Dictionary<string, Func<JsonElement, CancellationToken, Task<JsonElement>>> handlers = new(StringComparer.Ordinal);

    public CommandDispatcher(INativeFlControl fl, WorkspacePaths paths,
        Func<Func<string, JsonElement, CancellationToken, Task<object?>>, IEmbeddedPythonRuntime>? pythonFactory = null)
    {
        scripting = new FlScriptingDispatcher(fl);
        structuredQueries = fl is IFlStructuredQuery;
        this.paths = paths;
        scriptingPolicy = new ScriptingSessionPolicy(paths);
        createPython = pythonFactory ?? CreateEmbeddedPython;
        RegisterReads();
        RegisterAuthoring();
        RegisterMixing();
        Register<PathArgs>("save", SaveAsync);
        Register<PathArgs>("snapshot", SnapshotAsync);
        Register<PythonCall>("python_call", PythonCallAsync);
        Register<PythonExecute>("python_execute", PythonExecuteAsync);
    }

    public Task<JsonElement> DispatchAsync(string operation, JsonElement args, CancellationToken ct) =>
        handlers.TryGetValue(operation, out var handler) ? handler(args, ct) :
            throw new ArgumentException($"Unknown bridge operation: {operation}");

    private void RegisterReads()
    {
        RegisterRead("status", StatusAsync);
        RegisterRead("channels", async ct => await InvokeAsync<string>("list_channels", new { }, ct).ConfigureAwait(false));
        RegisterRead("patterns", async ct => await InvokeAsync<string>("list_patterns", new { }, ct).ConfigureAwait(false));
        RegisterRead("playlist", async ct => await InvokeAsync<string>("list_playlist_tracks", new { }, ct).ConfigureAwait(false));
        Register<CatalogArgs>("plugins", async (a, ct) => await InvokeAsync<string>("list_available_plugins", new { effects = a.Effects }, ct).ConfigureAwait(false));
        Register<ReadNotesArgs>("notes", async (a, ct) =>
        {
            Arguments.Range(a.Pattern, 1, 999, "pattern");
            Arguments.Range(a.Channel, -1, 999, "channel");
            Arguments.Range(a.Offset, 0, int.MaxValue, "offset");
            return await InvokeAsync<string>("get_notes", new { pattern = a.Pattern, channel = a.Channel, offset = a.Offset }, ct).ConfigureAwait(false);
        });
        Register<ParamListArgs>("parameters", async (a, ct) =>
        {
            ValidateParameterTarget(a.ChannelOrTrack, a.Slot);
            return await InvokeAsync<string>("list_plugin_params", new { channelOrTrack = a.ChannelOrTrack, slot = a.Slot, filter = a.Filter }, ct).ConfigureAwait(false);
        });
    }

    private async Task<object?> StatusAsync(CancellationToken ct)
    {
        if (!await InvokeAsync<bool>("is_available", new { }, ct).ConfigureAwait(false)) return new SessionStatus(false, Environment.ProcessId, "", 0, 0);
        var project = structuredQueries
            ? await InvokeAsync<FlProjectInfo>("query_project", new { }, ct).ConfigureAwait(false) : null;
        return new SessionStatus(true, Environment.ProcessId,
            await InvokeAsync<string>("get_project_info", new { }, ct).ConfigureAwait(false),
            await InvokeAsync<double>("get_tempo", new { }, ct).ConfigureAwait(false), await InvokeAsync<int>("get_ppq", new { }, ct).ConfigureAwait(false))
            { ProjectPath = project?.Path, ProjectTitle = project?.Title, Untitled = project?.Untitled };
    }

    private void RegisterAuthoring()
    {
        Register<TempoArgs>("tempo", async (a, ct) =>
        {
            Arguments.Range(a.Bpm, 10, 522, "bpm");
            await InvokeTaskAsync("set_tempo", new { bpm = a.Bpm }, ct).ConfigureAwait(false);
            return await InvokeAsync<double>("get_tempo", new { }, ct).ConfigureAwait(false);
        });
        Register<NameArgs>("add_channel", async (a, ct) => await InvokeAsync<int>("add_channel", new { pluginName = Arguments.Name(a.Name) }, ct).ConfigureAwait(false));
        Register<NameArgs>("create_pattern", async (a, ct) =>
        {
            var name = Arguments.Name(a.Name);
            var index = await InvokeAsync<int>("create_pattern", new { }, ct).ConfigureAwait(false);
            ct.ThrowIfCancellationRequested();
            await InvokeTaskAsync("set_pattern_name", new { index, name }, ct).ConfigureAwait(false);
            return index;
        });
        Register<NotesArgs>("add_notes", async (a, ct) =>
        {
            Arguments.Notes(a);
            await InvokeTaskAsync("add_notes", new { pattern = a.Pattern, notes = a.Notes }, ct).ConfigureAwait(false);
            return new { added = a.Notes.Length };
        });
        Register<ClipArgs>("add_clip", async (a, ct) =>
        {
            Arguments.Range(a.Pattern, 1, 999, "pattern");
            Arguments.Range(a.Track, 1, 500, "track");
            Arguments.Range(a.StartTick, 0, int.MaxValue - 1, "startTick");
            Arguments.Range(a.LengthTick, 1, int.MaxValue - a.StartTick, "lengthTick");
            await InvokeTaskAsync("add_pattern_clip", new { pattern = a.Pattern, track = a.Track, startTick = a.StartTick, lengthTick = a.LengthTick }, ct).ConfigureAwait(false);
            ct.ThrowIfCancellationRequested();
            await InvokeTaskAsync("set_song_mode", new { song = true }, ct).ConfigureAwait(false);
            return new { placed = true };
        });
        Register<PathArgs>("add_sample", async (a, ct) =>
        {
            var path = paths.Resolve(a.Path, ".wav");
            if (!File.Exists(path)) throw new FileNotFoundException("Stage the WAV sample inside the workspace.", path);
            return await InvokeAsync<int>("add_sample_channel", new { samplePath = path }, ct).ConfigureAwait(false);
        });
    }

    private void RegisterMixing()
    {
        Register<MixerArgs>("mixer", async (a, ct) =>
        {
            Arguments.Range(a.Track, 0, Arguments.MaximumMixerTrack, "track");
            Arguments.Range(a.Volume, 0, 12800, "volume");
            Arguments.Range(a.Pan, 0, 12800, "pan");
            await InvokeTaskAsync("set_mixer_volume", new { track = a.Track, value = a.Volume }, ct).ConfigureAwait(false);
            ct.ThrowIfCancellationRequested();
            await InvokeTaskAsync("set_mixer_pan", new { track = a.Track, value = a.Pan }, ct).ConfigureAwait(false);
            return new { updated = true };
        });
        Register<ChannelRouteArgs>("route", async (a, ct) =>
        {
            Arguments.Range(a.Channel, 0, 999, "channel");
            Arguments.Range(a.Track, 0, Arguments.MaximumMixerTrack, "track");
            await InvokeTaskAsync("set_channel_fx_route", new { channel = a.Channel, mixerTrack = a.Track }, ct).ConfigureAwait(false);
            return new { routed = true };
        });
        Register<EffectArgs>("add_effect", async (a, ct) =>
        {
            Arguments.Range(a.Track, 0, Arguments.MaximumMixerTrack, "track");
            Arguments.Range(a.Slot, 0, 9, "slot");
            await InvokeTaskAsync("add_mixer_effect", new { track = a.Track, slot = a.Slot, pluginName = Arguments.Name(a.Name) }, ct).ConfigureAwait(false);
            return new { loaded = true };
        });
        Register<ParamArgs>("set_parameter", async (a, ct) =>
        {
            ValidateParameterTarget(a.ChannelOrTrack, a.Slot);
            Arguments.Range(a.Parameter, 0, 65535, "parameter");
            Arguments.Range(a.Value, 0, 1, "value");
            await InvokeTaskAsync("set_plugin_param", new { channelOrTrack = a.ChannelOrTrack, slot = a.Slot, paramIndex = a.Parameter, value = a.Value }, ct).ConfigureAwait(false);
            return new { updated = true };
        });
    }

    private async Task<object?> SaveAsync(PathArgs args, CancellationToken ct)
    {
        var path = paths.NewFile(args.Path, ".flp");
        await InvokeTaskAsync("transport_stop", new { }, ct).ConfigureAwait(false);
        ct.ThrowIfCancellationRequested();
        await InvokeTaskAsync("set_song_mode", new { song = true }, ct).ConfigureAwait(false);
        ct.ThrowIfCancellationRequested();
        await InvokeTaskAsync("save_copy", new { path }, ct).ConfigureAwait(false);
        return new { path };
    }

    private async Task<object?> SnapshotAsync(PathArgs args, CancellationToken ct)
    {
        var path = paths.NewFile(args.Path, ".flp");
        await InvokeTaskAsync("save_copy", new { path }, ct).ConfigureAwait(false);
        return new { path };
    }

    private static void ValidateParameterTarget(int target, int slot)
    {
        Arguments.Range(slot, -1, 9, "slot");
        Arguments.Range(target, 0, slot == -1 ? 999 : Arguments.MaximumMixerTrack, "channelOrTrack");
    }

    private void Register<T>(string operation, Func<T, CancellationToken, Task<object?>> handler) =>
        handlers.Add(operation, async (args, ct) => Messages.Element(await handler(Messages.Arguments<T>(args), ct).ConfigureAwait(false)));

    private void RegisterRead(string operation, Func<CancellationToken, Task<object?>> handler) =>
        handlers.Add(operation, async (_, ct) => Messages.Element(await handler(ct).ConfigureAwait(false)));

    private async Task<T> InvokeAsync<T>(string operation, object arguments, CancellationToken ct) =>
        (T)(await scripting.InvokeAsync(operation, Messages.Element(arguments), ct).ConfigureAwait(false))!;

    private Task<object?> InvokeTaskAsync(string operation, object arguments, CancellationToken ct) =>
        scripting.InvokeAsync(operation, Messages.Element(arguments), ct);

}
