using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using FlMcp.Protocol;

namespace FlMcp.Server;

/// <summary>A script the server ran through the fl_execute_python path answered ok:false. <see cref="Error"/> is the
/// SDK's "ExceptionType: message" text verbatim (a CaptureError names the transport, folder or arm-symbol problem).</summary>
public sealed class SdkScriptException(string what, string error)
    : InvalidOperationException($"{what} failed in the embedded SDK: {error}{Hint(error)}")
{
    public string Error { get; } = error;

    private static string Hint(string error) =>
        error.StartsWith("ImportError", StringComparison.Ordinal) || error.StartsWith("ModuleNotFoundError", StringComparison.Ordinal)
        || error.StartsWith("AttributeError", StringComparison.Ordinal)
            ? " The installed fruitylink wheel may predate this helper (fl.audio, fl.samples, fruitylink.analysis.describe); fl_python_docs names the installed package."
            : "";
}

/// <summary>Builds the SDK scripts the audio tools run; string arguments are embedded as JSON literals, which Python reads unchanged.</summary>
internal static class AudioScripts
{
    public const string Prelude = "import time as _time\nfrom fruitylink.analysis import describe_audio\n_t0 = _time.perf_counter()\n";

    public static string Literal(string value) => JsonSerializer.Serialize(value);

    public static string Number(double value) => value.ToString("R", CultureInfo.InvariantCulture);

    /// <summary>A per-track dict of brief descriptions; a file that cannot be described reports why instead of failing the call.</summary>
    public static string Descriptions(string target, string items, int startBar, string bpm, string ppq) =>
        $"{target}[\"descriptions\"] = {{}}\n" +
        $"for _track, _path in {items}:\n" +
        "    try:\n" +
        $"        {target}[\"descriptions\"][str(_track)] = describe_audio(_path, bpm={bpm}, ppq={ppq}, start_bar={startBar}, detail=\"brief\").text\n" +
        "    except (OSError, ValueError, TypeError) as _error:\n" +
        $"        {target}[\"descriptions\"][str(_track)] = f\"describe failed: {{type(_error).__name__}}: {{_error}}\"\n";

    public const string Elapsed = "result[\"python_seconds\"] = round(_time.perf_counter() - _t0, 3)\n";
}

/// <summary>Validated fl_audio_describe arguments: exactly one of an existing WAV or a sampler channel index.</summary>
public sealed record AudioDescribeRequest(string? WavPath, int? Channel, string Detail, string? Baseline)
{
    public static readonly string[] Details = ["brief", "normal", "full"];

    public static AudioDescribeRequest From(string? path, int? channel, string? detail, string? compareWith, WorkspacePaths workspace)
    {
        var hasPath = !string.IsNullOrWhiteSpace(path);
        if (hasPath == channel.HasValue)
            throw new ArgumentException("Pass exactly one of path (an existing .wav: a render, a capture or a sample) or channel " +
                "(the zero-based channel-rack index of a sampler channel this session loaded through fl_sample_add or fl.channels.add_sample).");
        if (channel is < 0) throw new ArgumentOutOfRangeException(nameof(channel), "channel is a zero-based channel-rack index; see fl_channels_list.");
        var level = (detail ?? "normal").Trim().ToLowerInvariant();
        if (!Details.Contains(level)) throw new ArgumentException($"detail must be brief, normal or full; got '{detail}'.");
        return new(hasPath ? ResolveWave(path!, workspace, "path") : null, channel, level,
            string.IsNullOrWhiteSpace(compareWith) ? null : ResolveWave(compareWith, workspace, "compareWith"));
    }

