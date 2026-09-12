using System.Text.Json;
using FlMcp.Protocol;
using FruityLink.Core.Abstractions;

namespace FlMcp.Plugin;

/// <summary>Explicit operation allowlist: only the public typed FruityLink contract crosses IPC.</summary>
public sealed class CommandDispatcher
{
    private readonly INativeFlControl fl;
    private readonly WorkspacePaths paths;
    private readonly Dictionary<string, Func<JsonElement, CancellationToken, Task<JsonElement>>> handlers = new(StringComparer.Ordinal);

    public CommandDispatcher(INativeFlControl fl, WorkspacePaths paths)
    {
        this.fl = fl;
        this.paths = paths;
        RegisterReads();
        RegisterAuthoring();
        RegisterMixing();
        Register<PathArgs>("save", SaveAsync);
    }

    public Task<JsonElement> DispatchAsync(string operation, JsonElement args, CancellationToken ct) =>
        handlers.TryGetValue(operation, out var handler) ? handler(args, ct) :
            throw new ArgumentException($"Unknown bridge operation: {operation}");

    private void RegisterReads()
    {
        RegisterRead("status", StatusAsync);
        RegisterRead("channels", async ct => await fl.ListChannelsAsync(ct).ConfigureAwait(false));
        RegisterRead("patterns", async ct => await fl.ListPatternsAsync(ct).ConfigureAwait(false));
        RegisterRead("playlist", async ct => await fl.ListPlaylistTracksAsync(ct).ConfigureAwait(false));
        Register<CatalogArgs>("plugins", async (a, ct) => await fl.ListAvailablePluginsAsync(a.Effects, ct).ConfigureAwait(false));
        Register<ReadNotesArgs>("notes", async (a, ct) =>
        {
            Arguments.Range(a.Pattern, 1, 999, "pattern");
            Arguments.Range(a.Channel, -1, 999, "channel");
            Arguments.Range(a.Offset, 0, int.MaxValue, "offset");
            return await fl.GetNotesAsync(a.Pattern, a.Channel, a.Offset, ct).ConfigureAwait(false);
        });
        Register<ParamListArgs>("parameters", async (a, ct) =>
        {
            ValidateParameterTarget(a.ChannelOrTrack, a.Slot);
            return await fl.ListPluginParamsAsync(a.ChannelOrTrack, a.Slot, a.Filter, ct).ConfigureAwait(false);
        });
    }

    private async Task<object?> StatusAsync(CancellationToken ct)
    {
        if (!await fl.IsAvailableAsync(ct).ConfigureAwait(false)) return new SessionStatus(false, Environment.ProcessId, "", 0, 0);
        return new SessionStatus(true, Environment.ProcessId,
            await fl.GetProjectInfoAsync(ct).ConfigureAwait(false),
            await fl.GetTempoAsync(ct).ConfigureAwait(false), await fl.GetPpqAsync(ct).ConfigureAwait(false));
    }

    private void RegisterAuthoring()
    {
        Register<TempoArgs>("tempo", async (a, ct) =>
        {
            Arguments.Range(a.Bpm, 10, 522, "bpm");
            await fl.SetTempoAsync(a.Bpm, ct).ConfigureAwait(false);
            return await fl.GetTempoAsync(ct).ConfigureAwait(false);
        });
        Register<NameArgs>("add_channel", async (a, ct) => await fl.AddChannelAsync(Arguments.Name(a.Name), ct).ConfigureAwait(false));
        Register<NameArgs>("create_pattern", async (a, ct) =>
        {
            var name = Arguments.Name(a.Name);
            var index = await fl.CreatePatternAsync(ct).ConfigureAwait(false);
            ct.ThrowIfCancellationRequested();
            await fl.SetPatternNameAsync(index, name, ct).ConfigureAwait(false);
            return index;
        });
        Register<NotesArgs>("add_notes", async (a, ct) =>
        {
            Arguments.Notes(a);
            await fl.AddNotesAsync(a.Pattern, a.Notes.Select(n => new NoteSpec(n.Channel, n.Key, n.StartTick, n.LengthTick, n.Velocity)).ToArray(), ct).ConfigureAwait(false);
            return new { added = a.Notes.Length };
        });
        Register<ClipArgs>("add_clip", async (a, ct) =>
        {
            Arguments.Range(a.Pattern, 1, 999, "pattern");
            Arguments.Range(a.Track, 1, 500, "track");
            Arguments.Range(a.StartTick, 0, int.MaxValue - 1, "startTick");
            Arguments.Range(a.LengthTick, 1, int.MaxValue - a.StartTick, "lengthTick");
            await fl.AddPatternClipAsync(a.Pattern, a.Track, a.StartTick, a.LengthTick, ct).ConfigureAwait(false);
            ct.ThrowIfCancellationRequested();
            await fl.SetSongModeAsync(true, ct).ConfigureAwait(false);
            return new { placed = true };
        });
        Register<PathArgs>("add_sample", async (a, ct) =>
        {
            var path = paths.Resolve(a.Path, ".wav");
            if (!File.Exists(path)) throw new FileNotFoundException("Stage the WAV sample inside the workspace.", path);
            return await fl.AddSampleChannelAsync(path, ct).ConfigureAwait(false);
        });
    }

