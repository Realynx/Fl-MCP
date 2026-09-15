using System.Text.Json;
using FlMcp.Protocol;
using FlMcp.Server;
using Xunit;

namespace FlMcp.Tests;

/// <summary>fl_audio_describe, fl_audio_capture and fl_section_measure: argument validation, the scripts handed to the
/// embedded SDK, the result shapes, and the render fallback that closes, renders, reopens and measures.</summary>
public sealed class AudioToolTests
{
    private static JsonElement? Json(string? text) => text is null ? null : JsonDocument.Parse(text).RootElement.Clone();

    private static JsonElement Ok(object result) => Messages.Element(new { ok = true, result });

    private static JsonElement Failed(string error) => Messages.Element(new { ok = false, result = (object?)null, error, traceback = "Traceback..." });

    [Fact]
    public void DescribeRequiresExactlyOneOfPathOrChannel()
    {
        using var files = new TestFiles();
        var workspace = new WorkspacePaths(files.Root);
        TestFiles.WriteWave(files.PathFor("mix.wav"));
        var neither = Assert.Throws<ArgumentException>(() => AudioDescribeRequest.From(null, null, null, null, workspace));
        Assert.Contains("exactly one of path", neither.Message);
        Assert.Throws<ArgumentException>(() => AudioDescribeRequest.From("mix.wav", 2, null, null, workspace));
        Assert.Throws<ArgumentException>(() => AudioDescribeRequest.From("   ", null, null, null, workspace));
        Assert.Throws<ArgumentOutOfRangeException>(() => AudioDescribeRequest.From(null, -1, null, null, workspace));
    }

    [Fact]
    public void DescribeRejectsNonWavMissingFilesAndUnknownDetail()
    {
        using var files = new TestFiles();
        var workspace = new WorkspacePaths(files.Root);
        File.WriteAllText(files.PathFor("song.mp3"), "not audio");
        TestFiles.WriteWave(files.PathFor("mix.wav"));
        var format = Assert.Throws<ArgumentException>(() => AudioDescribeRequest.From("song.mp3", null, null, null, workspace));
        Assert.Contains(".wav", format.Message);
        var missing = Assert.Throws<FileNotFoundException>(() => AudioDescribeRequest.From("absent.wav", null, null, null, workspace));
        Assert.Contains(files.PathFor("absent.wav"), missing.Message);
        Assert.Contains(workspace.Root, missing.Message);
        Assert.Throws<FileNotFoundException>(() => AudioDescribeRequest.From("mix.wav", null, null, "absent.wav", workspace));
        var detail = Assert.Throws<ArgumentException>(() => AudioDescribeRequest.From("mix.wav", null, "loud", null, workspace));
        Assert.Contains("brief, normal or full", detail.Message);
    }

    [Fact]
    public void DescribeResolvesRelativePathsInsideTheWorkspaceAndNormalisesDetail()
    {
        using var files = new TestFiles();
        var workspace = new WorkspacePaths(files.Root);
        TestFiles.WriteWave(files.PathFor("mix.wav"));
        TestFiles.WriteWave(files.PathFor("before.wav"));
        var request = AudioDescribeRequest.From("mix.wav", null, " Full ", "before.wav", workspace);
        Assert.Equal(files.PathFor("mix.wav"), request.WavPath);
        Assert.Equal(files.PathFor("before.wav"), request.Baseline);
        Assert.Equal("full", request.Detail);
        Assert.Null(request.Channel);
        var absolute = AudioDescribeRequest.From(files.PathFor("mix.wav"), null, null, null, workspace);
        Assert.Equal(files.PathFor("mix.wav"), absolute.WavPath);
        Assert.Equal("normal", absolute.Detail);
    }

