using System.Text.Json;
using FlMcp.Protocol;

namespace FlMcp.Server;

/// <summary>An inclusive one-based bar span to isolate before a render (bars 49..64 keep sixteen bars).
/// <paramref name="TailBeats"/> keeps the song end that many beats past the span so reverb and release tails
/// are rendered (live 2026-09-14: with the markers removed FL stopped exactly at the last clip end).</summary>
public sealed record RenderRange(int StartBar, int EndBar, bool CutClips = false, double TailBeats = 0)
{
    /// <summary>Null when neither bar is supplied; otherwise both are required and validated.</summary>
    public static RenderRange? From(int? startBar, int? endBar, bool cutClips, double tailBeats = 0)
    {
        if (startBar is null && endBar is null)
        {
            if (tailBeats != 0) throw new ArgumentException("tailBeats applies only to a section render; pass startBar and endBar.");
            return null;
        }
        if (startBar is null || endBar is null)
            throw new ArgumentException("startBar and endBar must be supplied together.");
        var range = new RenderRange(startBar.Value, endBar.Value, cutClips, tailBeats);
        range.Validate();
        return range;
    }

    public void Validate()
    {
        if (StartBar < 1) throw new ArgumentOutOfRangeException(nameof(StartBar), "startBar must be a one-based bar number.");
        if (EndBar < StartBar) throw new ArgumentOutOfRangeException(nameof(EndBar), "endBar must not precede startBar.");
        if (!double.IsFinite(TailBeats) || TailBeats < 0)
            throw new ArgumentOutOfRangeException(nameof(TailBeats), "tailBeats must be a finite number of beats >= 0.");
    }

    /// <summary>The <c>isolate_bars</c> arguments; <c>tail_beats</c> is passed only when set so an installed
    /// SDK that predates it still renders tail-less sections.</summary>
    public string PythonArguments()
    {
        var arguments = $"{StartBar}, {EndBar}, cut_clips={(CutClips ? "True" : "False")}";
        if (TailBeats > 0) arguments += $", tail_beats={TailBeats.ToString("R", System.Globalization.CultureInfo.InvariantCulture)}";
        return arguments;
    }
}

public sealed partial class ManagedSession
{
    private const int RangeIsolationTimeoutSeconds = 120;

    /// <summary>
    /// FL's command-line exporter always renders the whole song, so a range is produced by trimming the live
    /// disposable project with the SDK's <c>fruitylink.audition.isolate_bars</c> before the render snapshot.
    /// The caller has already saved the untrimmed project to <paramref name="fullProject"/>.
    /// </summary>
    private async Task<JsonElement> IsolateRangeAsync(RenderRange range, string fullProject, CancellationToken ct)
    {
        var code = "from fruitylink.audition import isolate_bars\n"
            + $"result = isolate_bars(fl, {range.PythonArguments()}).to_dict()\n";
        try
        {
            return await RunSdkScriptAsync(code, RangeIsolationTimeoutSeconds, "Range isolation", ct).ConfigureAwait(false);
        }
        catch (SdkScriptException failure)
        {
            throw new InvalidOperationException(
                $"Render range bars {range.StartBar}..{range.EndBar} could not be isolated: {failure.Error}. The editing session stays open; "
                + $"the untrimmed project is preserved at {fullProject}. Playlist edits made before the failure are not rolled back, "
                + "so resume from that snapshot or inspect the clips before continuing.", failure);
        }
    }
}