    /// <summary>An existing .wav; relative paths resolve inside the workspace so a render can be named as fl_project_render returned it.</summary>
    internal static string ResolveWave(string path, WorkspacePaths workspace, string argument)
    {
        var full = Path.IsPathFullyQualified(path) ? Path.GetFullPath(path) : Path.GetFullPath(path, workspace.Root);
        if (!string.Equals(Path.GetExtension(full), ".wav", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException($"{argument} must name a .wav file (RIFF PCM or float); got '{path}'. Convert other formats to WAV first.");
        if (!File.Exists(full))
            throw new FileNotFoundException($"{argument} does not exist: {full}. Relative paths resolve inside the workspace {workspace.Root}; " +
                "pass an absolute path for files elsewhere (Splice samples, FL's recorded-audio folder).", full);
        return full;
    }

    public string PythonScript()
    {
        var script = AudioScripts.Prelude + "from fruitylink.analysis import compare_audio\n" +
            $"_options = {{\"bpm\": fl.transport.tempo, \"ppq\": fl.timebase.ppq, \"detail\": {AudioScripts.Literal(Detail)}}}\n";
        script += WavPath is not null
            ? $"_target = {AudioScripts.Literal(WavPath)}\n_described = describe_audio(_target, **_options)\n"
            : $"_described = fl.samples.describe({Channel}, **_options)\n_target = fl.samples.path_of({Channel})\n";
        script += $"result = {{\"source\": _target, \"kind\": {(WavPath is not null ? "\"path\"" : "\"channel\"")}, \"channel\": {Channel?.ToString(CultureInfo.InvariantCulture) ?? "None"}, " +
            "\"bpm\": _options[\"bpm\"], \"ppq\": _options[\"ppq\"], \"detail\": _options[\"detail\"], " +
            "\"text\": _described.text, \"tags\": _described.tags, \"data\": _described.data, \"comparison\": None}\n";
        if (Baseline is not null)
            script += $"_baseline = {AudioScripts.Literal(Baseline)}\n_comparison = compare_audio(_baseline, _described, **_options)\n" +
                "result[\"comparison\"] = {\"baseline\": _baseline, \"text\": _comparison.text, \"data\": _comparison.data}\n";
        return script + AudioScripts.Elapsed;
    }
}

/// <summary>Validated fl_audio_capture arguments. <see cref="Inserts"/> null means the master only; <see cref="EndBar"/> is inclusive.</summary>
public sealed partial record AudioCaptureRequest(int[]? Inserts, int StartBar, int EndBar, double TailBeats, string? Name,
    bool ArmRefresh = true, bool KeepOriginals = false)
{
    public const string InsertsUsage = "inserts must be \"master\" (or omitted) or an array of mixer track indices 0..500 (0 = Master; " +
        "ordinary inserts as listed by fl.mixer.list()).";

    public static AudioCaptureRequest From(JsonElement? inserts, int startBar, int endBar, double tailBeats, string? name,
        bool armRefresh = true, bool keepOriginals = false)
    {
        new RenderRange(startBar, endBar, false, tailBeats).Validate();
        if (name is not null && !CaptureName().IsMatch(name))
            throw new ArgumentException("name must be 1..80 characters of letters, digits, space, dot, underscore or dash; the recordings are copied as <name>-<track>.wav beside FL's originals.");
        if (keepOriginals && name is null)
            throw new ArgumentException("keepOriginals only applies with name: without a name FL's auto-named recordings are the result and are always kept.");
        return new(ParseInserts(inserts), startBar, endBar, tailBeats, name, armRefresh, keepOriginals);
    }

    /// <summary>"master"/null/omitted -> null; a number or an array of numbers -> distinct tracks 0..500 in call order.</summary>
    public static int[]? ParseInserts(JsonElement? inserts)
    {
        if (inserts is not { } element || element.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined) return null;
        switch (element.ValueKind)
        {
            case JsonValueKind.String:
                if (string.Equals(element.GetString(), "master", StringComparison.OrdinalIgnoreCase)) return null;
                throw new ArgumentException(InsertsUsage);
            case JsonValueKind.Number:
                return [Track(element)];
            case JsonValueKind.Array:
                var tracks = new List<int>();
                foreach (var item in element.EnumerateArray())
                {
                    var track = Track(item);
                    if (!tracks.Contains(track)) tracks.Add(track);
                }
                if (tracks.Count == 0) throw new ArgumentException("inserts is empty; " + InsertsUsage);
                return [.. tracks];
            default:
                throw new ArgumentException(InsertsUsage);
        }
    }

    private static int Track(JsonElement item)
    {
        if (item.ValueKind != JsonValueKind.Number || !item.TryGetInt32(out var track) || track is < 0 or > 500)
            throw new ArgumentException(InsertsUsage);
        return track;
    }

    public static string PythonInserts(int[]? inserts, string whenMaster) =>
        inserts is null ? whenMaster : "[" + string.Join(", ", inserts) + "]";

    /// <summary>Seconds the pass plays at <paramref name="bpm"/> in 4/4, tail included.</summary>
    public double Seconds(double bpm) => ((EndBar - StartBar + 1) * 4 + TailBeats) * 60 / bpm;

    public string PythonScript() =>
        AudioScripts.Prelude +
        $"_captured = fl.audio.capture({PythonInserts(Inserts, "\"master\"")}, {StartBar}, {EndBar}, tail_beats={AudioScripts.Number(TailBeats)}, " +
        $"name={(Name is null ? "None" : AudioScripts.Literal(Name))}, arm_refresh={Flag(ArmRefresh)}, keep_originals={Flag(KeepOriginals)})\n" +
        "result = _captured.to_dict()\n" +
        AudioScripts.Descriptions("result", "((_file.track, _file.path) for _file in _captured.files)", StartBar, "_captured.plan.bpm", "_captured.plan.ppq") +
        AudioScripts.Elapsed;

    private static string Flag(bool value) => value ? "True" : "False";

    [GeneratedRegex(@"^[A-Za-z0-9 ._-]{1,80}$", RegexOptions.CultureInvariant)]
    private static partial Regex CaptureName();
}

