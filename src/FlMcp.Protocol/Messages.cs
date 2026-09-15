using System.Text.Json;

namespace FlMcp.Protocol;

public sealed record BridgeRequest(string Token, string Operation, JsonElement Arguments, int TimeoutSeconds = 30)
{
    public string? LeaseToken { get; init; }
}
public sealed record BridgeResponse(JsonElement? Result, string? Error = null);
public sealed record SessionStatus(bool Available, int ProcessId, string Project, double Tempo, int Ppq)
{
    public string? ProjectPath { get; init; }
    public string? ProjectTitle { get; init; }
    public bool? Untitled { get; init; }
    public string? Ownership { get; init; }
    public bool RequiresReattach { get; init; }
    public IReadOnlyList<SessionWarning> Warnings { get; init; } = [];
    /// <summary>Set only by a managed launch: how tempo, PPQ and title behaved after the bridge first reported the project.</summary>
    public ProjectSettle? Settle { get; init; }
}
public sealed record SessionWarning(string Code, string Message, string OriginalProject, string Diagnostic);
/// <summary>FL can report the copied project path before it has applied that project's tempo, so a launch keeps
/// polling until tempo, PPQ and title stay unchanged for a window. Stable=false means the bounded wait ran out.</summary>
public sealed record ProjectSettle(bool Stable, int Milliseconds, int Polls, double FirstTempo);
/// <summary>The song as FL's exporter sees it: a render runs from bar 1 to the later of the last clip end and the
/// last time marker. Seconds assumes the current tempo throughout (tempo automation is not followed).</summary>
public sealed record SongExtent(int EndTick, int LastClipEndTick, int LastMarkerTick, double Tempo, int Ppq, double Seconds);
public sealed record Note(int Channel, int Key, int StartTick, int LengthTick, int Velocity);
public sealed record TempoArgs(double Bpm);
public sealed record NameArgs(string Name);
public sealed record IndexArgs(int Index);
public sealed record NotesArgs(int Pattern, Note[] Notes);
public sealed record ReadNotesArgs(int Pattern, int Channel = -1, int Offset = 0);
public sealed record ClipArgs(int Pattern, int Track, int StartTick, int LengthTick);
public sealed record MixerArgs(int Track, int Volume, int Pan = 0);
public sealed record ChannelRouteArgs(int Channel, int Track);
public sealed record EffectArgs(int Track, int Slot, string Name);
public sealed record ParamArgs(int ChannelOrTrack, int Slot, int Parameter, double Value);
public sealed record ParamListArgs(int ChannelOrTrack, int Slot = -1, string? Filter = null);
public sealed record PathArgs(string Path);
public sealed record CatalogArgs(bool Effects = false);

public static class Messages
{
    public static JsonSerializerOptions Json { get; } = new(JsonSerializerDefaults.Web);

    public static JsonElement Element<T>(T value) => JsonSerializer.SerializeToElement(value, Json);

    public static T Arguments<T>(JsonElement element) =>
        element.Deserialize<T>(Json) ?? throw new ArgumentException("Missing operation arguments.");
}
