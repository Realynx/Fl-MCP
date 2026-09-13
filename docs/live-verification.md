# Live verification

The September 12 checkpoint used FL MCP 0.2.0 with the locally prepared FruityLink installer **0.1.22** and SDK/Python 0.2.0. Installer and SDK version numbers are separate. These are verification records, not an announcement that installers or SDK packages have been publicly released.

## September 13: invalid-note recovery

The background-mode follow-up used the SDK private-desktop launcher with serialized cold starts. Two independent MCP clients held live FL 26.1.3.5570 authoring sessions simultaneously, each authored 24 notes and a playlist clip, and each rendered a 1,536,184-byte stereo float32 WAV at 48 kHz (192,000 frames). Neither owned PID appeared in the controller's active-desktop window enumeration. The damaged-copy recovery fixture also passed in background mode, retaining 23 valid notes and leaving its source SHA-256 unchanged. All owned jobs closed without cleanup errors. Receipts are SDK `artifacts/background-20260913/live-verification.json` and its accompanying per-client reports.

The SDK also passed standalone Python save/reopen/render tests without MCP and a separate Serum 2 export. Background means normal FL on a private Windows desktop; it does not remove the internal GUI renderer. Independent clients can run concurrent jobs, but each MCP client still manages one owned session at a time.

The installed **0.1.23** framework and MCP recovered a disposable FL 26.1.3.5570 project containing 24 notes with one deliberately invalid channel reference. It accepted only the exact load-recovery prompt, retained the 23 valid notes, saved a snapshot and rendered a 1,536,184-byte WAV. The corrupted source's SHA-256 was unchanged. The successful launch and render both returned recovery warnings with original-copy and diagnostic paths.

A separate installed-build authoring test rejected three invalid channel references and one overflowing note end without partial batch changes. All 24 valid notes, including an edit, survived save/reopen with unchanged properties and rendered successfully. Both tests used disposable fixtures; no user song was modified.

The final policy declines a startup invalid-note save confirmation with **No** because its destination is unconfirmed; it refuses that save confirmation during render. That startup save prompt did not appear in the final successful run. Its No semantics were verified from the native save branch and are covered by policy tests. The exact load prompt uses verified Yes semantics. See [dialog recovery](dialog-recovery.md) for the complete allowlist and ownership rules.

The shared reader passed 18 targeted regression tests covering both exact-build profiles, class/HWND/body ownership, callback identity, bounded Unicode and malformed metadata. The live receipt is SDK `artifacts/note-recovery-20260913/recovery-verification.json`. FL 2025 recovery remains binary- and fixture-verified only.

## September 12 checkpoint

## Exact-build coverage

| FL Studio build | Evidence |
| --- | --- |
| **26.1.3.5570, Windows x64** | Actual installed-plugin, MCP, authoring, snapshot, automation, mixer, measurement, and render runs described below |
| **25.2.5.5319, Windows x64** | Exact-binary inspection and native/managed regression tests; **no live FL 2025 run** in this verification cycle |

Do not infer support for other builds from the year or major version. Native scanner/layout capabilities determine which operations are available. Automated fixtures and binary inspection are useful evidence, but neither substitutes for exercising the actual application.

## What passed in FL Studio 2026

| Area | Observed behavior |
| --- | --- |
| Managed startup | Started a fresh project in a new FL process, waited for the correct project path and bridge readiness, and read tempo/PPQ. Python reported the same PID as FL. |
| Assistive attachment | Discovered and attached to ordinary FL sessions with saved projects outside the workspace and with untitled projects. Python read/edited/restored tempo. Snapshot saves preserved project identity and song mode. |
| Ownership | Attached close/render were refused; a second companion could not steal a live lease. Detach allowed handover, and companion exit left user-owned FL running. |
| Musical authoring and rendering | Authored and revised **Glass Satellites**, a 120-bar, 150 BPM arrangement with Serum 2 and sample-based voices, and rendered its **192-second WAV**. A smaller sampler composition also exercised save/reopen and exact arrangement length. Instruments and samples were separately licensed; they are not distributed here. |
| Mixer insertion | Appended and inserted after Master and an ordinary track in a disposable project. Existing names, effects, routing and sends survived index shifts. Save/reopen and rendering passed. The special Current track was refused as an insertion anchor. |
| Linked automation | Created an automation channel with its initial parameter link, replaced/added/deleted points, and placed an existing automation channel. Points, links and clips survived save/reopen. A rendered volume envelope produced nonzero audio in the first half and silence in the second. Ordinary non-automation channels were refused. |
| Audio analysis | The installed embedded interpreter measured explicit WAV ranges and transient windows, including RMS, energy, loudness, estimated true peak/PSR and kick spectrum. Standalone measurements were compared with FFmpeg. These tests used rendered/supplied audio, not live per-channel capture. |
| Cancellation and recovery | A deliberately infinite Python loop with a one-second deadline returned the expected cancellation error after approximately **1.32 seconds**. Subsequent Python execution and a native query succeeded in the same FL session. |

The automation render was eight seconds, stereo, 48 kHz float PCM. Measured first-half RMS was **0.0661298521** and second-half RMS **0.0**. This verifies the envelope's effect on rendered samples; it is not a claim of listening evaluation.

The 192-second song's offline integrated loudness measured **-18.8632 LUFS**, compared with FFmpeg's displayed **-18.9 LUFS**. Whole-file amplitude/loudness analysis without true-peak interpolation completed in approximately 15.5 seconds on the test machine. Numerical comparisons validate the tested signals and configuration, not certification as a standards-compliant meter.