/// <summary>Validated fl_section_measure arguments; an explicit insert list forces the live route (a render only yields the master).</summary>
public sealed record SectionMeasureRequest(int StartBar, int EndBar, int[]? Inserts, string Prefer, double TailBeats, int TimeoutSeconds)
{
    public static readonly string[] Preferences = ["auto", "live", "render"];
    public const int ReopenTimeoutCap = 300;

    public static SectionMeasureRequest From(int startBar, int endBar, JsonElement? inserts, string? prefer, double tailBeats, int timeoutSeconds)
    {
        new RenderRange(startBar, endBar, true, tailBeats).Validate();
        var choice = (prefer ?? "auto").Trim().ToLowerInvariant();
        if (!Preferences.Contains(choice)) throw new ArgumentException($"prefer must be auto, live or render; got '{prefer}'.");
        var tracks = AudioCaptureRequest.ParseInserts(inserts);
        if (choice == "render" && tracks is not null)
            throw new ArgumentException("Per-insert measurements need a live capture (a render only yields the master): omit inserts or pass prefer=\"live\" or \"auto\".");
        if (timeoutSeconds < 1 || timeoutSeconds > 3600)
            throw new ArgumentOutOfRangeException(nameof(timeoutSeconds), $"timeoutSeconds must be 1..3600 (the render deadline per attempt; the reopen uses at most {ReopenTimeoutCap}).");
        return new(startBar, endBar, tracks, choice, tailBeats, timeoutSeconds);
    }

    public RenderRange Range => new(StartBar, EndBar, CutClips: true, TailBeats);

    public double Seconds(double bpm) => ((EndBar - StartBar + 1) * 4 + TailBeats) * 60 / bpm;

    /// <summary>Runs measure_section; a RenderRequired decision comes back as data so the server can own the render.</summary>
    public string DecisionScript() =>
        AudioScripts.Prelude + "from fruitylink.capture import RenderRequired\n" +
        "_bpm = fl.transport.tempo\n_ppq = fl.timebase.ppq\n" +
        "try:\n" +
        $"    _measured = fl.audio.measure_section({StartBar}, {EndBar}, {AudioCaptureRequest.PythonInserts(Inserts, "None")}, " +
        $"prefer={AudioScripts.Literal(Prefer)}, tail_beats={AudioScripts.Number(TailBeats)})\n" +
        "except RenderRequired as _required:\n" +
        "    result = {\"render_required\": True, \"decision\": _required.decision.to_dict(), " +
        "\"suggested_tool_call\": _required.suggested_tool_call, \"message\": str(_required)}\n" +
        "else:\n" +
        "    result = _measured.to_dict()\n" +
        "    result[\"render_required\"] = False\n" +
        "    if isinstance(result.get(\"capture\"), dict):\n" +
        "        result[\"capture\"].pop(\"measurements\", None)\n" +
        Indent(AudioScripts.Descriptions("result", "_measured.files.items()", StartBar, "_bpm", "_ppq")) +
        "result.update(bpm=_bpm, ppq=_ppq)\n" + AudioScripts.Elapsed;