    [Fact]
    public void DescribeScriptsNameThePathTheChannelAndTheBaseline()
    {
        using var files = new TestFiles();
        var workspace = new WorkspacePaths(files.Root);
        TestFiles.WriteWave(files.PathFor("mix.wav"));
        TestFiles.WriteWave(files.PathFor("before.wav"));
        var byPath = AudioDescribeRequest.From("mix.wav", null, "brief", "before.wav", workspace).PythonScript();
        Assert.Contains("from fruitylink.analysis import describe_audio", byPath);
        Assert.Contains($"_target = {JsonSerializer.Serialize(files.PathFor("mix.wav"))}", byPath);
        Assert.Contains("_described = describe_audio(_target, **_options)", byPath);
        Assert.Contains("\"detail\": \"brief\"", byPath);
        Assert.Contains("\"bpm\": fl.transport.tempo, \"ppq\": fl.timebase.ppq", byPath);
        Assert.Contains($"_baseline = {JsonSerializer.Serialize(files.PathFor("before.wav"))}", byPath);
        Assert.Contains("compare_audio(_baseline, _described, **_options)", byPath);
        Assert.Contains("\"kind\": \"path\"", byPath);

        var byChannel = AudioDescribeRequest.From(null, 3, null, null, workspace).PythonScript();
        Assert.Contains("fl.samples.describe(3, **_options)", byChannel);
        Assert.Contains("fl.samples.path_of(3)", byChannel);
        Assert.Contains("\"kind\": \"channel\", \"channel\": 3", byChannel);
        Assert.DoesNotContain("compare_audio(_baseline", byChannel);
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("null", null)]
    [InlineData("\"master\"", null)]
    [InlineData("\"MASTER\"", null)]
    [InlineData("5", new[] { 5 })]
    [InlineData("[5, 0, 5]", new[] { 5, 0 })]
    [InlineData("[0]", new[] { 0 })]
    public void InsertsAcceptMasterASingleTrackOrAnArray(string? json, int[]? expected) =>
        Assert.Equal(expected, AudioCaptureRequest.ParseInserts(Json(json)));

    [Theory]
    [InlineData("[]")]
    [InlineData("[\"x\"]")]
    [InlineData("[501]")]
    [InlineData("[-1]")]
    [InlineData("[1.5]")]
    [InlineData("\"other\"")]
    [InlineData("{}")]
    [InlineData("true")]
    public void InsertsRejectAnythingElseWithTheUsageText(string json)
    {
        var error = Assert.Throws<ArgumentException>(() => AudioCaptureRequest.ParseInserts(Json(json)));
        Assert.Contains("mixer track indices 0..500", error.Message);
    }