    private void RegisterMixing()
    {
        Register<MixerArgs>("mixer", async (a, ct) =>
        {
            Arguments.Range(a.Track, 0, 125, "track");
            Arguments.Range(a.Volume, 0, 12800, "volume");
            Arguments.Range(a.Pan, 0, 12800, "pan");
            await fl.SetMixerVolumeAsync(a.Track, a.Volume, ct).ConfigureAwait(false);
            ct.ThrowIfCancellationRequested();
            await fl.SetMixerPanAsync(a.Track, a.Pan, ct).ConfigureAwait(false);
            return new { updated = true };
        });
        Register<ChannelRouteArgs>("route", async (a, ct) =>
        {
            Arguments.Range(a.Channel, 0, 999, "channel");
            Arguments.Range(a.Track, 0, 125, "track");
            await fl.SetChannelFxRouteAsync(a.Channel, a.Track, ct).ConfigureAwait(false);
            return new { routed = true };
        });
        Register<EffectArgs>("add_effect", async (a, ct) =>
        {
            Arguments.Range(a.Track, 0, 125, "track");
            Arguments.Range(a.Slot, 0, 9, "slot");
            await fl.AddMixerEffectAsync(a.Track, a.Slot, Arguments.Name(a.Name), ct).ConfigureAwait(false);
            return new { loaded = true };
        });
        Register<ParamArgs>("set_parameter", async (a, ct) =>
        {
            ValidateParameterTarget(a.ChannelOrTrack, a.Slot);
            Arguments.Range(a.Parameter, 0, 65535, "parameter");
            Arguments.Range(a.Value, 0, 1, "value");
            await fl.SetPluginParamAsync(a.ChannelOrTrack, a.Slot, a.Parameter, a.Value, ct).ConfigureAwait(false);
            return new { updated = true };
        });
    }

    private async Task<object?> SaveAsync(PathArgs args, CancellationToken ct)
    {
        var path = paths.NewFile(args.Path, ".flp");
        await fl.TransportStopAsync(ct).ConfigureAwait(false);
        ct.ThrowIfCancellationRequested();
        await fl.SetSongModeAsync(true, ct).ConfigureAwait(false);
        ct.ThrowIfCancellationRequested();
        await fl.SaveCopyAsync(path, ct).ConfigureAwait(false);
        return new { path };
    }

    private static void ValidateParameterTarget(int target, int slot)
    {
        Arguments.Range(slot, -1, 9, "slot");
        Arguments.Range(target, 0, slot == -1 ? 999 : 125, "channelOrTrack");
    }

    private void Register<T>(string operation, Func<T, CancellationToken, Task<object?>> handler) =>
        handlers.Add(operation, async (args, ct) => Messages.Element(await handler(Messages.Arguments<T>(args), ct).ConfigureAwait(false)));

    private void RegisterRead(string operation, Func<CancellationToken, Task<object?>> handler) =>
        handlers.Add(operation, async (_, ct) => Messages.Element(await handler(ct).ConfigureAwait(false)));
}