    /// <summary>Measures a finished section render (bar 1 of the file is <see cref="StartBar"/>) in the reopened session.</summary>
    public string MeasureScript(string wavPath) =>
        AudioScripts.Prelude + "from fruitylink.capture import measure_wav\n" +
        "_bpm = fl.transport.tempo\n_ppq = fl.timebase.ppq\n" +
        $"_path = {AudioScripts.Literal(wavPath)}\n" +
        $"result = {{\"measurement\": measure_wav(_path, bpm=_bpm, start_bar={StartBar}, end_bar={EndBar}, source_kind=\"master_render\"), \"bpm\": _bpm, \"ppq\": _ppq}}\n" +
        AudioScripts.Descriptions("result", "((0, _path),)", StartBar, "_bpm", "_ppq") +
        AudioScripts.Elapsed;

    private static string Indent(string block) =>
        string.Concat(block.Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(line => "    " + line + "\n"));
}

public sealed partial class ManagedSession
{
    private const int DescribeTimeoutSeconds = 180;
    private const int MeasureWaveTimeoutSeconds = 300;
    private const string MasterTrack = "master";

    /// <summary>fl_audio_describe: describe (and optionally compare) a WAV or a sampler channel inside the live session.</summary>
    public async Task<JsonElement> DescribeAudioAsync(string? path, int? channel, string? detail, string? compareWith, CancellationToken ct)
    {
        var request = AudioDescribeRequest.From(path, channel, detail, compareWith, paths);
        await gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var started = Stopwatch.GetTimestamp();
            await RequireProjectIdentityAsync(ct).ConfigureAwait(false);
            var described = Fields(await RunSdkScriptAsync(request.PythonScript(), DescribeTimeoutSeconds, "fl_audio_describe", ct).ConfigureAwait(false));
            described["elapsedSeconds"] = Messages.Element(Elapsed(started));
            return BoundResult(described);
        }
        finally { gate.Release(); }
    }

    /// <summary>fl_audio_capture: FL's own disk recording of the inserts over a bar span, measured and briefly described.</summary>
    public async Task<JsonElement> CaptureAudioAsync(JsonElement? inserts, int startBar, int endBar, double tailBeats, string? name,
        bool armRefresh, bool keepOriginals, CancellationToken ct)
    {
        var request = AudioCaptureRequest.From(inserts, startBar, endBar, tailBeats, name, armRefresh, keepOriginals);
        await gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var started = Stopwatch.GetTimestamp();
            var status = await RequireProjectIdentityAsync(ct).ConfigureAwait(false);
            var timeout = CaptureTimeout(status.Tempo > 0 ? request.Seconds(status.Tempo) : double.PositiveInfinity);
            var captured = Fields(await RunSdkScriptAsync(request.PythonScript(), timeout, "fl_audio_capture", ct).ConfigureAwait(false));
            captured["elapsedSeconds"] = Messages.Element(Elapsed(started));
            captured["timeoutSeconds"] = Messages.Element(timeout);
            return BoundResult(captured);
        }
        finally { gate.Release(); }
    }

    /// <summary>fl_section_measure: the SDK's policy picks live capture or offline render; the render route is owned here.</summary>
    public async Task<JsonElement> MeasureSectionAsync(int startBar, int endBar, JsonElement? inserts, string? prefer, double tailBeats,
        int timeoutSeconds, CancellationToken ct)
    {
        var request = SectionMeasureRequest.From(startBar, endBar, inserts, prefer, tailBeats, timeoutSeconds);
        await gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var started = Stopwatch.GetTimestamp();
            var status = await RequireProjectIdentityAsync(ct).ConfigureAwait(false);
            var timeout = CaptureTimeout(status.Tempo > 0 ? request.Seconds(status.Tempo) : double.PositiveInfinity);
            var decided = Fields(await RunSdkScriptAsync(request.DecisionScript(), timeout, "fl_section_measure", ct).ConfigureAwait(false));
            var decideSeconds = Elapsed(started);
            var record = decided.TryGetValue("render_required", out var flag) && flag.ValueKind == JsonValueKind.True
                ? await MeasureByRenderAsync(request, decided, decideSeconds, ct).ConfigureAwait(false)
                : LiveRecord(request, decided, decideSeconds);
            record["timing"] = Timing(record, started);
            return BoundResult(record);
        }
        finally { gate.Release(); }
    }

    /// <summary>The live pass plays in real time: the span plus generous overhead for arming, file finalisation and analysis.</summary>
    private static int CaptureTimeout(double seconds) => (int)Math.Clamp(Math.Ceiling(seconds) + 90, 120, 900);

    private static Dictionary<string, object?> LiveRecord(SectionMeasureRequest request, Dictionary<string, JsonElement> measured, double decideSeconds)
    {
        var record = SectionRecord(request, "live", measured);
        record["sdkMethod"] = Get(measured, "method") is { ValueKind: JsonValueKind.String } sdkMethod ? sdkMethod.GetString() : "fl_disk_recording";
        record["files"] = Get(measured, "files");
        record["measurements"] = Get(measured, "measurements");
        record["descriptions"] = Get(measured, "descriptions");
        record["capture"] = Get(measured, "capture");
        record["warnings"] = Get(measured, "capture") is { ValueKind: JsonValueKind.Object } capture && capture.TryGetProperty("warnings", out var warnings)
            ? warnings.Clone() : Messages.Element(Array.Empty<string>());
        record["captureSeconds"] = decideSeconds;
        return record;
    }

    private static Dictionary<string, object?> SectionRecord(SectionMeasureRequest request, string method, Dictionary<string, JsonElement> decided) => new()
    {
        ["method"] = method,
        ["sdkMethod"] = method == "live" ? "fl_disk_recording" : "offline_render",
        ["startBar"] = request.StartBar,
        ["endBar"] = request.EndBar,
        ["tailBeats"] = request.TailBeats,
        ["inserts"] = (object?)request.Inserts ?? MasterTrack,
        ["prefer"] = request.Prefer,
        ["decision"] = Get(decided, "decision"),
        ["bpm"] = Get(decided, "bpm"),
        ["ppq"] = Get(decided, "ppq"),
        ["files"] = null,
        ["measurements"] = null,
        ["descriptions"] = null,
        ["capture"] = null,
        ["render"] = null,
        ["sessionClosed"] = false,
        ["sessionReopenedFrom"] = null,
        ["resumedProject"] = null,
        ["resumedStatus"] = null,
        ["warnings"] = Array.Empty<string>(),
        ["pythonSeconds"] = Get(decided, "python_seconds"),
    };

    /// <summary>Section render, reopen the preserved project, measure the WAV there. Every step after the render is
    /// reported rather than thrown: the WAV exists and the caller must learn where the session went.</summary>
    private async Task<Dictionary<string, object?>> MeasureByRenderAsync(SectionMeasureRequest request, Dictionary<string, JsonElement> decided,
        double decideSeconds, CancellationToken ct)
    {
        var reason = Get(decided, "decision") is { ValueKind: JsonValueKind.Object } decision && decision.TryGetProperty("reason", out var why)
            ? why.GetString() : "policy";
        if (ownership == SessionOwnership.Attached)
            throw new InvalidOperationException($"The policy chose an offline render ({reason}), but a user-owned attached FL session is never rendered or closed. " +
                "Pass prefer=\"live\" to capture the section with FL's disk recording instead, or measure it from a managed fl_project_start session.");
        var stem = Path.GetFileNameWithoutExtension(expectedProject!);
        var basename = $"{stem}-bars{request.StartBar}-{request.EndBar}-{DateTime.UtcNow.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture)}";
        var background = ownedBackground;
        var warnings = new List<string>();
        var renderStarted = Stopwatch.GetTimestamp();
        var render = await RenderCoreAsync(Path.Combine("measure", basename + ".wav"), request.TimeoutSeconds, ct, request.Range).ConfigureAwait(false);
        warnings.AddRange(render.Warnings.Select(warning => $"{warning.Code}: {warning.Message}"));
        var record = SectionRecord(request, "render", decided);
        record["decideSeconds"] = decideSeconds;
        record["renderSeconds"] = Elapsed(renderStarted);
        record["suggestedToolCall"] = Get(decided, "suggested_tool_call");
        record["render"] = render;
        record["sessionClosed"] = true;
        record["sessionReopenedFrom"] = render.FullProject;
        record["files"] = new Dictionary<string, string> { ["0"] = render.Path };
        record["warnings"] = warnings;
        await ReopenAndMeasureAsync(request, render, background, record, warnings, ct).ConfigureAwait(false);
        return record;
    }

    private async Task ReopenAndMeasureAsync(SectionMeasureRequest request, RenderResult render, bool background,
        Dictionary<string, object?> record, List<string> warnings, CancellationToken ct)
    {
        var reopenStarted = Stopwatch.GetTimestamp();
        try
        {
            var resumed = await LaunchCoreAsync(Path.Combine("measure", Path.GetFileNameWithoutExtension(render.Path) + "-resumed.flp"),
                Math.Min(request.TimeoutSeconds, SectionMeasureRequest.ReopenTimeoutCap), ct, render.FullProject!, background).ConfigureAwait(false);
            record["resumedProject"] = expectedProject;
            record["resumedStatus"] = resumed;
            warnings.AddRange(resumed.Warnings.Select(warning => $"{warning.Code}: {warning.Message}"));
        }
        catch (Exception failure) when (failure is InvalidOperationException or IOException or InvalidDataException or TimeoutException)
        {
            record["reopenSeconds"] = Elapsed(reopenStarted);
            warnings.Add($"MeasurementUnavailable: the section rendered to {render.Path} but the preserved project could not be reopened to measure it: {failure.Message} " +
                $"Resume it with fl_project_start(sourceProjectPath={render.FullProject}) and measure the WAV with fruitylink.capture.measure_wav(path, bpm=..., start_bar={request.StartBar}, end_bar={request.EndBar}).");
            return;
        }
        record["reopenSeconds"] = Elapsed(reopenStarted);
        var measureStarted = Stopwatch.GetTimestamp();
        try
        {
            var measured = Fields(await RunSdkScriptAsync(request.MeasureScript(render.Path), MeasureWaveTimeoutSeconds, "fl_section_measure", ct).ConfigureAwait(false));
            record["measurements"] = new Dictionary<string, JsonElement?> { ["0"] = Get(measured, "measurement") };
            record["descriptions"] = Get(measured, "descriptions");
            record["bpm"] = Get(measured, "bpm");
            record["ppq"] = Get(measured, "ppq");
            record["pythonSeconds"] = Get(measured, "python_seconds");
        }
        catch (SdkScriptException failure)
        {
            warnings.Add($"MeasurementFailed: {failure.Error} The WAV is at {render.Path}; the session was reopened from {render.FullProject} as {expectedProject}.");
        }
        record["measureSeconds"] = Elapsed(measureStarted);
    }

    private static object Timing(Dictionary<string, object?> record, long started)
    {
        var timing = new Dictionary<string, object?> { ["totalSeconds"] = Elapsed(started) };
        foreach (var key in new[] { "captureSeconds", "decideSeconds", "renderSeconds", "reopenSeconds", "measureSeconds", "pythonSeconds" })
            if (record.Remove(key, out var value)) timing[key] = value;
        return timing;
    }

    /// <summary>Runs SDK code through the same python_execute path fl_execute_python uses; the caller holds the gate and has
    /// confirmed the project identity. Returns the script's result; an ok:false reply becomes an SdkScriptException.</summary>
    private async Task<JsonElement> RunSdkScriptAsync(string code, int timeoutSeconds, string what, CancellationToken ct)
    {
        JsonElement reply;
        try
        {
            var request = new PythonExecute(code, timeoutSeconds, expectedProject ?? attachedProject?.Path ?? "") { AttachedProject = attachedProject };
            reply = await CallCoreAsync("python_execute", request, timeoutSeconds, ct).ConfigureAwait(false);
        }
        catch (BridgeCompletionUnknownException) { embeddedCompletionUnknown = true; throw; }
        if (reply.ValueKind == JsonValueKind.Object && reply.TryGetProperty("ok", out var ok) && ok.ValueKind == JsonValueKind.True)
            return reply.TryGetProperty("result", out var result) ? result.Clone() : default;
        var error = reply.ValueKind == JsonValueKind.Object && reply.TryGetProperty("error", out var detail) && detail.ValueKind == JsonValueKind.String
            ? detail.GetString()! : "the embedded SDK returned no error text";
        throw new SdkScriptException(what, error);
    }

    private static Dictionary<string, JsonElement> Fields(JsonElement result) =>
        result.ValueKind == JsonValueKind.Object
            ? result.Deserialize<Dictionary<string, JsonElement>>(Messages.Json)!
            : throw new InvalidDataException("The embedded SDK script returned no result object; the installed fruitylink wheel may be older than this server expects.");

    private static JsonElement? Get(Dictionary<string, JsonElement> fields, string key) =>
        fields.TryGetValue(key, out var value) && value.ValueKind != JsonValueKind.Null ? value : null;

    private static double Elapsed(long started) => Math.Round(Stopwatch.GetElapsedTime(started).TotalSeconds, 2);

    private JsonElement BoundResult<T>(T record) =>
        PythonResults.Bound(Messages.Element(record), settings.PythonResponseLimitBytes, paths, DateTimeOffset.UtcNow, ok: true);
}