## Embedded analysis performance

The same installed-MCP script analyzed a three-second audio region with loudness and true-peak estimation, another region's amplitude, and a kick's transient windows and spectrum:

| Local installer | Elapsed time | Result |
| --- | ---: | --- |
| 0.1.21 | 101.437 seconds | Correct output, excessive tracing overhead |
| **0.1.22** | **11.000 seconds** | Same output, **9.22× faster** in this measured run |

The change batches expensive frame-ancestry inspection and managed cancellation polling to once per 1024 Python trace events. Initial script entry and SDK-call cancellation checks remain immediate. Tracing stays active for user code, nested `exec`, and imported helpers. The event bound is not a wall-clock guarantee: blocking native code or an unfinished child thread can still delay cancellation and cleanup.

## Historical render regression

The four-bar **Neon Steps** sampler fixture rendered at 128 BPM / 96 PPQ to 7.5 seconds of stereo 48 kHz float WAV. Its saved project contained 72 notes and two four-bar clips. Under local installer 0.1.12, add/resize/delete operations changed content length **4 → 8 → 6 → 4 bars**, with the corresponding playback-range ends **1535 → 3071 → 2303 → 1535 ticks**. A version-dependent raw cached-length write was removed in favor of FL's native arrangement refresh. Earlier intermediate failures were superseded; they are not counted as successful verification.

## Remaining limits

- **Note inspection discrepancy:** the saved Glass Satellites file and backup contain identical payloads for 5,341 notes, while a later live enumeration reported 5,076. The saved payload was preserved. The difference between live enumeration, load-time interpretation and subsequent edits remains unresolved; these records do not establish that every live note query reproduces the serialized count.
- FL 2025 still needs a live run. Other exact FL builds, plugin versions, drivers, display settings and render options require their own checks.
- A detected title/path/untitled change requires explicit reattachment. Attaching during project loading can succeed before the title settles and then correctly refuse the next operation. FL exposes no reliable project generation ID; identical untitled replacements and races with human edits cannot be ruled out.
- Missing assets, plugin licensing/recovery dialogs, alternate export settings, and every render cancellation/failure path have not all been exercised live. A structurally valid WAV is not proof of musical correctness or successful loading of every external asset.
- Measurements do not capture live channel PCM. Mixer stems may contain multiple instruments and sends, while master renders contain the complete mix. Source attribution must remain explicit.
- Analysis tests include synthetic calibration signals and real-file comparisons. True peak is labelled an estimate; PSR requires a complete three-second context. No claims are made about all loudness standards, arbitrary multichannel layouts, or perceptual sound quality.

## Reproduce on another exact build

Use a disposable project and licensed assets. Record the complete FL version, installer/SDK revisions, Windows version, and generator/effect versions.

1. Install a matching host/plugin, private runtime, and wheel; enable FL MCP and register the client. Confirm **27 tools** are advertised. Read `fl_python_docs` and `fl_python_api`.
2. Start `sessions/smoke.flp` using `fl_project_start`; verify PID, loaded project path, tempo and PPQ. Author notes and playlist clips, read them back, save a fresh snapshot, and reopen it.
3. Render a new WAV with `fl_project_render`. Check successful renderer exit, sample format, duration, finite samples, and expected audio content. Resume the returned snapshot via `sourceProjectPath` and verify earlier files remain unchanged.
4. In separate disposable projects, verify mixer insertion around routed/effected tracks and automation creation/editing/save/reopen. Render an envelope with a predictable level change and measure both regions.
5. Attach to saved and untitled user-owned sessions. Check snapshot identity/mode preservation, close/render refusal, exclusive leases, detach/handover, and client exit without terminating FL. Reattach after a detected project change.
6. Test Python cancellation, then a fresh SDK query. Verify cancellation does not permit later batch mutations or process teardown before active native work drains. Test missing assets and rendering timeouts separately; retain snapshots and inspect state before retrying.
7. Compare explicit audio sections with a reference analyzer. Record requested seconds, actual frames, sample rate, channel attribution, loudness/peak methods, and window settings. Do not compare PSR with peak-minus-RMS.

## Local evidence and automated checks

Raw receipts contain machine-local paths and separately licensed project/sample references. They are not public distribution requirements or linked download artifacts. Maintainer workspace evidence includes:

- FL-MCP `artifacts/live-attach-20260912-171336.json` and `live-attach-20260912-171354.json`: saved/untitled attachment.
- FL-MCP `artifacts/neon-render-20260912-180733.json`: sampler rendering and length regression.
- SDK `artifacts/glass-satellites-v2-20260912/verification.json`: 120-bar composition state.
- SDK `artifacts/mixer-creation-0.1.20/live-mixer-insertion.json` and `reopened-verification.json`: mixer insertion and persistence.
- SDK `artifacts/automation-analysis-0.1.21/automation-live.json`, `automation-reopened.json`, and `verify_render_offline.json`: automation and rendered level change.
- SDK `artifacts/embedded-analysis-0.1.22/release-verification.json`, `embedded-timing.json`, and `cancellation-verification.json`: installed patch, benchmark, and recovery.

At the 0.1.22 verification checkpoint, the SDK passed **382 managed tests**, **8 native test groups**, and **228 Python tests**; three isolated real-CPython patch tests also passed. The local installer passed 36 package checks. Those counts describe that checkpoint, not the current result of every future checkout. The adapter has its own [quality gate](building.md), including stdio, real named-pipe, lifecycle, policy and embedded-runtime coverage.