    [Fact]
    public void CaptureRequestValidatesBarsTailAndName()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => AudioCaptureRequest.From(null, 0, 4, 0, null));
        Assert.Throws<ArgumentOutOfRangeException>(() => AudioCaptureRequest.From(null, 5, 4, 0, null));
        Assert.Throws<ArgumentOutOfRangeException>(() => AudioCaptureRequest.From(null, 1, 4, -1, null));
        Assert.Throws<ArgumentOutOfRangeException>(() => AudioCaptureRequest.From(null, 1, 4, double.NaN, null));
        var name = Assert.Throws<ArgumentException>(() => AudioCaptureRequest.From(null, 1, 4, 0, "bad/name"));
        Assert.Contains("<name>-<track>.wav", name.Message);
        Assert.Throws<ArgumentException>(() => AudioCaptureRequest.From(null, 1, 4, 0, ""));
        var originals = Assert.Throws<ArgumentException>(() => AudioCaptureRequest.From(null, 1, 4, 0, null, keepOriginals: true));
        Assert.Contains("keepOriginals only applies with name", originals.Message);
        var request = AudioCaptureRequest.From(Json("[5, 0]"), 33, 40, 2, "serum check");
        Assert.Equal(new[] { 5, 0 }, request.Inserts);
        Assert.True(request.ArmRefresh);
        Assert.False(request.KeepOriginals);
        Assert.Equal(20.4, request.Seconds(100), 3); // (8 bars * 4 beats + 2 beats) * 0.6 s
    }

    [Fact]
    public void CaptureScriptPassesInsertsBarsTailAndName()
    {
        var script = AudioCaptureRequest.From(Json("[5, 0]"), 33, 40, 2, "serum check").PythonScript();
        Assert.Contains("_captured = fl.audio.capture([5, 0], 33, 40, tail_beats=2, name=\"serum check\", arm_refresh=True, keep_originals=False)", script);
        Assert.Contains("result = _captured.to_dict()", script);
        Assert.Contains("describe_audio(_path, bpm=_captured.plan.bpm, ppq=_captured.plan.ppq, start_bar=33, detail=\"brief\").text", script);
        Assert.Contains("result[\"python_seconds\"]", script);
        var master = AudioCaptureRequest.From(null, 1, 4, 0, null).PythonScript();
        Assert.Contains("fl.audio.capture(\"master\", 1, 4, tail_beats=0, name=None, arm_refresh=True, keep_originals=False)", master);
        var flags = AudioCaptureRequest.From(null, 1, 4, 0, "take", armRefresh: false, keepOriginals: true).PythonScript();
        Assert.Contains("fl.audio.capture(\"master\", 1, 4, tail_beats=0, name=\"take\", arm_refresh=False, keep_originals=True)", flags);
    }

    [Fact]
    public void SectionMeasureRequestValidatesPreferenceInsertsAndDeadline()
    {
        var prefer = Assert.Throws<ArgumentException>(() => SectionMeasureRequest.From(1, 4, null, "fast", 0, 600));
        Assert.Contains("auto, live or render", prefer.Message);
        var perInsert = Assert.Throws<ArgumentException>(() => SectionMeasureRequest.From(1, 4, Json("[5]"), "render", 0, 600));
        Assert.Contains("live capture", perInsert.Message);
        Assert.Throws<ArgumentOutOfRangeException>(() => SectionMeasureRequest.From(1, 4, null, "auto", 0, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => SectionMeasureRequest.From(1, 4, null, "auto", 0, 3601));
        Assert.Throws<ArgumentOutOfRangeException>(() => SectionMeasureRequest.From(4, 1, null, "auto", 0, 600));
        var request = SectionMeasureRequest.From(33, 40, Json("\"master\""), " Live", 2, 600);
        Assert.Equal("live", request.Prefer);
        Assert.Null(request.Inserts);
        Assert.Equal(new RenderRange(33, 40, true, 2), request.Range);
    }

    [Fact]
    public void SectionMeasureScriptsEmbedTheRequestAndCatchRenderRequired()
    {
        var decision = SectionMeasureRequest.From(33, 108, null, "auto", 2, 600).DecisionScript();
        Assert.Contains("from fruitylink.capture import RenderRequired", decision);
        Assert.Contains("fl.audio.measure_section(33, 108, None, prefer=\"auto\", tail_beats=2)", decision);
        Assert.Contains("except RenderRequired as _required:", decision);
        Assert.Contains("\"suggested_tool_call\": _required.suggested_tool_call", decision);
        Assert.Contains("result[\"capture\"].pop(\"measurements\", None)", decision);
        var perInsert = SectionMeasureRequest.From(33, 40, Json("[5, 0]"), "live", 0, 600).DecisionScript();
        Assert.Contains("fl.audio.measure_section(33, 40, [5, 0], prefer=\"live\", tail_beats=0)", perInsert);
        var measure = SectionMeasureRequest.From(33, 108, null, "auto", 2, 600).MeasureScript(@"C:\ws\measure\song.wav");
        Assert.Contains("from fruitylink.capture import measure_wav", measure);
        Assert.Contains($"_path = {JsonSerializer.Serialize(@"C:\ws\measure\song.wav")}", measure);
        Assert.Contains("measure_wav(_path, bpm=_bpm, start_bar=33, end_bar=108, source_kind=\"master_render\")", measure);
        Assert.Contains("start_bar=33, detail=\"brief\"", measure);
    }

    [Fact]
    public async Task AudioToolsNeedAConnectedSession()
    {
        using var fixture = new SessionFixture();
        await using var session = fixture.Session();
        TestFiles.WriteWave(fixture.Files.PathFor("mix.wav"));
        var describe = await Assert.ThrowsAsync<InvalidOperationException>(() => session.DescribeAudioAsync("mix.wav", null, null, null, CancellationToken.None));
        Assert.Contains("No FL project is connected", describe.Message);
        var capture = await Assert.ThrowsAsync<InvalidOperationException>(() => session.CaptureAudioAsync(null, 1, 4, 0, null, true, false, CancellationToken.None));
        Assert.Contains("No FL project is connected", capture.Message);
        var measure = await Assert.ThrowsAsync<InvalidOperationException>(() => session.MeasureSectionAsync(1, 4, null, "auto", 0, 600, CancellationToken.None));
        Assert.Contains("No FL project is connected", measure.Message);
        Assert.DoesNotContain("python_execute", fixture.Bridge.Calls);
        // Argument problems are reported before the session is consulted.
        await Assert.ThrowsAsync<ArgumentException>(() => session.DescribeAudioAsync("mix.wav", 2, null, null, CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentException>(() => session.CaptureAudioAsync(Json("[]"), 1, 4, 0, null, true, false, CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentException>(() => session.MeasureSectionAsync(1, 4, null, "quick", 0, 600, CancellationToken.None));
    }

    [Fact]
    public async Task DescribeRunsInsideTheSessionAndReturnsTextTagsAndData()
    {
        using var fixture = new SessionFixture();
        await using var session = fixture.Session();
        await session.LaunchAsync("fresh.flp", 1, CancellationToken.None);
        TestFiles.WriteWave(fixture.Files.PathFor("mix.wav"));
        PythonExecute? request = null;
        fixture.Bridge.OnPython = (execute, _) =>
        {
            request = execute;
            return Task.FromResult(Ok(new
            {
                source = fixture.Files.PathFor("mix.wav"), kind = "path", channel = (int?)null, bpm = 100.0, ppq = 96, detail = "normal",
                text = "mix.wav - 259.200 s\nlevel: peak -1.0 dBFS\ntags: dark, wide", tags = new[] { "dark", "wide" },
                data = new { level = new { peak_dbfs = -1.0 } }, comparison = (object?)null, python_seconds = 2.7,
            }));
        };

        var result = await session.DescribeAudioAsync("mix.wav", null, null, null, CancellationToken.None);

        Assert.Contains($"_target = {JsonSerializer.Serialize(fixture.Files.PathFor("mix.wav"))}", request!.Code);
        Assert.Equal(180, request.TimeoutSeconds);
        Assert.Equal(fixture.Files.PathFor("fresh.flp"), request.ExpectedProjectPath);
        Assert.StartsWith("mix.wav - 259.200 s", result.GetProperty("text").GetString());
        Assert.Equal(new[] { "dark", "wide" }, result.GetProperty("tags").EnumerateArray().Select(tag => tag.GetString()));
        Assert.Equal(-1.0, result.GetProperty("data").GetProperty("level").GetProperty("peak_dbfs").GetDouble());
        Assert.Equal(JsonValueKind.Null, result.GetProperty("comparison").ValueKind);
        Assert.Equal(2.7, result.GetProperty("python_seconds").GetDouble());
        Assert.True(result.GetProperty("elapsedSeconds").GetDouble() >= 0);
        Assert.Equal(new[] { "python_execute" }, fixture.Bridge.Calls.Where(call => call != "status"));
    }

    [Fact]
    public async Task DescribeSurfacesTheSdkErrorVerbatimAndHintsAtAnOldWheel()
    {
        using var fixture = new SessionFixture();
        await using var session = fixture.Session();
        await session.LaunchAsync("fresh.flp", 1, CancellationToken.None);
        const string lookup = "LookupError: Channel 3 has no sample path known to this session; FL does not expose the Sampler's file, so pass path=... or load it through fl.channels.add_sample.";
        fixture.Bridge.OnPython = (_, _) => Task.FromResult(Failed(lookup));
        var error = await Assert.ThrowsAsync<SdkScriptException>(() => session.DescribeAudioAsync(null, 3, null, null, CancellationToken.None));
        Assert.Contains(lookup, error.Message);
        Assert.StartsWith("fl_audio_describe failed in the embedded SDK", error.Message);
        Assert.Equal(lookup, error.Error);
        Assert.DoesNotContain("predate", error.Message);

        fixture.Bridge.OnPython = (_, _) => Task.FromResult(Failed("AttributeError: 'Studio' object has no attribute 'samples'"));
        var old = await Assert.ThrowsAsync<SdkScriptException>(() => session.DescribeAudioAsync(null, 3, null, null, CancellationToken.None));
        Assert.Contains("may predate this helper", old.Message);
        Assert.Contains("fl_python_docs", old.Message);
    }

    [Fact]
    public async Task CaptureReturnsTheSdkRecordWithDescriptionsAndADeadlineSizedToTheSpan()
    {
        using var fixture = new SessionFixture();
        await using var session = fixture.Session();
        await session.LaunchAsync("fresh.flp", 1, CancellationToken.None);
        PythonExecute? request = null;
        fixture.Bridge.OnPython = (execute, _) =>
        {
            request = execute;
            return Task.FromResult(Ok(new
            {
                method = "fl_disk_recording",
                plan = new { inserts = new[] { 5, 0 }, start_bar = 1, end_bar = 108, bpm = 120.0, ppq = 96, seconds = 216.0 },
                files = new[] { new { track = 5, name = "Serum", path = @"C:\rec\song_1_Serum.wav", seconds = 217.0 }, new { track = 0, name = "Master", path = @"C:\rec\song_2_Master.wav", seconds = 217.0 } },
                captured_start_tick = 0, captured_end_tick = 41472, complete = true, stop_tick = 41480,
                measurements = new Dictionary<string, object> { ["5"] = new { rms_dbfs = -18.2 }, ["0"] = new { rms_dbfs = -16.4 } },
                recorded_folder = @"C:\rec",
                warnings = new[] { "Arm-refresh workaround applied: insert 1 (Insert 1) was armed and disarmed.", "FL stopped by itself at tick 41480 (song end?) before 41664.",
                    "FL added sample channel(s) [9, 10] for the recording(s); they were retired (muted, renamed)." },
                deleted_clips = 2, retired_channels = new[] { 9, 10 }, removed_originals = new[] { @"C:\rec\song_1_Serum.wav", @"C:\rec\song_2_Master.wav" },
                descriptions = new Dictionary<string, string> { ["5"] = "take-5.wav - 217.0 s", ["0"] = "take-master.wav - 217.0 s" },
                python_seconds = 231.4,
            }));
        };

        var result = await session.CaptureAudioAsync(Json("[5, 0]"), 1, 108, 2, "take", false, false, CancellationToken.None);

        Assert.Contains("fl.audio.capture([5, 0], 1, 108, tail_beats=2, name=\"take\", arm_refresh=False, keep_originals=False)", request!.Code);
        Assert.Contains("describe_audio(_path", request.Code);
        Assert.Equal(307, request.TimeoutSeconds); // 434 beats at 120 BPM = 217 s, plus 90 s
        Assert.Equal(307, result.GetProperty("timeoutSeconds").GetInt32());
        Assert.Equal("fl_disk_recording", result.GetProperty("method").GetString());
        Assert.Equal(2, result.GetProperty("files").GetArrayLength());
        Assert.Equal(-18.2, result.GetProperty("measurements").GetProperty("5").GetProperty("rms_dbfs").GetDouble());
        Assert.StartsWith("take-master.wav", result.GetProperty("descriptions").GetProperty("0").GetString());
        var warnings = result.GetProperty("warnings").EnumerateArray().Select(warning => warning.GetString()!).ToArray();
        Assert.Equal(3, warnings.Length);
        Assert.StartsWith("Arm-refresh workaround applied", warnings[0]);
        Assert.Contains("song end?", warnings[1]);
        Assert.Equal(2, result.GetProperty("deleted_clips").GetInt32());
        Assert.Equal(new[] { 9, 10 }, result.GetProperty("retired_channels").EnumerateArray().Select(item => item.GetInt32()));
        Assert.Equal(2, result.GetProperty("removed_originals").GetArrayLength());
        Assert.Equal("fl_disk_recording", result.GetProperty("method").GetString());
        Assert.True(result.GetProperty("complete").GetBoolean());
        Assert.True(result.GetProperty("elapsedSeconds").GetDouble() >= 0);
        Assert.Single(fixture.Processes.Started);
        Assert.False(fixture.Processes.Started[0].Process.Terminated);
    }

    [Fact]
    public async Task CaptureErrorsAreReturnedVerbatim()
    {
        using var fixture = new SessionFixture();
        await using var session = fixture.Session();
        await session.LaunchAsync("fresh.flp", 1, CancellationToken.None);
        const string refused = "CaptureError: The native mixer record-arm routines (FLmx_SetTrackArmed, MixerTrackArmedOffset) are not resolved on this FL build.";
        fixture.Bridge.OnPython = (_, _) => Task.FromResult(Failed(refused));
        var error = await Assert.ThrowsAsync<SdkScriptException>(() => session.CaptureAudioAsync(null, 33, 40, 0, null, true, false, CancellationToken.None));
        Assert.Contains(refused, error.Message);
        Assert.StartsWith("fl_audio_capture failed in the embedded SDK", error.Message);
        Assert.False(fixture.Processes.Started[0].Process.Terminated);
    }

    [Fact]
    public async Task SectionMeasureLiveRouteKeepsTheSessionOpenAndReturnsTheUniformRecord()
    {
        using var fixture = new SessionFixture();
        await using var session = fixture.Session();
        await session.LaunchAsync("fresh.flp", 1, CancellationToken.None);
        PythonExecute? request = null;
        fixture.Bridge.OnPython = (execute, _) =>
        {
            request = execute;
            return Task.FromResult(Ok(new
            {
                render_required = false, method = "fl_disk_recording",
                decision = new { method = "fl_disk_recording", live_cost_seconds = 27.2, render_cost_seconds = 47.4, reason = "live 27.2s <= render 47.4s" },
                start_bar = 33, end_bar = 40,
                files = new Dictionary<string, string> { ["0"] = @"C:\rec\song_1_Master.wav" },
                measurements = new Dictionary<string, object> { ["0"] = new { rms_dbfs = -16.4, integrated_lufs = -14.1 } },
                capture = new { plan = new { seconds = 19.2 }, complete = true, recorded_folder = @"C:\rec", warnings = new[] { "Could not disarm insert 0: refused" },
                    deleted_clips = 1, retired_channels = new[] { 9 }, removed_originals = Array.Empty<string>() },
                descriptions = new Dictionary<string, string> { ["0"] = "song_1_Master.wav - 19.2 s" },
                bpm = 100.0, ppq = 96, python_seconds = 30.1,
            }));
        };

        var result = await session.MeasureSectionAsync(33, 40, null, "auto", 0, 600, CancellationToken.None);

        Assert.Contains("fl.audio.measure_section(33, 40, None, prefer=\"auto\", tail_beats=0)", request!.Code);
        Assert.Equal("live", result.GetProperty("method").GetString());
        Assert.Equal("fl_disk_recording", result.GetProperty("sdkMethod").GetString());
        Assert.Equal(new[] { 9 }, result.GetProperty("capture").GetProperty("retired_channels").EnumerateArray().Select(item => item.GetInt32()));
        Assert.Equal(1, result.GetProperty("capture").GetProperty("deleted_clips").GetInt32());
        Assert.Equal("master", result.GetProperty("inserts").GetString());
        Assert.Equal("live 27.2s <= render 47.4s", result.GetProperty("decision").GetProperty("reason").GetString());
        Assert.Equal(-16.4, result.GetProperty("measurements").GetProperty("0").GetProperty("rms_dbfs").GetDouble());
        Assert.Equal(@"C:\rec\song_1_Master.wav", result.GetProperty("files").GetProperty("0").GetString());
        Assert.StartsWith("song_1_Master.wav", result.GetProperty("descriptions").GetProperty("0").GetString());
        Assert.True(result.GetProperty("capture").GetProperty("complete").GetBoolean());
        Assert.Equal(100.0, result.GetProperty("bpm").GetDouble());
        Assert.False(result.GetProperty("sessionClosed").GetBoolean());
        Assert.Equal(JsonValueKind.Null, result.GetProperty("render").ValueKind);
        Assert.Equal(JsonValueKind.Null, result.GetProperty("sessionReopenedFrom").ValueKind);
        Assert.Equal(JsonValueKind.Null, result.GetProperty("resumedProject").ValueKind);
        Assert.Equal("Could not disarm insert 0: refused", Assert.Single(result.GetProperty("warnings").EnumerateArray()).GetString());
        var timing = result.GetProperty("timing");
        Assert.True(timing.GetProperty("totalSeconds").GetDouble() >= 0);
        Assert.True(timing.GetProperty("captureSeconds").GetDouble() >= 0);
        Assert.Equal(30.1, timing.GetProperty("pythonSeconds").GetDouble());
        Assert.False(timing.TryGetProperty("renderSeconds", out _));
        Assert.Single(fixture.Processes.Started);
        Assert.False(fixture.Processes.Started[0].Process.Terminated);
        Assert.Equal(new[] { "python_execute" }, fixture.Bridge.Calls.Where(call => call != "status"));
    }

    private static Task<JsonElement> RenderRouteScripts(PythonExecute execute, List<PythonExecute> requests)
    {
        requests.Add(execute);
        if (execute.Code.Contains("measure_section", StringComparison.Ordinal))
            return Task.FromResult(Ok(new
            {
                render_required = true,
                decision = new { method = "offline_render", live_cost_seconds = 267.2, render_cost_seconds = 77.4, reason = "section of 259.2s exceeds the 120s live limit" },
                suggested_tool_call = new { tool = "fl_project_render", startBar = 1, endBar = 108, tailBeats = 2.0 },
                message = "Section bars 1-108 should be rendered offline", bpm = 100.0, ppq = 96, python_seconds = 0.01,
            }));
        if (execute.Code.Contains("isolate_bars", StringComparison.Ordinal))
            return Task.FromResult(Ok(new { kept_clips = 5, deleted_clips = 0, cut_clips = 0, tail_ticks = 192 }));
        if (execute.Code.Contains("measure_wav", StringComparison.Ordinal))
            return Task.FromResult(Ok(new
            {
                measurement = new { source_kind = "master_render", rms_dbfs = -16.4, integrated_lufs = -13.8, covered_bars = 108 },
                descriptions = new Dictionary<string, string> { ["0"] = "song-bars1-108.wav - 259.200 s" }, bpm = 100.0, ppq = 96, python_seconds = 2.7,
            }));
        throw new InvalidOperationException("unexpected script: " + execute.Code);
    }

    private static void WriteRenderOutput(System.Diagnostics.ProcessStartInfo info, double seconds)
    {
        if (info.ArgumentList[0] != "/R") return;
        var directory = info.ArgumentList[2][2..];
        TestFiles.WriteWave(Path.Combine(directory, Path.GetFileNameWithoutExtension(info.ArgumentList[3]) + ".wav"), seconds);
    }

    [Fact]
    public async Task SectionMeasureRenderRouteRendersReopensThePreservedProjectAndMeasuresThere()
    {
        using var fixture = new SessionFixture();
        await using var session = fixture.Session();
        await session.LaunchAsync("song.flp", 1, CancellationToken.None);
        var requests = new List<PythonExecute>();
        fixture.Bridge.OnPython = (execute, _) => RenderRouteScripts(execute, requests);
        fixture.Bridge.Song = new SongExtent(41472, 41472, 0, 100, 96, 259.2);
        fixture.Processes.OnRenderExit = info => WriteRenderOutput(info, 259.2);

        var result = await session.MeasureSectionAsync(1, 108, null, "auto", 2, 5, CancellationToken.None);

        Assert.Equal("render", result.GetProperty("method").GetString());
        Assert.Equal("offline_render", result.GetProperty("sdkMethod").GetString());
        Assert.Equal("section of 259.2s exceeds the 120s live limit", result.GetProperty("decision").GetProperty("reason").GetString());
        Assert.Equal("fl_project_render", result.GetProperty("suggestedToolCall").GetProperty("tool").GetString());
        Assert.True(result.GetProperty("sessionClosed").GetBoolean());
        var fullProject = result.GetProperty("sessionReopenedFrom").GetString()!;
        Assert.EndsWith("-full.flp", fullProject);
        Assert.True(File.Exists(fullProject));
        var resumed = result.GetProperty("resumedProject").GetString()!;
        Assert.EndsWith("-resumed.flp", resumed);
        Assert.StartsWith(Path.Combine(fixture.Files.Root, "measure") + Path.DirectorySeparatorChar, resumed);
        Assert.True(File.Exists(resumed));
        Assert.Equal(120, result.GetProperty("resumedStatus").GetProperty("tempo").GetDouble()); // the fake bridge's tempo
        Assert.True(result.GetProperty("resumedStatus").GetProperty("settle").GetProperty("stable").GetBoolean());
        var render = result.GetProperty("render");
        var wav = render.GetProperty("path").GetString()!;
        Assert.Matches(@"[\\/]measure[\\/]song-bars1-108-\d{8}-\d{6}\.wav$", wav);
        Assert.True(File.Exists(wav));
        Assert.Equal(wav, result.GetProperty("files").GetProperty("0").GetString());
        Assert.Equal(259.2, render.GetProperty("seconds").GetDouble(), 3);
        Assert.Equal(192, render.GetProperty("range").GetProperty("tail_ticks").GetInt32());
        Assert.Equal("ok", Assert.Single(render.GetProperty("attempts").EnumerateArray()).GetProperty("outcome").GetString());
        Assert.Equal(-16.4, result.GetProperty("measurements").GetProperty("0").GetProperty("rms_dbfs").GetDouble());
        Assert.StartsWith("song-bars1-108.wav", result.GetProperty("descriptions").GetProperty("0").GetString());
        Assert.Empty(result.GetProperty("warnings").EnumerateArray());
        var timing = result.GetProperty("timing");
        foreach (var key in new[] { "decideSeconds", "renderSeconds", "reopenSeconds", "measureSeconds", "totalSeconds" })
            Assert.True(timing.GetProperty(key).GetDouble() >= 0, key);
        Assert.Equal(2.7, timing.GetProperty("pythonSeconds").GetDouble());

        Assert.Equal(3, requests.Count);
        Assert.Contains("isolate_bars(fl, 1, 108, cut_clips=True, tail_beats=2)", requests[1].Code);
        Assert.Equal(resumed, requests[2].ExpectedProjectPath);
        Assert.Contains($"_path = {JsonSerializer.Serialize(wav)}", requests[2].Code);
        Assert.Equal(new[] { "python_execute", "save", "python_execute", "save", "song", "python_execute" }, fixture.Bridge.Calls.Where(call => call != "status"));
        Assert.Equal(3, fixture.Processes.Started.Count);
        Assert.True(fixture.Processes.Started[0].Process.Terminated);
        Assert.True(fixture.Processes.Started[1].Process.Terminated);
        Assert.False(fixture.Processes.Started[2].Process.Terminated);
        Assert.Equal(resumed, fixture.Processes.Started[2].Info.ArgumentList[0]);
        Assert.Equal(File.ReadAllBytes(fullProject), File.ReadAllBytes(resumed));
        var status = await session.CallAsync("status", new { }, CancellationToken.None);
        Assert.True(status.GetProperty("available").GetBoolean());
    }

    [Fact]
    public async Task SectionMeasureKeepsTheRenderWhenThePreservedProjectCannotBeReopened()
    {
        using var fixture = new SessionFixture();
        await using var session = fixture.Session();
        await session.LaunchAsync("song.flp", 1, CancellationToken.None);
        var requests = new List<PythonExecute>();
        fixture.Bridge.OnPython = (execute, _) => RenderRouteScripts(execute, requests);
        fixture.Processes.OnRenderExit = info => WriteRenderOutput(info, 259.2);
        var onStart = fixture.Processes.OnStart;
        fixture.Processes.OnStart = info =>
        {
            onStart?.Invoke(info);
            if (fixture.Processes.Started.Count >= 2) fixture.Bridge.Project = fixture.Files.PathFor("somewhere-else.flp"); // the reopened FL never reports the project
        };

        var result = await session.MeasureSectionAsync(1, 108, null, "render", 2, 1, CancellationToken.None);

        Assert.Equal("render", result.GetProperty("method").GetString());
        Assert.True(result.GetProperty("sessionClosed").GetBoolean());
        Assert.True(File.Exists(result.GetProperty("render").GetProperty("path").GetString()));
        Assert.Equal(JsonValueKind.Null, result.GetProperty("measurements").ValueKind);
        Assert.Equal(JsonValueKind.Null, result.GetProperty("resumedProject").ValueKind);
        var warning = Assert.Single(result.GetProperty("warnings").EnumerateArray()).GetString()!;
        Assert.StartsWith("MeasurementUnavailable", warning);
        Assert.Contains(result.GetProperty("sessionReopenedFrom").GetString()!, warning);
        Assert.Contains("fl_project_start(sourceProjectPath=", warning);
        Assert.Contains("measure_wav", warning);
        Assert.True(result.GetProperty("timing").GetProperty("reopenSeconds").GetDouble() >= 0);
        Assert.False(result.GetProperty("timing").TryGetProperty("measureSeconds", out _));
        Assert.Equal(2, requests.Count); // decision and isolation only; nothing was measured
        Assert.Equal(3, fixture.Processes.Started.Count);
        Assert.True(fixture.Processes.Started[2].Process.Terminated);
        await Assert.ThrowsAsync<InvalidOperationException>(() => session.CallAsync("status", new { }, CancellationToken.None));
    }

    [Fact]
    public async Task SectionMeasureReportsAFailedMeasurementInsteadOfHidingTheReopenedSession()
    {
        using var fixture = new SessionFixture();
        await using var session = fixture.Session();
        await session.LaunchAsync("song.flp", 1, CancellationToken.None);
        fixture.Bridge.OnPython = (execute, _) => execute.Code.Contains("measure_wav", StringComparison.Ordinal)
            ? Task.FromResult(Failed("ValueError: WAV has no data chunk"))
            : RenderRouteScripts(execute, []);
        fixture.Processes.OnRenderExit = info => WriteRenderOutput(info, 259.2);

        var result = await session.MeasureSectionAsync(1, 108, null, "render", 0, 5, CancellationToken.None);

        Assert.Equal(JsonValueKind.Null, result.GetProperty("measurements").ValueKind);
        Assert.EndsWith("-resumed.flp", result.GetProperty("resumedProject").GetString());
        var warning = Assert.Single(result.GetProperty("warnings").EnumerateArray()).GetString()!;
        Assert.StartsWith("MeasurementFailed: ValueError: WAV has no data chunk", warning);
        Assert.Contains(result.GetProperty("resumedProject").GetString()!, warning);
        Assert.True(result.GetProperty("timing").GetProperty("measureSeconds").GetDouble() >= 0);
        var status = await session.CallAsync("status", new { }, CancellationToken.None);
        Assert.True(status.GetProperty("available").GetBoolean());
    }

    [Fact]
    public async Task OversizedAudioResultsAreSavedUnderTheWorkspaceWithOkTrue()
    {
        using var fixture = new SessionFixture();
        await using var session = new ManagedSession(fixture.Settings with { PythonResponseLimitBytes = PythonResults.MinimumLimitBytes }, fixture.Processes, fixture.Bridge);
        await session.LaunchAsync("fresh.flp", 1, CancellationToken.None);
        TestFiles.WriteWave(fixture.Files.PathFor("mix.wav"));
        fixture.Bridge.OnPython = (_, _) => Task.FromResult(Ok(new { text = new string('t', 20_000), data = new { }, tags = Array.Empty<string>(), comparison = (object?)null }));

        var envelope = await session.DescribeAudioAsync("mix.wav", null, null, null, CancellationToken.None);

        Assert.True(envelope.GetProperty("oversized").GetBoolean());
        Assert.True(envelope.GetProperty("ok").GetBoolean());
        var saved = envelope.GetProperty("path").GetString()!;
        Assert.StartsWith(Path.Combine(fixture.Files.Root, PythonResults.ResultsDirectory) + Path.DirectorySeparatorChar, saved);
        using var document = JsonDocument.Parse(File.ReadAllText(saved));
        Assert.Equal(20_000, document.RootElement.GetProperty("text").GetString()!.Length);
        Assert.True(document.RootElement.GetProperty("elapsedSeconds").GetDouble() >= 0);
    }
}
