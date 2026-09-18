# MCP friction log

Running backlog of anything that was not straightforward to do through FL MCP or the
embedded Python API while working on real projects. Add an entry the moment something
costs more than one attempt; batches of these get fixed together. Keep original evidence
(calls, readbacks, files) in the linked records rather than here.

Status: `open`, `in-progress`, `fixed-unverified` (source changed, not live-checked),
`fixed` (live-verified), `wontfix` (with reason), `caller` (was a caller mistake; keep for docs).

Entry template:

```
### <short title>
- Status: open
- Seen: <date>, <project>
- Call: <tool or Python call>
- Expected / actual: ...
- Workaround: ...
- Evidence: <file or record>
```

## 2026-09-13 — Ember Tides v006 batch

### Mixer pan scale documented wrong, negatives clamped
- Status: fixed (live: -3200 round trip, persisted)
- Evidence: examples/ember-tides/records/issues-v006.md #1, records/live-validation-2026-09-13.md

### No way to delete time markers; markers extend renders
- Status: fixed (delete_marker live-verified; with markers deleted and one 8-bar clip at tick 0 the render was 13.71 s instead of 178.3 s)
- Evidence: records/issues-v006.md #2

### Installed wheel / API catalog / Python names disagree
- Status: **fixed and live-verified (2026-09-17, FL 26.1.3.5570, managed session from the E-minor smoke song)**: `inspect.signature` shows `select_channel(*, channel: int)` / `set_channel_solo(*, channel: int)`; `fl.ops.invoke("select_channel", index=0)` still accepted through the legacy alias.
- Evidence: records/issues-v006.md #3

### Automation endpoints protected from deletion
- Status: fixed (set_point live-verified)

### Parameter display readback lags rapid writes in one request
- Status: docs/mitigated (2026-09-14 batch). No host fix: the raw value from the wrapper's `getParamValue` is immediate, the display text comes from the plugin instance and only syncs after FL's audio-thread/idle pass; no opcode to force it is known without live RE. `Parameters.set_verified` now compares decoded normalized values (never display strings) and keeps re-reading until the display moves (`VerifiedWrite.normalized_after`, `display_changed`).
- Evidence: records/issues-v006.md #5, records/live-validation-2026-09-13.md

### `query_plugin_parameters` filter appeared to ignore spaces
- Status: caller (limit bounds the scanned slots from offset). Docs updated; `Parameters.read(index)` added.

### `fl_project_start` schema lacked `background`; title "(untitled)"
- Status: **fixed and live-verified (2026-09-17)**: `fl_project_start(projectPath, background=true)` launched FL on a private desktop (pid 12744), reported the project path as the title (not "(untitled)"), settled tempo/PPQ in 2.1 s, and `fl_project_close` shut it down cleanly. Original note: for background (server rebuilt; the tool schema shown to the client
  did not include it until the client reconnected). Title: fixed (live).

### Permission classifier denied read-only `fl_python_api(filter="marker")`
- Status: open (harness). Workaround: read the SDK source.

### Oversized Python results and exceptions discarding partial results
- Status: **fixed and live-verified (2026-09-17, FL 26.1.3.5570, managed session from the E-minor smoke song)**: a 200 KB `result` came back as `{ok, oversized:true, totalBytes:200191, limitBytes:65536, path}` with the full JSON on disk; `resultPartial:true` observed on several failing scripts the same day. Nit: the envelope's `head`/`tail` are several KB each of the blob, which is most of the token cost the limit exists to avoid — cap them at a few hundred bytes.

### Serum preset loading: default class id ignored silently
- Status: fixed (deployed; shipped load_preset on "LD - Analog Glow" read back WT Pos 57 / 87% / detune 0.01). Working id is the GUID string order
  `56534558667350736572756D20320000`; memory order is ignored without error.
- Evidence: records/live-validation-2026-09-13.md

### Serum factory presets need processor-state normalisation
- Status: fixed (deployed and live-verified through load_preset; minimal identity subset still not isolated). Drop UI-only sections, add
  component/product/productVersion/version.

### Synthesized `.fst` did not load; raw `.SerumPreset` ignored
- Status: wontfix for synthesized .fst (documented unsupported; FL-saved .fst untested); raw .SerumPreset is converted automatically by load_preset.

### `use_channel_loader=True` renames the channel and can start playback
- Status: fixed (live-verified 2026-09-17: refusal path exercised; channel name unchanged, no playback start). Dispatcher route is the default.

### `load_state` verification line always says "0/12 sampled values changed"
- Status: **fixed and live-verified (2026-09-17, FL 26.1.3.5570, managed session from the E-minor smoke song)**: every load line today read e.g. `state record changed (13079 -> 14908 bytes, sha256 61eacd24 -> 8d80c6b8, 14231 differing bytes = 95.5%, first at byte ...)`, and an ignored file reads `state record unchanged (1271 bytes, sha256 ...)`.

### No render range / selection render; audition of one clip costs a full-length render
- Status: **fixed and live-verified (2026-09-17, FL 26.1.3.5570, managed session from the E-minor smoke song)** on a clean project: `fl_project_render(startBar=33, endBar=36, tailBeats=2)` kept 10 clips, deleted 145, removed the End marker, rendered 8.4375 s (expected 8.4375) in 7.1 s. See the new entry below for the case that hangs the renderer (retired capture channels pointing at deleted WAVs).

### Plugin waveform/wavetable identity not readable through parameters
- Status: **fixed and live-verified (2026-09-17, FL 26.1.3.5570, managed session from the E-minor smoke song)**: `describe_state(fl, 9)` names oscillator A's section, enable source, level in dB and the loaded table; covered with entry 26.

### Enum/scale mappings undiscoverable (unison count, detune, sub shape, filter cutoff)
- Status: partial (live 2026-09-17): `explain_parameter("Sub Shape", 0.25)` -> `roundrect` and the enum vocabulary serialises, but `explain_parameter("A Unison", 0.5)` still returns `known: False` because `tools/harvest_live.py` has never been run; unison/detune/filter-cutoff scales remain undiscoverable until the harvest is done.

### `PresetFile` has no `name` attribute; `vars()` fails on slotted records
- Status: **fixed and live-verified (2026-09-17, FL 26.1.3.5570, managed session from the E-minor smoke song)**: `find_presets(...)[0].name` = `PD - 1970s String Machine`, `to_dict()` keys `folder, format, name, path, relative_path, root`; `LoadResult.to_dict()` works.

### Duplicate-note addressing (channel/key/tick) can hit several notes
- Status: **fixed and live-verified (2026-09-17, FL 26.1.3.5570, managed session from the E-minor smoke song)**: two identical notes added, `notes.delete([NoteRef(10, 60, 0)])` refused (RemoteError) before any write, `allow_multiple=True` deleted both (returned 2).

### Preset/wavetable browsing across Serum folders from Python
- Status: **fixed and live-verified (2026-09-17, FL 26.1.3.5570, managed session from the E-minor smoke song)**: `list_folders`, `find_presets(folder=..., limit=)` and name resolution (`load_preset(fl, ch, "BA - Reese Square")`) used throughout the smoke song build.
  Factory/Lead; other roots and legacy libraries untested).

### Building a Serum patch from a plain description is not yet straightforward
- Status: fixed (SerumPatch builder + schema data + read-state, live-verified 2026-09-13; remaining uncertainties listed in the schema entry below)
- Seen: 2026-09-13, Ember Tides, asked "square wave with some reverb"
- Call: fruitylink_serum.loading.build_preset(base, overrides)
- Expected / actual: expected to describe a patch in musical terms; actual requires Serum's
  internal kParam names and units (mostly unknown), a base preset (defaults are omitted from
  files), a wavetable path plus frame position to pick a waveform, and the FXRack layout for
  effects is undecoded.
- Workaround: derive variants from an existing preset; use the Sub oscillator shape enum for
  simple waveforms; set effects on the mixer instead of inside Serum.
- Fix path: add a read-plugin-state operation (dispatcher getChunk) so a script can set a known
  FL parameter, dump the state and learn name/unit/range for all 685 named controls; generate a
  parameter map and wavetable frame names as extension data; add an init base and a high-level
  builder (osc/sub/filter/env/fx methods) on top.

## 2026-09-13 — Serum patch-builder batch (in progress)

### Serum state schema derived from the local library; several mappings rest on single data points
- Status: in-progress (`tools/harvest_live.py` write_phase/read_phase/apply and `data/mod-matrix.json` added, confidence fields stamped on every row; the live harvest itself is still pending, 2026-09-14 batch)
- Uncertain: kParamTablePos frame rule (one live point: 88.29 on a 166-frame table showed WT Pos 57);
  filter kParamType display names beyond MG Low 12 are pattern guesses; osc A/B/C kParamOctave FL
  mapping; synced LFO rate units; kRoundRect FL position; Default Shapes frame 5 may be a wide pulse.
- Fix path: with read-state now available, run a set-then-read harvest over FL's 685 named Serum
  params on a disposable project and stamp confidence=verified on every row it resolves.

### No native plugin "get state" opcode known; read-state goes through a temp project copy
- Status: fixed (live: Channel.get_state returned the 13,663-byte record; fruitylink_serum.read_state and snapshot_preset round-tripped).
  Slower than a native getChunk and depends on note validation passing; acceptable for now.

### `read_channel_state` (snapshot reader) crashes: 'ProjectInfo' object is not callable
- Status: fixed (live 2026-09-13; the state reader now uses `fl.project.info` as a property).
  Native `fl.channels[n].get_state()` and `fruitylink_serum.read_state` worked (13,663-byte record).
- Fix path: use `fl.project.info` without parentheses; add a test with a stub Studio whose info is a property.

### Builder round trip verified live (square lead + reverb + delay)
- Status: fixed (live). `SerumPatch(...).load(fl, 4)` then readbacks: WT Pos 4 (square frame), unison 4,
  detune 0.20, width 60, B +1 oct 30%, sub Triangle -1 oct 50%, MG Low 24 at 500 Hz / 20%, Env 50 ms / 400 ms,
  Macro 1 = 40, main -9 dB; plugin state shows FXRack0 = [Reverb, Delay]. Audition rendered: 13.71 s, L/R RMS -28.66/-28.82 dBFS, peak -15.6 dBFS (Ember-Tides/audition-v007-builder-square-lead.wav/.mp3).

## 2026-09-13 - Ember Tides v007 (first revision using preset tools)

### Loaded presets can silently bypass existing FL automation targets
- Status: **fixed and live-verified (2026-09-17, FL 26.1.3.5570, managed session from the E-minor smoke song)**: an automation clip on Pad's `A Level` (param 21) then `load_preset` -> warning `channel 8 automates plugin parameter 21 'A Level' (clip 'verify link' ...)`, `LoadResult.automation` lists the links (also one on mixer 5 volume, attribution `other`), `.signal_path` empty for a healthy pad.
- Seen: v007 draft; "KY - Smart Future" on channel 1
- Expected / actual: the song automates Serum Filter 1 Freq on that channel; the preset's oscillators have direct level 0 and feed FX buses, so the voice filter is out of the path and the automation did nothing audible (intro centroid 7.9 kHz vs 2.6 kHz).
- Workaround: keep the original patch where automation matters and add the new preset as a separate channel/layer.
- Fix path: load_preset could list FL automation links on the target channel and report which state keys they drive; the schema could flag presets whose osc direct levels are 0 or whose bus routing is active.

### Raw plugin-state hash is not a persistence oracle
- Status: caller/docs. Channel.get_state() bytes differ after reopen on untouched channels; decoded parameter diffs show only float noise. Compare decoded states (as done in state-before/reopened-v007.json).

### Multi-candidate audition in one render works but needed manual scaffolding
- Status: **fixed and live-verified (2026-09-17, FL 26.1.3.5570, managed session from the E-minor smoke song)** (layout only): `audition_candidates(fl, source_pattern=11, mixer_track=6, candidates=[...2 presets...], gap_bars=1)` returned `candidates[{index,label,channel,pattern,start_tick,end_tick,start_seconds,end_seconds,load}]`, `slot_ticks`, `notes_copied`, `markers_deleted`; the render itself was not run.

### Playlist track numbering collision
- Status: caller; helper fixed-unverified (`Playlist.first_free_track(start_tick, end_tick, above=1)`, counts pattern, audio and automation clips, 2026-09-14 batch). Placed stack clips on track 15, which already held an automation clip; moved them to track 20 and named it.

### Filter wet default is absent-in-file = 100 %
- Status: docs. A factory preset had kParamWet 0.0 explicitly (filter bypassed); after an override to 100 the reopened state omits the key (default). Builder docs should state which defaults are implied by absence.

### Multi-heredoc Bash command failed to parse in the harness
- Status: harness/caller. Four appended heredocs in one Bash call raised "unexpected EOF while looking for matching quote" and nothing ran; single-file writes via the Write tool were used instead.

## 2026-09-13 - Ember Tides v008

### NoteEdit field names differ from the natural guess
- Status: caller/docs. `NoteEdit(..., velocity=)` raised TypeError; the fields are `new_key`, `new_start_tick`, `new_length`, `new_velocity`, `muted`. Worth one line in fl_python_docs and an example.

### SerumPatch.master_volume(db=) suspected off by 3 dB - RETRACTED, builder is correct
- Status: caller. Live points 0.25 -> -9.0 dB, 0.3548 -> -6.0 dB, 0.5 -> -3.0 dB all satisfy display dB = 20*log10(value) + 3, which is exactly the builder's formula; the FL percent is sqrt(value). My mid-session "correction" was wrong. Keep these three points as a builder regression test.

### Notes longer than the clip extend the song and the render
- Status: docs. Outro chord/pad notes ending at tick 3832 inside an 8-bar (3072-tick) clip pushed the render from 192 s to 197 s. Useful as a tail here, surprising elsewhere; state_text songLength should be checked after long notes.

### Multi-candidate audition scaffolding again hand-written
- Status: **fixed and live-verified (2026-09-17, FL 26.1.3.5570, managed session from the E-minor smoke song)**: same call as entry 12.

### Per-section measurement is manual ffmpeg scripting
- Status: **fixed and live-verified (2026-09-17, FL 26.1.3.5570, managed session from the E-minor smoke song)**: `wav(...).describe_sections(128, {"first": (1, 2), "second": (3, 4)})` returned `provenance, grid, sections, band_definitions, ...` on the captured chorus.

## 2026-09-13 - Ember Tides v009

### add_mixer_effect plugin name must match the plugin database file name
- Status: **fixed and live-verified (2026-09-17, FL 26.1.3.5570, managed session from the E-minor smoke song)**: `effects[1].load("reeverb 2")` loaded Fruity Reeverb 2 (containment); `"Fruity Delay"` refused with `matches several installed plugins: 'Fruity Delay 2', 'Fruity Delay 3', 'Fruity Delay Bank'`; a miss lists the closest names.

### No band-energy check before committing a new voice
- Status: **fixed and live-verified (2026-09-17, FL 26.1.3.5570, managed session from the E-minor smoke song)**: `band_energy()` dict and `compare_bands(lead)` with `largest_increase/largest_decrease` on today's pad and lead stems.

## 2026-09-14 - Ember Tides v011

### Preset load resets global plugin parameters written just before it
- Status: **fixed and live-verified (2026-09-17, FL 26.1.3.5570, managed session from the E-minor smoke song)**: `SerumPatch().sub("sine").bend_range(12, 7).load(fl, 7)` then `parameters.page(filter="Bend")` -> `Bend Up = 12 semitones` in the same request.

### Automation clip point times are relative to the clip start
- Status: docs. fl.automation.create(...) at bar 105 then set_points with beats 0..40 mapped correctly to bars 105-115; earlier song-start clips made this look absolute.

### Pitch bend via Serum's Pitch Bend parameter works; channel pitch range is not exposed
- Status: docs. Automating plugin parameter 7 gave a clean two-octave glide (verified by f0 tracking). FL channel pitch automation would need the channel's pitch range knob, which the SDK does not expose.

### Verifying a time-varying effect needs an isolated render plus a pitch track
- Status: **fixed and live-verified (2026-09-17, FL 26.1.3.5570, managed session from the E-minor smoke song)**: `pitch_track(start_seconds=0, end_seconds=2)` returned provenance plus the track on the lead stem.

## 2026-09-14 - Ember Tides v012

### Channel-volume automation scale
- Status: docs (written up in docs/python/api.md "Scales and conventions", 2026-09-14 batch). AutomationTarget.channel_volume values 0..1 map to the 0..12800 channel scale (0.55 -> 7040 read back).

### No masking/clash check between two instruments
- Status: **fixed and live-verified (2026-09-17, FL 26.1.3.5570, managed session from the E-minor smoke song)**: `pad.masking_report(lead)` -> `clashes: ['250-2000']` with per-band louder/overlap.

## 2026-09-14 - Ember Tides v013

### Finding the empty spaces is manual
- Status: **fixed and live-verified (2026-09-17, FL 26.1.3.5570, managed session from the E-minor smoke song)**: `patterns[7].gaps()` (bass, no rest >= 1 beat -> 0 gaps) and `playlist.gaps(1, 8, channel=7)` -> one 3072-tick gap over the intro where the bass is absent.

## 2026-09-14 - Ember Tides v014/v015 (details, mix, master)

### Limiter gain parameter scale had to be probed
- Status: docs. Pro-L 2 "Gain" normalized 0..1 = 0..30 dB (0.5 -> +15, 0.6 -> +18, 0.1 -> +3). Same-request readback after a write showed the old value; the next request was correct. A per-plugin scale table (like the Serum parameter map) would remove this probing for FabFilter plugins.

### Mastering EQ skipped for lack of a cheap parameter map
- Status: open. Pro-Q 4 is installed but its band parameters would need probing; a FabFilter parameter-map generator (same set-then-read loop as Serum) is the fix.

### End-of-song silence via a marker
- Status: **fixed and live-verified (2026-09-17, FL 26.1.3.5570, managed session from the E-minor smoke song)**: `set_song_end(bar=97)` then `set_song_end(bar=98)` left exactly one `End` marker at the new tick.

### Sidechain pump needed 194 automation points by hand
- Status: **fixed and live-verified (2026-09-17, FL 26.1.3.5570, managed session from the E-minor smoke song)**: `automation.pump(AutomationTarget.mixer_volume(5), T(33), 4 bars, track=8, depth=0.4, ceiling=0.8)` -> 48 points, clip placed. Side finding: the automation CHANNEL keeps pinning that fader even after its clip is deleted (see the new entry below).

## 2026-09-14 - Ember Tides v016/v017

### Velocity does not scale some Serum patches
- Status: **fixed and live-verified (2026-09-17, FL 26.1.3.5570, managed session from the E-minor smoke song)**: sine sub with `velocity(1.0, target="amp")` measured -11.4 dBFS (vel 127) vs -16.7 dBFS (vel 40) per bar on a master capture; the same patch without the modulation measured -20.9 vs -20.9. `describe_state` lists `mod_slots[0].source = Velocity (id 16, inferred)`.

### Per-bar anomaly scan should be a helper
- Status: **fixed and live-verified (2026-09-17, FL 26.1.3.5570, managed session from the E-minor smoke song)**: `scan_bars(128)` returned `summary`, `anomalies`, paged bars on the captured chorus.

### Loudness target belongs in the mastering step's inputs
- Status: docs. The first master aimed at -10 LUFS by genre habit; the user wanted -14 (Spotify). Record the target in the project README and make the limiter helper take `target_lufs`.

## 2026-09-14 - Ember Tides v018

### Transition effects need an A/B transition measurement
- Status: **fixed and live-verified (2026-09-17, FL 26.1.3.5570, managed session from the E-minor smoke song)**: `v1.transition(v3, 128, 3)` returned `before/after/jump` deltas between two chorus captures.

## 2026-09-14 — Post-Ember-Tides fix batch

Six batches applied from source (SDK, serum-support extension, Fl-MCP server). Nothing is deployed
or live-checked yet: the installed wheel and the staged extension must be rebuilt first (memory:
installed wheel drifts from source). Batch reports are in the session scratchpad.

| Item | Status | New API / where |
| --- | --- | --- |
| Readback lag after rapid writes | fixed (live 2026-09-14: `set_verified(199, 0.8)` on Serum 2 -> verified, display Saw->Square, attempts 2) | `Parameters.set_verified` compares normalized values and waits for the display; `VerifiedWrite.normalized_after/display_changed`; `plugins.normalized_from_raw` |
| `load_state` "0/12 sampled" line | fixed (live: Smart Future 16269 differing bytes = 96.2%; same file again -> "unchanged") | `DescribeStateEvidence` sizes, SHA-256 prefixes, differing bytes (`FlInjectBridge.PluginState.cs`) |
| `index=` vs `channel=` | fixed (live: `channels[3].select()` and `fl.ops.invoke("select_channel", index=3)` accepted; no selected-channel readback exists) | `select_channel(channel=)`, `set_channel_solo(channel=)`; legacy `index` alias in `OperationRegistry` |
| Plugin names must match exactly | fixed (live: "fabfilter pror2" -> Pro-R 2, "Pro" lists 7 candidates, "Prooo-Z 9" lists 5 closest) | `PluginNameResolver` (exact, letters/digits, unique containment; near-miss list) in add/clone effect and add_channel |
| Duplicate-note addressing | fixed (live: stacked pair refused with lengths named, `length_tick` deletes one, `allow_multiple` deletes both) | `NoteRef/NoteEdit.length_tick`; `Notes.edit/delete(allow_multiple=False)` refuse multi-hits before writing |
| Exception discards partial result | fixed (live: `resultPartial:true` with result and stdout) | worker `resultPartial:true`; `encode_response` drops only `result` over 1 MiB (`resultDropped`) |
| Oversized Python results | fixed (live: 584,590-byte response -> envelope + complete JSON file; the 4096 env override was not tried) | MCP `FL_MCP_PYTHON_RESPONSE_LIMIT` (64 KiB default); envelope with head/tail and `path` under `%LOCALAPPDATA%\FlMcp\Projects\results` |
| Finding rests | fixed (live: pattern and playlist gaps match the note/clip layout to the tick) | `Pattern.gaps`, `Playlist.gaps` -> `Gap` records |
| Track collisions | fixed (live: 8 for bars 49-64 and 3 for bar 1, equal to the clip-derived answer) | `Playlist.first_free_track(start_tick, end_tick)` |
| Sidechain pump by hand | fixed (live: 24 points, dip on each beat, hold 1/96 beat before the hit; recovery tension sign not visually checked) | `Automation.pump(...)` -> `PumpResult`; pure `automation.pump_points` |
| Song end via marker | fixed (live: one End marker at 46080 then 46464 after re-run) | `Transport.set_song_end(bar)`; `Timebase.bar_start` |
| Per-bar scan, band checks, sections, transition, pitch, masking | fixed for scan_bars/describe_sections/transition (offline on the bars 49-64 render; pitch_track/masking_report not run live) | `fruitylink.analysis.scan_bars/band_energy/compare_bands/describe_sections/transition/pitch_track/masking_report` (also `AudioAnalysis` methods); `_kernels.py` numpy path; ranged `Loudness.integrated(begin, end)` |
| Bend range lost on preset load; velocity ignored | fixed (live: Bend Up 2 / Bend Down -12 read back; velocity 30 vs 127 render differs by 6.18 dB RMS) | `SerumPatch.bend_range`, `.velocity`, `.modulate`; `data/mod-matrix.json` |
| Preset load bypasses automation silently | fixed-unverified (Live 2026-09-14: FAIL, see entries below) | `load_preset -> LoadResult` (`warnings`, `automation`, `signal_path`); `loading.automation_links` |
| Wavetable identity unreadable | fixed (live: describe_state names Default Shapes frame 1 "saw" and the Velocity mod row; Serum UI not compared) | `describe.describe_state/describe_preset/format_description/signal_path_notes` |
| Multi-candidate audition scaffolding | fixed (live: two candidates laid out at 0 and 3456 ticks on track 50, channels routed; slot audio not measured in isolation) | `fruitylink_serum.audition.audition_candidates` |
| Schema mappings on single data points | in-progress (live harvest verified 16 anchors, applied to parameter-map.json; probe ordering bug below) | `tools/harvest_live.py` (write_phase/read_phase/apply); confidence fields in all serum-support data JSON |
| `vars()` on slotted records | fixed (live: `LoadResult.to_dict()` keys verification/path/warnings/automation/signal_path) | `IndexedPreset.to_dict`, `LoadResult.to_dict`; `ChannelState` exported |
| No render range | fixed (live: bars 49-64 refused without cutClips naming clips [3,4,5,6,7,8,43]; with cutClips 27.4286 s WAV = exactly 16 bars, no tail; -full.flp reopened) | `fruitylink.audition.isolate_range/isolate_bars`, `StudioSession.render_range`, `fl_project_render(startBar, endBar, cutClips)`; saves `<name>-full.flp` first |
| Extension staging | fixed-unverified | `installer/stage-serum-support.ps1` allowlist +`audition.py`, `describe.py`, `data/mod-matrix.json` |

Live verification checklist (one disposable FL project; Serum 2 on channel 4 unless stated):

- No FL needed: run `scan_bars`, `describe_sections`, `transition`, `pitch_track` and `masking_report` on the v018 master WAVs and compare with the hand ffmpeg numbers in the records.
- Rebuild the wheel, redeploy to `<FL>\FruityLink\tools\fl-mcp\python`, run `stage-serum-support.ps1` and its tests, restart FL, reconnect the MCP client.
- `result={'a':1}; raise ValueError` returns `result`, `resultPartial:true` and stdout.
- `result=[fl.channels[0].parameters.page(limit=200)]*20` returns the oversize envelope and the file at `path` is complete JSON; `FL_MCP_PYTHON_RESPONSE_LIMIT=4096` in the client env lowers the threshold.
- `fl.channels[3].select()` and an old-wheel `select_channel(index=3)` both select channel 3.
- `effects[0].load("FabFilter Pro-R 2")` loads Pro-R 2; `load("Pro")` errors listing Pro-C/L/Q/R.
- `set_verified(199, 0.8)` on Serum: `verified=True`, `display_changed=True`, `display_after="Square"` in one request; note `attempts`.
- `load_state` with a preset that changes the sound: evidence line shows a large differing-byte fraction; reload the same file: "unchanged".
- Stack two notes on one channel/key/tick: `delete([NoteRef(...)])` raises; with `length_tick` deletes one; `allow_multiple=True` deletes both.
- `set_song_end(121)`: one "End" marker at tick 120*4*PPQ, re-running moves it, render length extends.
- `first_free_track` on a playlist mixing pattern, audio and automation clips.
- `Playlist.gaps`: confirm `source_index` is the one-based pattern number against `list_clips`, including a sliced clip with an offset (documented unsupported).
- `pump` on `channel_volume` with `ceiling=0.78`: dip on each beat, vertical drop (hold point one tick before the hit), recovery shape as expected; if it curves the wrong way flip the `tension` default sign.
- `SerumPatch(name="vel").osc_a("saw", level=0.7).velocity(1.0).bend_range(2, 12)`: next request shows matrix row 1 Velocity -> Voice Amp, Bend Up 2 / Bend Down -12; render notes at velocity 30 and 127, RMS differs by > 6 dB.
- `describe_state(fl, 4)` names `Default Shapes` frame 1 (saw); compare with the Serum UI on a factory preset.
- `load_preset(fl, 4, "KY - Smart Future").warnings` lists the Filter 1 Freq automation clip and the direct-level note.
- `audition_candidates` with two presets and the chord pattern, one render; slots line up with `start_seconds`.
- `fl_project_render(outputPath, startBar=49, endBar=64)` on Ember Tides: WAV about 16 bars plus tails, `-full.flp` resumable, `range.kept_clips` plausible; a range starting mid-clip is refused; with `cutClips=true` audio/automation halves stay continuous (pattern half restarts); trailing markers no longer extend the audition; `SetLoopRegion` clear works on the running build.
- `harvest_live.write_phase`, then `read_phase` in a separate request, then `harvest_live.py apply`; mod probe with `probe_mod_matrix=True`.
- Optional: save an FLP with a timeline selection and CLI-render it; if honoured, a cheaper native render range exists.

Not fixed:

- Permission classifier denying read-only `fl_python_api` calls: harness, not ours.
- Serum mod-slot source ids (16 = Velocity etc.) and the wavetable frame rule are inferred from library statistics and one live point, not verified; the harvest above resolves them.
- FL's meter is not readable, so every bar-based helper defaults to `beats_per_bar=4`.
- FabFilter parameter map (Pro-L 2 gain, Pro-Q 4 bands) still needs the live set-then-read harvest; mastering EQ stays skipped.
- Native render range: FL's CLI has no range switch (`/R /E /F /O /D` only), so the disposable-project transform is used; a saved timeline selection is untested.

## 2026-09-14 — Live verification results

Disposable copies of `Ember-Tides-v018-master.flp` under `Ember-Tides/live-check-2026-09-14/`; full evidence in
`live-check.md` there. Deployed wheel `fruitylink_python-0.2.0`, staged `fruitylink_serum.whl`, FL 2026 (PPQ 96, 140 bpm).

| Item | Verdict | Evidence |
| --- | --- | --- |
| Partial result on exception | PASS | `ok:false, result:{a:1,tempo:140}, resultPartial:true, stdout:"read tempo"` |
| Oversize envelope + file | PASS | 584,590 B -> `oversized:true`, file under `Projects\results\` parses, 20 pages complete |
| `FL_MCP_PYTHON_RESPONSE_LIMIT=4096` | BLOCKED | needs client env + server restart |
| `select()` / wire `index=` alias | PASS | both accepted; Python kw is `channel=` now; no readback op |
| Tolerant plugin names | PASS | Pro-R 2 by exact and by "fabfilter pror2"; "Pro" -> 7 candidates; miss -> 5 closest; generators too |
| `set_verified(199, 0.8)` | PASS | verified, Saw->Square, `display_changed` True, attempts 2 |
| Note disambiguation | PASS | refusal names lengths (192, 96); `length_tick` -> 1; `allow_multiple` -> 2 |
| `set_song_end(121/122)` | PASS | single End marker at 46080 then 46464 |
| `first_free_track` | PASS | 8 (bars 49-64) and 3 (bar 1) equal the clip-derived answer, all clip kinds counted |
| `Pattern.gaps` / `Playlist.gaps` | PASS | scratch rests to the tick; pattern 144's 28-tick rests at 0.25 beat; `source_index` = pattern number |
| `pump(ceiling=0.78)` | PASS | 24 points: 0.28 on each beat, 0.78 held at beat-1/96, tension 0.5 on recovery (curve direction not eyeballed) |
| Builder `.velocity(1.0).bend_range(2, 12)` | PASS | Bend Up "2 semitones", Bend Down "-12 semitones"; vel 30 vs 127 = -17.41 vs -11.23 dBFS (6.18 dB) |
| `describe_state` | PASS | Default Shapes frame 1 "saw", mod row Velocity -> Global0/kParamVoiceAmp |
| `load_preset` warnings | FAIL | automation links all "event 0x…, unknown" (Filter 1 Freq clip not attributed); signal-path check crashes on Smart Future / Airy Chant |
| `load_state` evidence line | PASS | 96.2% differing bytes; same file again -> "unchanged" |
| `audition_candidates` | PASS (layout) | clips at 0 and 3456 on track 50, channels 29/30 -> mixer 15; slot audio not isolated |
| `harvest_live` write/read/apply | PARTIAL | 16 anchors verified and applied (27 rows); `probe_mod_matrix=True` corrupts the run (see entry) |
| `fl_project_render` range | PASS | refusal names clips; cutClips render = exactly 16 bars (no tail); `-full.flp` reopened |
| `scan_bars` / `describe_sections` / `transition` | PASS | flags bars 5 (<90 +11.5 dB) and 9 (>4000 +12 dB); bar-9 jump +4.42 dB vs master bar 57 +4.9 dB |
| Round 2: `load_preset` automation attribution | PASS | `0x180cd` -> `certain`, param 205 "Filter 1 Freq", warning names 'Ember - brightness arc' (ch 13); `0x18000` -> Main Vol; other channels' links `other` with decoded kind/index |
| Round 2: `signal_path_notes` / `describe_preset` | PASS | Smart Future: direct-levels-0 note, VoiceFilter0 wet 0 %, no-Velocity row; no "skipped" warning; Airy Chant describe returns a dict (BottleBlow.wav frame 53) |
| Round 2: `harvest_live` with `probe_mod_matrix=True` | PASS | ch 27: ordering `ok` (sentinel 0.49 -> 0.25), 5/5 verified; ch 28 `include_verified=True`: ordering `ok` (0.49 -> 0.36), 35/35 verified, 0 unresolved; apply not run |
| Round 2: `fl_project_render` `tailBeats=8` | PASS | `tail_ticks 768`, deleted_markers 10, 11,849,428 B; ffprobe 30.857146 s vs 30.857143 expected (18 bars); session closed |
| Round 2: `snapshot_preset(name=<path>)` | PASS | file at the requested `round2-snap.SerumPreset` path, 4,332 B, no %TEMP% detour |

### `automation_links` attributes nothing: FL reports link targets as event ids
- Status: fixed (live 2026-09-14 round 2: `0x180cd` row `certain`, parameter 205 "Filter 1 Freq", warning names clip 'Ember - brightness arc' on channel 13)
- Seen: 2026-09-14, live-check (Ember Tides v018 copy)
- Call: `fruitylink_serum.loading.automation_links(fl, 1)`; `load_preset(fl, 1, "KY - Smart Future").warnings`
- Expected / actual: the Filter 1 Freq clip on channel 1 listed as `certain`; actual: 10 rows `target "event 0x180cd"`, `attribution "unknown"`, warning "10 automation link(s) report only an event id". The plugin text never names the target, so the name matcher has nothing to match.
- Workaround: decode the id. Observed: `0x180cd` ch1, `0x480cd` ch4, `0x1080cd` ch16, `0x1880cd` ch24, all Filter 1 Freq (0xcd = 205); `0x48007` ch4 Pitch Bend (7) => plugin parameter link = `((channel << 4) | 8) << 16 | 0x8000 | param_index`. Built-in: `0x30000` ch3 volume, `0x160000` ch22 volume, `0x18000`/`0x58000` ch1/ch5 (fade clips), `0x70401fc0` mixer track 1 volume.
- Evidence: `live-check-2026-09-14/live-check.md` section 7

### `signal_path_notes` crashes when `SubOsc4/plainParams` is the string "default"
- Status: fixed (live 2026-09-14 round 2: Smart Future `signal_path` has the direct-level note and no "skipped" warning; `describe.describe_preset("PD - Airy Chant")` returns a dict)
- Seen: 2026-09-14, live-check
- Call: `describe.signal_path_notes(read_preset("KY - Smart Future").state)` (also via `load_preset` warnings and `audition_candidates`)
- Expected / actual: the "all enabled oscillator direct levels are 0" note; actual `AttributeError: 'str' object has no attribute 'get'` at `describe.py:130` (`_oscillator`, `shape.get("plainParams", {}).get("kParamShape")`). Those presets store `Oscillator4 = {'SubOsc4': {'plainParams': 'default'}, 'plainParams': {'kParamVolume': 0.75}}`. "PD - Airy Chant" same; "LD - Analog Glow" fine. `load_preset` swallows it as a warning, so the intended warning is silently missing.
- Workaround: none from the caller.
- Evidence: live-check.md section 7

### `harvest_live.write_phase(probe_mod_matrix=True)` loads the probe patch after the parameter writes
- Status: fixed (live 2026-09-14 round 2: `probe_mod_matrix=True` on fresh channels 27/28 -> `ordering.status "ok"`, 5/5 then 35/35 verified with `include_verified=True`)
- Seen: 2026-09-14, live-check
- Call: `harvest_live.write_phase(fl, channel=27, out_dir=..., probe_mod_matrix=True)` then `read_phase`
- Expected / actual: 21 anchors verified plus the mod probe; actual 6 verified / 15 `changed_other_key` with contradictory rows (Env 1 Attack state 2.488 s vs display "1.0 ms"): `patch.load` replaces the whole state after `set_named` ran, exactly what the new load warning describes. A second run without the probe verified 16; the remaining 5 (Env 1 A/D/R, Macro 1, LFO 1 Rate) were already at the probe value from run 1, so the diff was empty (baseline contamination, not a mapping failure).
- Workaround: run the probe on another channel or before the writes; use a fresh channel per run.
- Evidence: `live-check-2026-09-14/harvest/harvest-20260914-125448-result.json`, `-125627-result.json`

### Range render has no tail
- Status: fixed (live 2026-09-14 round 2: bars 49-64 `tailBeats=8` -> `tail_ticks 768`, ffprobe 30.857146 s = 18 bars at 140 bpm)
- Seen: 2026-09-14, live-check
- Call: `fl_project_render(startBar=49, endBar=64, cutClips=true)`
- Expected / actual: "about 16 bars plus tails"; actual 27.428563 s = exactly 16 bars: the isolate transform deletes the markers, so FL stops at the last clip end and reverb/release tails are cut. A short pattern in the last bar shortens it further (the 121-124 render was 3 bars long because the only clip was 3 bars).
- Workaround: add an empty clip or `set_song_end` past the range before rendering; consider a `tail_bars` option.
- Evidence: live-check.md section 8

### `snapshot_preset` ignores the requested directory
- Status: fixed (live 2026-09-14 round 2: `snapshot_preset(fl, 1, name=<...
ound2-snap.SerumPreset>)` wrote 4,332 bytes at exactly that path)
- Seen: 2026-09-14, live-check
- Call: `snapshot_preset(fl, 1, r"...\live-check-2026-09-14\velvet-ch1-snapshot.SerumPreset")`
- Expected / actual: file at that path; actual `%TEMP%\fruitylink-serum-h2ftsago\C_Users_poofi_AppData_Local_..._velvet-ch1-snapshot.SerumPreset.SerumPreset` (path flattened into the name, extension doubled). It reloaded fine.
- Workaround: pass a bare name and copy the returned path.
- Evidence: live-check.md section 7

### `fl.channels.add` returns a `Channel`, docs/checklists assume an index
- Status: caller
- Seen: 2026-09-14, live-check
- Call: `idx = fl.channels.add("Serum 2"); fl.channels[idx]`
- Expected / actual: `IndexError: Index must be an integer >= 0`; use `.index` (a channel was still added).
- Evidence: live-check.md section 7

### No way to read the selected channel; effect-slot `parameters.page().total` reports 4240 for Pro-R 2
- Status: open
- Seen: 2026-09-14, live-check
- Call: `fl.ops.invoke("select_channel", index=3)`; `fl.mixer[16].effects[0].parameters.page(limit=4)`
- Expected / actual: a `get_selected_channel` to prove the alias works (only acceptance can be checked); the Pro-R 2 slot reports `total 4240`, identical to Serum 2's count (wrapper cap or wrong count). `list_mixer_effects` also lists VST slots as "Fruity Wrapper".
- Evidence: live-check.md sections 2 and 3

### `harvest_live` is not shipped in the Serum wheel; `describe_preset` lives in `describe`, not `loading`
- Status: open (docs/packaging)
- Seen: 2026-09-14, live-check round 2
- Call: `import fruitylink_serum.harvest_live` / `fruitylink_serum.loading.describe_preset(...)`
- Expected / actual: the installed `fruitylink_serum.whl` has no harvest tool (modules: analysis, audition, builder, cbor, describe, inventory, loading, schema, state_reader, vstpreset, xfer), so the repo `extensions/serum-support/tools/harvest_live.py` had to be imported by `sys.path`; `describe_preset` raised `AttributeError` on `loading` and is `fruitylink_serum.describe.describe_preset`.
- Workaround: `sys.path.insert(0, r"<sdk>\extensions\serum-support\tools"); import harvest_live` inside FL; call `describe.describe_preset`.
- Evidence: live-check.md "Round 2"

## 2026-09-14 — Parking Lot Moon

### Plugin state read/load fails with "FL event 254 is truncated" on every channel (FL build 26.1.3.5570)
- Status: fixed (live 2026-09-14: check 7 -- `fl.channels[1].get_state()` returned 17,640 bytes (head `0c00000001000000`) with no error, and a fresh build-4726 template session read 13,079 bytes off a new Serum 2 channel; deployed 2026-09-14 from stage deploy-20260914-h, backup-installed-20260914-174218; root cause: FL 5570 writes event 0xAC as tag + u16 (tag 1) or tag + u32 (tag 0), `FlpPluginStateReader` now tries tagged then legacy framing and accepts only a walk that lands on the chunk end (44/44 FL-5570 FLPs under FlMcp\Projects parse), `ExtractStateAsync` retries once after a 250 ms settle and `TryRead*StateRecordAsync` tolerate `InvalidDataException` so a load is still applied when its evidence snapshot cannot be parsed)
- Seen: 2026-09-14, Parking Lot Moon phase 1, managed interactive session on Parking-Lot-Moon-v001.flp (template saved by build 4726, live FL 5570)
- Call: `loading.load_preset(fl, 1, "LD - Lush and Vintage")`; `fl.channels[1].get_state()`; same on GMS (ch 4) and 3x Osc (ch 7)
- Expected / actual: LoadResult / state bytes; actual `RemoteError: operation_failed: FL event 254 is truncated.` from `FlpPluginStateReader.ReadPayload` on the temp project snapshot. `LoadChannelPluginStateAsync` takes the "before" snapshot *before* dispatching the load and `TryReadChannelStateRecordAsync` only catches InvalidOperationException/IOException, so an InvalidDataException aborts the load and nothing is applied. Parsing the same snapshot (`scratch/state-probe-v001.flp`, saved with `fl.project.save_copy`) with the reader's algorithm shows the desync: FL 5570 writes event 0xAC (172) with a 3-byte payload at offsets 48 and 182 (`ac 01 01 00 | c0 36 "FL Studio 26.1.3.5570.5570"` and `ac 00 01 00 | 00 ed 10 <ProjectTime>`), the reader assumes 4 bytes for ids 128..191, runs one byte ahead and eventually reads garbage (`fe ff ff 40` -> event 254, length 1,064,959 in a 135 KB file). Ember-Tides-v018-master.flp (same build) has the same bytes but happens to resync, which is why state reads worked there. The template v001 (build 4726) has no 0xAC event and parses cleanly.
- Workaround: close to a fresh vNNN and reopen from it (the FLP written by FL 5570 through the close path parses), then load presets. Before that was known, the Serum 2 sounds were authored with `set_plugin_param` on named wrapper parameters (filter, unison, envelopes, FX) and read back with `parameters.page`, not `describe_state`. Persistence check compares parameter displays instead of state bytes.
- Evidence: `Parking-Lot-Moon/scratch/state-probe-v001.flp`; parser transcript in this session; `sdk/src/FruityLink.FlStudio/FlpPluginStateReader.cs:114`, `FlInjectBridge.PluginState.cs:34,188`

### Sample channels report "hosts no generator plugin (automation/bus channel)" for get_state
- Status: fixed (live 2026-09-14: check 8 -- channel 8 now reports "it is a built-in Sampler channel (or an audio clip / layer) ... replace_channel_sample", channel 21 "it is an automation clip (automates event 0x71008030)"; deployed 2026-09-14 from stage deploy-20260914-h, backup-installed-20260914-174218; `RequireGeneratorAsync` classifies a gen < 0 channel through the automation-target registry: "it is an automation clip (automates X) and hosts no plugin" or "it is a built-in Sampler channel (or an audio clip / layer) ... replace_channel_sample ..."; used by get_state and load_state)
- Seen: 2026-09-14, Parking Lot Moon phase 1
- Call: `fl.channels[8].get_state()` on a Sampler channel loaded with `fl.channels.add_sample`
- Expected / actual: a message saying Sampler channels keep no wrapper state; actual "Channel 8 hosts no generator plugin (automation/bus channel)", which misclassifies a sample channel.
- Workaround: ignore; sample channels have no plugin state to read.
- Evidence: this session

### Template mixer has 16 inserts; naming insert 17 is refused
- Status: fixed (live 2026-09-14: check 19 -- insert 25 refused with "Insert 25 does not exist yet ... ensure_inserts(25)", `ensure_inserts` added 1 and insert 25 took the name "Vox"; the fresh template variant read before 16 / capacity 500; deployed 2026-09-14 from stage deploy-20260914-h, backup-installed-20260914-174218; `fl.mixer.insert_count` (native count - 2), `fl.mixer.capacity` (500), `fl.mixer.ensure_inserts(n)` grows the mixer through the existing `add_mixer_track` and returns how many were added; the bridge range error now names the insert count, the capacity and `ensure_inserts(17)`)
- Seen: 2026-09-14, Parking Lot Moon phase 1
- Call: `fl.mixer[17].name = "Vox"` on the default template (`get_mixer_track_count` = 18 including Master and Current)
- Expected / actual: rename; actual `invalid_arguments: Mixer track must be 0..16 (Master and ordinary inserts); Current and dormant slots are unavailable`. The loop aborted mid-way but inserts 1..16 were renamed.
- Workaround: `fl.mixer.add("Vox")` returned index 17, then route to it.
- Evidence: this session

### `inventory.query_index(text=...)` misses single-word queries that match preset names
- Status: fixed (live 2026-09-14: check 9 -- all six single-word queries returned hits; "cotton" -> ["LD - Analog Crispy Cotton", "PD - Analog Soft Cotton"], "soft" == "Soft" (5 hits, case-insensitive); deployed 2026-09-14 from stage deploy-20260914-h, backup-installed-20260914-174218; root cause not reproduced from the venv (the real presets.db returns 25 rows for "soft"), so the text filter now runs in Python: every query word must occur case-insensitively in name/location/description/comment/author/tags, SQL keeps the category/tag filters, `limit` applies after filtering; `test_inventory_words.py`)
- Seen: 2026-09-14, Parking Lot Moon phase 1
- Call: `inventory.query_index(root, text="soft")`, `"Soft"`, `"cotton"`, `"analog"`, `"warm"`, `"tape"`, `"80s"` (root = Serum 2 Presets)
- Expected / actual: at least "PD - Analog Soft Cotton" / "PD - Analog Butter" ("You want warm pads"); actual empty tuples, while `text="analog pad"`, `"dream"`, `"synthwave"`, `"nostalgic"`, `"retro"` return rows.
- Workaround: browse with `category="Pad"` / `"Lead"` / `"Keyboard"` (limit 200) and filter `location` in Python.
- Evidence: this session

### No channel delete: the template's empty "Sampler" channel stays in the rack
- Status: wontfix (delete/clone/move are UI-only `TFruityLoopsMainForm.ChannelMenuPopup` commands with no engine call, and FL's own scripting API has none either; workaround: `Channel.retire(name=None)` / `fl.channels.retire(index)` mutes, routes to Master and renames "(unused) <old name>" (idempotent, returns the new name), api.md "Retire a channel")
- Seen: 2026-09-14, Parking Lot Moon phase 1
- Call: catalog search for delete/remove channel; none exists (only `clear_pattern`, `delete_clip(s)`, `remove_mixer_effect`)
- Expected / actual: a way to drop channel 0 after adding real channels; actual it stays, renamed "(unused template)" and routed to Master.
- Workaround: rename and ignore; note in records.
- Evidence: fl_python_api catalog

### Drum-insert routing to a bus cannot be verified: no send/route readback
- Status: fixed (live 2026-09-14: check 18 -- `fl.mixer[8].sends()` read [(0 Master, 0.8), (6 Bass, 0), (15 Drum Bus, 0.8)], `disconnect(0)` removed the Master row with no dialog and no stall, `fl.mixer.routes()` 49; deployed 2026-09-14 from stage deploy-20260914-h, backup-installed-20260914-174218; new structured op `query_mixer_sends(track)` (C# `IFlStructuredQuery.QueryMixerSendsAsync`, `FlMixerSendInfo(Source, Destination, DestinationName, Level, Active)`, only active sends, level = native/16000); Python `MixerTrack.sends()`, `send_level(destination)`, `fl.mixer.routes()`, `MixerSendInfo.level_db`; `set_mixer_send(..., active=True)` (FL route-active core 0/1) and `MixerTrack.disconnect(destination)`; a sidechain-flagged route is indistinguishable from a plain send)
- Seen: 2026-09-14, Parking Lot Moon phase 1
- Call: `fl.mixer[i].send_to(15, 1.0)` then `fl.mixer[i].send_to(0, 0.0)` for inserts 8..14 (kick..perc -> "Drum Bus")
- Expected / actual: both accepted, but there is no `get_mixer_send` / route query, so whether the master route is actually disabled (or only its level zeroed) is unknown until a render.
- Workaround: phase 2 should confirm in the FL mixer or by rendering the drum bus muted.
- Evidence: fl_python_api catalog (only `set_mixer_send`)

### `playlist.add_patterns` ignores `PatternClipSpec.length_tick`: clips take the pattern's own length
- Status: fixed (live 2026-09-14: check 25 -- both new clips read `length_tick` 3072 straight after `add_patterns`, `fixed == 0`, no resize call; deployed 2026-09-14 from stage deploy-20260914-h, backup-installed-20260914-174218; C# root cause: `AddPatternClipsAsync` now pins an explicit `LengthTick` through `FLpl_SetClipSourceRange` + the +0x08 poke (shared `PinClipLengthAsync`) right after each insert, before the pattern refresh that re-derived the length; `length_tick <= 0` (new default 0) follows the pattern; safety net `fl.playlist.add_patterns(specs, enforce_lengths=True)` re-lists and resizes any clip whose length still differs and returns the count)
- Seen: 2026-09-14, Parking Lot Moon phase 1
- Call: `fl.playlist.add_patterns([PatternClipSpec(36, 9, 0, 3072), ...])` (75 clips)
- Expected / actual: 8-bar clips; actual `fl.clips.list()` reports the pattern length instead (Tex Bed 3840 = 10 bars because its last hiss note ends at tick 3840; Pad Intro 3456 because legato pad notes overhang the bar by 8 ticks; humanised drum hits push kits to 9 bars), so consecutive clips on one track overlapped by 1-2 bars.
- Workaround: `fl.clips.resize([ClipResize(index, length_tick), ...])` afterwards (works, verified 3840 -> 3072); or keep every note strictly inside the intended bars.
- Evidence: this session (clip 1 before/after)

### `fl.clips.resize` takes a sequence of `ClipResize`, not `(index, length)`
- Status: fixed (live 2026-09-14: check 26 -- `resize(index, 1536)` then `resize([(index, 3072)])` read back (1536, 3072); deployed 2026-09-14 from stage deploy-20260914-h, backup-installed-20260914-174218; `fl.clips.resize(index, length_tick)`, `resize([(index, length_tick), ...])` or records; same forms for `move(index, start_tick, track)`; `delete(7)` and `set_muted(7, True)` accept a single index; mixed forms raise `TypeError` before any request; docstrings on `ClipResize`/`ClipMove`, api.md)
- Seen: 2026-09-14, Parking Lot Moon phase 1
- Call: `fl.clips.resize(1, 3072)`
- Expected / actual: resize; actual `TypeError: Clips.resize() takes 2 positional arguments but 3 were given`; signature is `resize(resizes: Sequence[ClipResize])`. Same shape for `move`.
- Workaround: `from fruitylink import ClipResize; fl.clips.resize([ClipResize(1, 3072)])`.
- Evidence: this session

### `parameters.page(limit=4240)` refused: page limit is 512
- Status: fixed (live 2026-09-14: check 10 -- `all()` returned 4240 rows in one request and `page(limit=4240)` raised an IndexError naming the 512 host cap and `all()`/`list()`/`iter()`; deployed 2026-09-14 from stage deploy-20260914-h, backup-installed-20260914-174218; `Parameters.all(filter=None, unique=False)` pages automatically (alias of `list()`), `plugins.PAGE_LIMIT = 512`, `page(limit>512)` raises an `IndexError` naming the cap and `all()`)
- Seen: 2026-09-14, Parking Lot Moon phase 1
- Call: `fl.channels[1].parameters.page(offset=0, limit=4240)` to dump Serum 2's parameter names
- Expected / actual: one page (docs only say "bounded reads"); actual `IndexError: Index must be an integer >= 1 and <= 512.`
- Workaround: loop on `next_offset` with limit 512 (9 requests inside one fl_execute_python, ~4240 rows in well under the timeout) and dump to `scratch/serum2-params-ch1.json`.
- Evidence: this session

### Serum 2 through the wrapper: FX slots are only "FX Main Param 1..16", oscillator wavetables and sub shape are unnamed enums
- Status: **fixed and live-verified (2026-09-17, FL 26.1.3.5570, managed session from the E-minor smoke song)**: `explain_parameters(fl, 9, filter="Sub Shape")` now round-trips through the worker (`values` keyed `"0.0": "sine", ... "1.0": "pulse"`, `value: sine`); `describe_state` returns `channel, envelopes, filters, fx, global, lfos, macros, mod_slots, ...`. FX proxy slots stay opaque as documented.
- Seen: 2026-09-14, Parking Lot Moon phase 1 (preset loading blocked, so patches were authored by parameter)
- Call: `set_plugin_param` on Serum 2 channels; parameter dump `scratch/serum2-params-ch1.json`
- Expected / actual: a way to enable/choose an effect (chorus) or a wavetable frame by name; actual the effect rack exposes 16 anonymous "FX Main Param n" per bus and no effect-type/enable parameter, `A WT Pos` is a bare frame index, `Sub Shape` is an enum whose order had to be probed by writing values (0.25 -> "RoundRect"). Calibration (0..1 -> display): Filter 1 Freq 0.5=425 Hz, 0.6=937, 0.7=2064, 0.8=4549, 0.9=10025; Env Attack 0.3=78 ms, 0.4=328 ms, 0.5=1.00 s, 0.6=2.49 s; Env Release 0.4=328 ms, 0.5=1.00 s, 0.6=2.49 s, 0.7=5.38 s; Unison = 1+15v; Uni Detune = v^2; Uni Width = 200v-100; Fine = 200v-100 cents; Sustain 0.9=-1.8 dB, 1.0=0 dB.
- Workaround: chorus/reverb for the lead come from mixer effects in phase 2; wavetable stays the init saw frame.
- Evidence: this session; `scratch/serum2-params-ch1.json`

### Stock effect parameter names carry a "^b^a" prefix
- Status: fixed (live 2026-09-14: check 12 -- Reeverb 2 rows read "Low cut"/"High cut" with `raw_name` "^b^aLow cut", `find("Wet level")` -> 12; deployed 2026-09-14 from stage deploy-20260914-h, backup-installed-20260914-174218; `plugins.clean_parameter_name` strips `^X` codes and `^^hint ^` blocks, `PluginParameterInfo.name` is clean and `raw_name` keeps the original, `find`/`set_named`/`set_verified` accept either form; Python-side only, works against any host)
- Seen: 2026-09-14, Parking Lot Moon phase 2
- Call: `fl.mixer[4].effects[1].parameters.page()` on Fruity Chorus / Reeverb 2 / Delay 3 / Parametric EQ 2 / Limiter / Compressor / Soft Clipper / Stereo Shaper / Hyper Chorus / Vintage Chorus
- Expected / actual: names such as "Wet level"; actual every stock (non-wrapper) parameter is named "^b^aWet level" (FL hint-formatting bytes), Vintage Chorus band 0 is "^b^a^^(shift-click for I + II) ^Mode". `find(name)` / `set_named(name, v)` therefore need the prefix; wrapper (FabFilter) names are clean.
- Workaround: address stock parameters by index (dumped once per plugin) and verify with `set_verified(index, v)`; strip `^.` sequences before matching names.
- Evidence: this session (phase-2 dumps in `records/phase-2-mix.md`)

### Send levels ARE readable (effects.list_text "sends:" line); unity is 0.8, not 1.0
- Status: fixed (live 2026-09-14: check 18 -- send levels read back 0.8 and 0 through `sends()` and the `effects.list_text()` "sends:" line agrees; deployed 2026-09-14 from stage deploy-20260914-h, backup-installed-20260914-174218; `fruitylink.SEND_UNITY = 0.8`, `send_level_to_db`/`send_level_from_db` in `fruitylink.levels`, `MixerTrack.send_to(destination, level=1.0, *, db=None, active=True)` (default kept for compatibility, docstring says 1.0 is about +5.6 dB and steers to 0.8), typed readback via `sends()` instead of the "sends:" text line; see the send/route readback entry above)
- Seen: 2026-09-14, Parking Lot Moon phase 2
- Call: `fl.mixer[8].effects.list_text()` -> "sends: ->0 'Master' (0), ->15 'Drum Bus' (1)"; untouched inserts read "->0 'Master' (0.8)"
- Expected / actual: `set_mixer_send` docs say "1.0 ≈ unity"; actual the default Master route of every insert reads 0.8, so 1.0 is above unity (FL send/volume scale 0..16000 with 12800 = 0 dB = 0.8). Phase 1 had the seven drum inserts feeding the Drum Bus at 1.0 (hot); the Master route at 0 is confirmed off (level 0, no flag readable).
- Workaround: `send_to(dst, 0.8)` for unity; check the "sends:" line after every routing write. The docstring should say 0.8 = unity and mention the readback.
- Evidence: this session

### No Sampler channel-settings ops: time stretch, sample start/end, fades, reverse, stretch mode
- Status: **fixed and live-verified (2026-09-17, FL 26.1.3.5570, managed session from the E-minor smoke song)**: `Channel.stretch_time` 0 -> 500 -> 0 round trip and `sample_offset` read on the kick channel; the int-key serialisation failure is gone (results keyed by `str`).
- Seen: 2026-09-14, Parking Lot Moon phase 2 (Vox E8 loop is 112 BPM, brief asks for 100 BPM; roomtone is 70 s under an 8-bar intro)
- Call: catalog search of the 144 ops for stretch/tempo/sample start/fade/reverse; only `add_sample_channel`, `replace_channel_sample`, `set_channel_pitch` exist
- Expected / actual: a way to set the Sampler's time-stretch (mode + tempo/multiplier) or trim/fade/reverse the sample; actual nothing, and Edison is GUI-only.
- Workaround: audio edited offline in embedded Python with the `wave` module (no numpy in FL's Python 3.14): `Samples\Tex_Roomtone_Suburb-8bars.wav` (19.2 s, 1 s fade in / 2.5 s fade out) swapped in with `replace_channel_sample`; `Samples\Vox_E8_everywhere_112_Gbm-reversed.wav` (last 4.8 s reversed, faded). The vocal stays at 112 BPM: it only plays in the drum-less interlude, so the tempo mismatch is a texture, not a rhythm error; a pure-Python WSOLA was judged too slow. Phase 3/4 may set stretch in the GUI.
- Evidence: this session

### Fruity Delay 3 exposes three parameters named "Distortion" (18, 19, 20)
- Status: fixed (live 2026-09-14: check 22 -- `LookupError: ... found 3 at indices 18, 19, 20 ...`, "Distortion [19]" wrote index 19, `all(unique=True)` lists the three bracketed names; deployed 2026-09-14 from stage deploy-20260914-h, backup-installed-20260914-174218; `find`/`set_named` collect duplicates and raise `LookupError: ... found 3 at indices 18, 19, 20 ...` before any write; `find("Distortion [19]")` / `set_named("Distortion [20]", v)` address one; `unique_names(rows)` and `all(unique=True)` list them as `Distortion [18]`; section names such as "(Drive)" are not knowable from the plugin; the Delay 3 calibration is in `data/parameter-scales.json`)
- Seen: 2026-09-14, Parking Lot Moon phase 2
- Call: `fl.mixer[4].effects[3].parameters.page()`
- Expected / actual: unique names; actual indices 18-20 all read "^b^aDistortion", so `set_named`/`find` cannot address them.
- Workaround: indices. Calibration for the record: Time = 16v steps ("3:1" = dotted 1/8 at v 0.1875), Feedback = 125v %, Output wet/dry linear %, Feedback cutoff 0.55 -> 2437 Hz, 0.57 -> 2893 Hz, 0.6 -> 3577 Hz.
- Evidence: this session

### `set_verified` reports verified=False when the slot already holds the value
- Status: **fixed and live-verified (2026-09-17, installed build)**: `set_verified(0, 0.5)` on Fruity Limiter Gain already at raw 1000 returned `verified=True, attempts=1, unchanged=True` on two consecutive calls. Failed live again on 2026-09-17: `mixer[0].effects[0].parameters.set_verified(0, 0.5)` on Fruity Limiter Gain already at raw 1000 (display `0.0dB`) returned `verified=False, attempts=6, raw_before=1000, raw_after=1000, normalized_after=None, display_changed=False` twice in a row (the `unchanged` short-circuit only fired when `normalized_after` was available, and this row reads `rawValue 1000, normalized null`); a moving write (`0.7` -> raw 1400, `7.5dB`) returned `verified=True, attempts=2` as it should.
- Seen: 2026-09-14, Parking Lot Moon phase 2
- Call: `parameters.set_verified(2, 1.0)` on Delay 3 "Tempo sync" (already On), "Output dry" (already 100%), Hyper Chorus "Modulation amount" (already 50%)
- Expected / actual: verified=True (value is in place); actual `verified=False, display_changed=False` because the raw value never changed, which reads like a failed write in a batch summary.
- Workaround: treat `normalized_after == value` as success; compare displays. A `already_set` flag on VerifiedWrite would remove the ambiguity.
- Fix path: `Parameters.set_verified` now also short-circuits when the normalized readback is UNAVAILABLE: after an accepted write, a first readback whose raw integer and display string both equal the pre-write ones means nothing had to change, so the result is `unchanged=True, verified=True, attempts=1` (`normalized_after` stays None). A readable normalized value keeps the old behaviour, and an undecodable raw value that stands still while the display MOVES is still reported `verified=False` -- something moved that the raw oracle cannot confirm. `python/src/fruitylink/plugins.py`; tests `test_set_verified_on_an_undecodable_scale_that_already_holds_the_value`, `..._that_moves_is_unaffected`, `test_set_verified_still_reports_unverified_when_only_the_display_moves` (`python/tests/test_parameters.py`, 39 tests).
- Evidence: this session; the 2026-09-17 live run (Fruity Limiter Gain, two consecutive calls)

### No sidechain routing: Fruity Limiter has no sidechain-source parameter, Pro-C 3 "External" has no source
- Status: wontfix (FL's "Sidechain to this track" is a route flag that is not in the verified `FlMixerLayout` (send records hold only level int32 @0 and active byte @4; the RE notes a per-track table at +0x12A4 and an FX sub-table at +0x158, neither profiled), `FLmx_SetRouteActiveCore` has no sidechain argument and can raise a "Disable routing?" dialog, so a send always sums audio; documented in the `set_mixer_send` docstring and api.md "Verify sends and bus routing"; workaround: `fl.automation.pump(AutomationTarget.mixer_volume(insert), ...)` or `fl.automation.duck(...)` keyed to `fl.playlist.onsets(kick, ...)`, or flip the route in the GUI; RE lead: resolve the +0x12A4 table for 26.1.3.5570)
- Seen: 2026-09-14, Parking Lot Moon phase 2 (brief: Fruity Limiter COMP on the bass keyed from the Kick insert)
- Call: `fl.mixer[6].effects[2].parameters.page()` on Fruity Limiter (18 parameters: gain, sat, limiter, comp threshold/ratio/knee/attack/release/curve/RMS, noise gate; no sidechain input); `Pro-C 3` parameter 21 "Side Chain Input" accepts 0.25..0.34 -> "External" (0 Internal, 0.5 Host Sync, 1.0 MIDI); catalog has only `set_mixer_send(src, dst, level)` with no sidechain flag
- Expected / actual: a way to mark the 8 -> 6 route as a sidechain (FL right-click "Sidechain to this track") so the limiter/Pro-C 3 sees the kick; actual a plain send would sum the kick into the bass insert, and the wrapper's sidechain input stays silent, so Pro-C 3 External would never compress.
- Workaround: Fruity Limiter left in COMP mode on insert 6 with sidechain-ready settings (threshold -11.8 dB, 1:3.0, knee 40 %, attack 3.08 ms, release 163 ms) acting on the bass itself; Pro-C 3 removed; route 8 -> 6 created at level 0 as a placeholder. Phase 3 can emulate the duck with a mixer-volume automation clip on insert 6 keyed to the kick pattern, or the user can flip the route to sidechain in the GUI. An `set_mixer_send(..., sidechain=True)` op is the fix.
- Evidence: this session

### Mixer-volume dB curve is undocumented; gain staging done in plugin output gain instead
- Status: **fixed and live-verified (2026-09-17, FL 26.1.3.5570, managed session from the E-minor smoke song)** near unity: pad alone through insert 10, master capture, raw 12800 -> 6400 measured -12.44 dB (model -12.58). Deeper it diverges as the docs warn: 12800 -> 3200 measured -21.3 dB (model -25.2, solved exponent about 1.77), so keep the +-3 dB caveat below -20 dB. First attempt on insert 5 showed NO change because an automation channel still targeted that fader (new entry below).
- Seen: 2026-09-14, Parking Lot Moon phase 2
- Call: `fl.ops.get_mixer_volume` (all inserts 12800), `set_mixer_volume` docs "no dB conversion is defined"
- Expected / actual: a documented raw -> dB mapping (FL mixer: 12800/16000 = 0 dB, max +5.6 dB, curve unknown; Ember Tides 6800 -> 12800 measured +11.8 dB, which fits neither a linear nor the +5.6 dB power curve); actual none, and renders are forbidden in this phase.
- Workaround: leave every insert at 12800 (0 dB) and trim with dB-exact plugin gains verified by display: Pro-Q 4 "Output Level" (index 556, 0.45 -> -3.60 dB, so dB = 72 v - 36) and Parametric EQ 2 "Main level" (index 35, dB = 36 v - 18). Send levels use the 0.8 = unity scale.
- Evidence: this session

### Unlicensed Super VHS instance hung the next embedded request (60 s timeout), not the MCP
- Status: docs (deployed 2026-09-14 from stage deploy-20260914-h, backup-installed-20260914-174218; troubleshooting.md section (symptom, cause, remedy); `InProcBridge.RawAsync` now runs a best-effort `UiThreadProbe.Describe()` after a timeout so the `TimeoutException` reads "FL's UI thread is not processing messages; visible FL windows: 'Sign in - Super VHS' ..."; whether Fl-MCP's outer "FL operation timed out or was cancelled" text forwards the inner message needs the live check)
- Seen: 2026-09-14, Parking Lot Moon phase 2
- Call: `fl.mixer[16].effects[4].load("Super VHS")` succeeded and its 9 parameters read (Heat/Wash/Drift/Magic/Mix/Output/Static/Shape/Bypass, all defaults); the NEXT request (`fl.mixer[16].effects[0].load("Fruity Parametric EQ 2")` ...) hung at its first native call and timed out ("FL operation timed out or was cancelled"), leaving insert 16 slot 0 empty.
- Expected / actual: parameter writes; actual the plugin had opened its cloud sign-in dialog (unauthenticated instance) and blocked FL's UI thread. After the user signed in and reloaded the plugin, a retry in the same session read and wrote every parameter by display (Heat 15 %, Wash 10 %, Drift 10 %, Static 5 %, Mix 30 %, Output 80 %). Cause: plugin licence state, not the bridge; the bridge only lacks a way to detect/dismiss a modal plugin dialog.
- Workaround: sign in / authorise cloud plugins in the GUI before scripting them; after a timeout, re-inspect `effects.list_text()` for empty slots and redo the request. A "modal dialog open" hint in the timeout error would save a round trip.
- Evidence: this session

### `AutomationPointSpec.tension` direction is undocumented (positive = fast start, slow finish)
- Status: docs (deployed 2026-09-14 from stage deploy-20260914-h, backup-installed-20260914-174218; `AutomationPointSpec` docstring states the sign convention with the live numbers (tension belongs to the segment ENDING at the point; positive = fast start / slow finish; negative = slow start accelerating into the point); constants `fruitylink.TENSION_EASE_OUT = 0.5` and `TENSION_EASE_IN = -0.5`; `set_point`, `set_points`, `pump` docstrings and the api.md conventions table updated)
- Seen: 2026-09-14, Parking Lot Moon phase 3
- Call: `fl.automation[21].set_points([... P(96, 0.662), P(127.98, 0.809, 0.3) ...])` (pad Pro-Q 4 High Cut opening across bars 25-33), then `seek` to bar 29 and read the display
- Expected / actual: docs say only "tension -1..1"; a slow opening that accelerates into the downbeat was wanted. Actual: tension +0.3 on the segment's end point gave 5698 Hz at the half-way bar (89 % of the travel at 50 % of the time), i.e. positive tension bends the segment fast-early; -0.3 gave 2282 Hz at bar 29 and 3004 Hz at bar 31 (slow-early, accelerating). `pump()` uses +0.5 on its recovery segment, which therefore recovers fast then eases.
- Workaround: negative tension for swells that accelerate into a downbeat; positive for compressor-like recoveries. The docstring should state the sign convention and that the tension belongs to the segment ending at that point.
- Evidence: this session (bar-29 display 5698 Hz vs 2282 Hz for +0.3 / -0.3)

### No automation inventory: `fl.automation` has no list/describe, event ids only via `get_channel_plugin`
- Status: fixed (live 2026-09-14: check 5 -- `describe()` printed 17 clips with decoded targets (insert 4 slot 0 parameter 48, `mixer_volume` 24 and 6) and placements; `list()[0].target` is an `AutomationTarget`; deployed 2026-09-14 from stage deploy-20260914-h, backup-installed-20260914-174218; `fl.automation.list(with_points=True) -> tuple[AutomationChannelInfo, ...]` (channel, name, `targets_text`, `event_ids`, decoded `targets`, `.target`, `point_count`, `clips`) and `fl.automation.describe() -> str` (one line per clip), built on `query_channels` + `get_channel_plugin` (parses ": automation clip -> ...", decodes `event 0x...` via `from_event_id`) + `query_automation_points` + one `query_clips` pass; `parse_automation_link` exported from `fruitylink.automation`; unlinked clips ("no generator") are not listed and placements match by `source_index == channel index`)
- Seen: 2026-09-14, Parking Lot Moon phase 3
- Call: `dir(fl.automation)` -> only `create`, `pump`, `__getitem__`; `fl.ops.query_channels()` marks automation channels like any other channel
- Expected / actual: a `describe()`/`list()` that returns every automation channel with its target, event id, point count and clip placements; actual the inventory had to be built by filtering `query_channels()` on the "PLM - " name prefix, calling `get_channel_plugin(channel=i)` (prints "automation clip -> event 0x71008030") and `automation[i].list()` per channel, and matching `query_clips()` rows with `source_kind == "channel"`.
- Workaround: the loop above; `AutomationTarget.from_event_id` decodes the printed id back to a target (checked: 0x71008030 = insert 4 slot 0 param 48, 0x76001fc0 = insert 24 volume, 0x70001fc0 = Master volume).
- Evidence: this session; `records/phase-3-state-v008.json`

### Automation clips cannot loop or offset: one 1-bar tile cannot follow the alternating kick pattern
- Status: fixed (live 2026-09-14: check 29 -- `fl.automation.duck` over bars 9-25 built 99 points for 33 kick onsets (3 per hit, first hit at the clip start) with the 0.69/0.80 hold and tension 0.5 recovery; deployed 2026-09-14 from stage deploy-20260914-h, backup-installed-20260914-174218; helpers: `fl.playlist.onsets(channel, start_tick, end_tick)` reads a channel's note-ons through the playlist clips (same rules as `gaps`); `fl.automation.duck(target, hits_ticks, start_tick, length_tick, *, track, depth, recovery_beats=0.25, floor, ceiling, tension, name)` writes ONE envelope with a dip at every absolute hit; `fl.automation.tile(target, shape, start_tick, length_tick, *, track, period_beats, offsets_beats=(0,), name)` repeats a shape with per-repeat offsets; pure `duck_points`/`tile_points` (`pump_points` is now `duck_points` on a regular grid); `add_clip` docstring states the no-loop/no-offset fact)
- Seen: 2026-09-14, Parking Lot Moon phase 3 (bass sidechain emulation on `AutomationTarget.mixer_volume(6)`)
- Call: `AutomationCurve.add_clip(track, start_tick, length_tick)` re-places the same channel (curve restarts at each placement, no clip offset/loop flag); the kick pattern is a 2-bar cycle (odd bars 1 + 2.5, even bars 1 + 3; choruses 1 + 3 / 1 + 2.5 + 3; pre-chorus 1 only)
- Expected / actual: a tiled 1-bar duck matching every kick; actual impossible with one envelope, and 80 separate placements of a 2-bar tile would still miss the chorus/pre variants.
- Workaround: read the kick notes of patterns 29-33 through the playlist clips on track 7 (162 hits in verses/pre/choruses only), and write ONE full-song clip with a computed 488-point list (hold 0.8 one tick before each hit, 0.69 at the hit, back to 0.8 after 24 ticks = 150 ms with tension 0.5). `pump()` itself is only for regular grids (`beats_per_hit`).
- Evidence: this session (`PLM - Bass duck (insert 6)`, channel 36, event 0x71801fc0)

### No `AutomationTarget` for mixer send levels; automated the send insert's own volume instead
- Status: wontfix (the bridge has no send-level event id: `SetMixerSendAsync` pokes the send table directly, no `0x1FCx`/send control constant exists in the SDK, native bridge or Fl-MCP notes, and creating a link on a guessed id could crash FL; documented in the `AutomationTarget` docstring, the api.md conventions table and the automation section; workaround: automate the return insert's `mixer_volume` (all sources together, base the curve on its current level) or per source the send effect's wet parameter via `AutomationTarget.effect_parameter(track, slot, index)`; live probe for a later `mixer_send(src, dst)` target: create a send-level automation clip by hand in FL and read its `event 0x...` in `fl.automation.describe()`)
- Seen: 2026-09-14, Parking Lot Moon phase 3 (delay-send pulses at phrase ends)
- Call: `AutomationTarget` kinds are channel volume/pan/pitch, mixer volume/pan, plugin_parameter; `set_mixer_send(src, dst, level)` has no automation counterpart
- Expected / actual: automate Lead->24 and Vox->24 send levels; actual no such target, so the pulse is on `mixer_volume(24)` ("Delay Send" return insert), which moves all sources together and lowers the phase-2 baseline (0.66 in verses = below the 0.8 unity that phase 2 assumed; 0.8 at phrase ends and through the interlude, 0.72 in the outro).
- Workaround: the return-insert volume; alternatively the per-insert Delay 3 "Output wet" (index 23) on inserts 1/4/5.
- Evidence: this session (`PLM - Delay send level (insert 24)`, event 0x76001fc0; readback 10560 at bar 20, 12800 at bars 16 and 52)

### Fruity Reeverb 2 parameter scales are undocumented; first writes landed far off
- Status: fixed (live 2026-09-14: check 23 -- `scale_for("Fruity Reeverb 2", "Low cut").to_normalized(300)` displayed "302Hz" against the 300.0 Hz prediction, `verified` true; deployed 2026-09-14 from stage deploy-20260914-h, backup-installed-20260914-174218; `python/src/fruitylink/data/parameter-scales.json` + `fruitylink.scales`, re-exported from `fruitylink.plugins`: `scale_for(plugin, parameter)`, `scales_for(plugin)`, `known_plugins()`, `add_scale()`, `ParameterScale.to_display/to_normalized/describe` (kinds linear, exp, power, log_db, table, enum; every entry carries confidence + evidence); seeded with the Reeverb 2 and Delay 3 measurements from this log; api.md "Known parameter scales")
- Seen: 2026-09-14, Parking Lot Moon phase 3 (new Reeverb 2 on the Master, wet 3 %)
- Call: `set_plugin_param(channel_or_track=0, slot=0, param_index=i, value=v)` with guessed v (Low cut 0.25 -> 764 Hz, High cut 0.6 -> 13.5 kHz, Predelay 0.2 -> 200 ms, Decay 0.35 -> 7.1 s, Dry 1.0 -> 125 %)
- Expected / actual: a documented v -> display mapping; actual none, three calibration writes per knob (display lags within one request, so each read needed a 0.25 s pause).
- Workaround: measured: Low cut 0.05 = 168 Hz, 0.075 = 243, 0.1 = 317, 0.15 = 466; High cut and High damping 0.2 = 4.8 kHz, 0.21 = 5.0, 0.3 = 7.0, 0.4 = 9.1; Predelay ms = 1000 v (0.02 = 20 ms); Decay 0.1 = 2.1 s, 0.12 = 2.5, 0.15 = 3.1, 0.2 = 4.1; Dry, ER and Wet levels = 125 v %. Room size = 100 v.
- Evidence: this session

### `query_plugin_parameters` `raw_value` is a float bit pattern, not the normalized value
- Status: fixed (live 2026-09-14: check 13 -- raw 1062303685 (0x3F5178C5) decoded to `normalized` 0.8182337880134583 with no `struct` work, display "7000.0 Hz"; deployed 2026-09-14 from stage deploy-20260914-h, backup-installed-20260914-174218; `PluginParameterInfo.normalized` decoded in `__post_init__` via `normalized_from_raw` (None for native integer scales), serialised as `normalized` next to `rawValue`; `decode_record` tolerates the field missing from an older host)
- Seen: 2026-09-14, Parking Lot Moon phase 3
- Call: `fl.ops.query_plugin_parameters(channel_or_track=4, slot=0, offset=48, limit=1).items[0].raw_value`
- Expected / actual: the 0..1 value written by the automation clip; actual 1059075707 (= 0x3F203A7B, the IEEE-754 bits of 0.626) with no `normalized_value` field, so the display string is the only readable value and numeric verification needs `struct.unpack("f", struct.pack("I", raw))`.
- Workaround: decode the bits or compare displays.
- Evidence: this session

### Transport `seek` readback drifts 14-20 ticks; bar-level automation values read fine, 150 ms ducks do not
- Status: fixed (live 2026-09-14: check 14 -- `seek_settled(3072)` returned `positionTick 3072, settled true, reads 2`: zero drift; deployed 2026-09-14 from stage deploy-20260914-h, backup-installed-20260914-174218; client-side settle, no bridge change (the bridge has no "automation pass done" signal): `fl.transport.position_tick` (parsed from `get_song_state`), `fl.transport.seek_settled(tick, attempts=20, delay=0.05) -> SeekResult(requested_tick, position_tick, settled, reads)` polls until two consecutive reads agree, `seek_ticks(tick, settle=True)` / `seek_beats(..., settle=True)` return the same, `SeekResult` exported; 24-tick ducks are still best verified from the point list)
- Seen: 2026-09-14, Parking Lot Moon phase 3 (verifying clips by seeking and reading plugin displays / mixer volumes)
- Call: `fl.ops.seek(tick=3072)`; `time.sleep(0.12)`; `fl.transport.state_text()`; `fl.ops.get_mixer_volume(track=6)`
- Expected / actual: position 3072 and the duck's dip value (0.69 -> 11040); actual `pos=bar 9 beat 1 (tick 3086)` with playing=no, every seek landing 14-20 ticks late (about the wait time at 100 BPM), so the 24-tick dip is never sampled (12784-12800). Bar-scale curves verify well this way (Pad LP 1500/1800/7000/2500/8000/1000 Hz at bars 1/20/33/52/88/107, drum LP 2989 Hz at tick 12254 vs 20000 Hz at 12294, lead shelf -5.00/0.00 dB, mixer volumes 10560/12800/0).
- Workaround: verify short envelopes from the point list; seek for bar-level checks only. A stopped seek should not advance.
- Evidence: this session

### The named SDK venv has no numpy/soundfile; the `fruitylink.analysis` helpers are pure Python and slow on a full song
- Status: fixed (live 2026-09-14: checks 32 and 43 -- numpy confirmed in the embedded runtime by the orchestrator, and `scan_bars(bpm=100)` over the 259.2 s full render took 33.97 s, seconds not minutes; note the dev venv `sdk\python\.venv` still reports `_kernels.USE_NUMPY False` until `uv sync`; deployed 2026-09-14 from stage deploy-20260914-h, backup-installed-20260914-174218; root cause: the embedded CPython 3.14.6 runs with `site_import` off and an explicit module search path and the extension mechanism accepted only one wheel per `python/extensions/<name>/`; `EmbeddedPythonRuntimeLocator.DiscoverExtensionPackages` now also accepts an unpacked `<extension>/site-packages`, the deploy ships `python/extensions/analysis-support` with numpy 2.5.1 (installer `stage-analysis-support.ps1`, `python-analysis.json` pin, `package.ps1 -NumpyWheelCacheDirectory`, `--without-analysis-support`), `pyproject.toml` gains the `analysis` extra (`uv sync` or `pip install -e sdk\python[analysis]` for the dev venv); `soundfile` is not needed (the WAV reader is pure); `fruitylink.analysis` takes the numpy fast path when `_kernels.USE_NUMPY` is true, the pure path stays the default)
- Seen: 2026-09-14, Parking Lot Moon phase 4 (verifying the v009 full render, 259 s stereo float WAV)
- Call: `sdk\python\.venv\Scripts\python.exe -c "import numpy, soundfile"` -> `ModuleNotFoundError`; `fruitylink.analysis.loudness` imports only `array`/`math`
- Expected / actual: the phase brief promised numpy/soundfile in that venv; actual it only has the SDK, and `load_wav` returns an `AudioSource` without `summary()` (the docs' `audio.summary(loudness=True, true_peak=True)` is on `fl.analysis.wav(...)`, the `AudioAnalysis` wrapper). `scan_bars` + `describe_sections` on the full 259 s render took about 2.5 min of pure Python.
- Workaround: system Python 3.12 (numpy 2.5.1, soundfile) with `PYTHONPATH=sdk\python\src` for the SDK helpers; whole-file integrated LUFS / true peak from `ffmpeg -af ebur128=peak=true`; own numpy FFT band envelopes for the duck check. A `pip install -e .[analysis]` extra (numpy, soundfile) and a numpy fast path in `scan_bars` would remove this.
- Evidence: this session; `Parking-Lot-Moon/records/verification-v009.json`

### GMS authored by parameter renders silence; its displays are raw fractions; a `.gmsynth` preset loads through `load_channel_plugin_state`
- Status: docs (deployed 2026-09-14 from stage deploy-20260914-h, backup-installed-20260914-174218; cause as recorded (GMS oscillators are GUI-chosen single-cycle "Synth Waves", not parameters; not re-verifiable offline); `Channel.load_preset(path)` / `Channels.load_preset_file(index, path)` added and the `load_channel_plugin_state` docstring now lists which formats load (Serum 2 .vstpreset, GMS .gmsynth, FL .fst; .SerumPreset ignored); recipe "Start a native synth from a factory preset" in examples.md)
- Seen: 2026-09-14, Parking Lot Moon phase 4 (first full render v009: intro bars 1-6 at -50 dBFS, pad harmonics at -70 dB; verse/chorus pad missing entirely)
- Call: phase 1 `set_plugin_param` on GMS channel 4 (osc mixes, unison 4, detune, amp ADSR, filter cutoff 0.60); phase 4 `fl.channels[4].parameters.page()` -> "CHN: Amp Attack = 0.50 %", "Filter Cutoff = 0.60 %" (no units, no oscillator waveform parameter at all); `fl.ops.load_channel_plugin_state(channel=4, path=r"...\GMS\Pads & Textures\Smooth & Warm TE.gmsynth")`
- Expected / actual: a pad; actual the parameter-authored GMS instance is inaudible (bars 1-5 150-400 Hz at -58 dB; shortening the attack to 0.10 changed nothing, so it is not an envelope problem: GMS oscillators are single-cycle "Synth Waves" .wav files chosen in the GUI, which the parameter list never exposes, and the fresh instance evidently has none loaded). The `.gmsynth` preset loaded in place through the dispatcher route (verification line: same instance, 226 differing bytes = 4.5 %, unison 10, cutoff 0.42, amp level +4.5 dB) and the pad became audible (150-400 Hz -58 -> -40 dB in the same intro render). The op docstring says proprietary preset files are "silently ignored" (true for .SerumPreset); for GMS it works.
- Workaround: load a factory `.gmsynth` first, then trim by parameter; verify a new synth voice with an isolated section render before building a mix on it. Docs should list which native plugins accept their own preset format through opcode 0x12 and warn that GMS/3x Osc waveform choices are not parameters.
- Evidence: `Parking-Lot-Moon/scratch/v010-intro-attack010.wav` vs `v010a-intro-gmspreset.wav`; `records/phase-4-master.md`

### `set_channel_volume` takes `value=`, `set_mixer_volume` / `fl_mixer_set` take `volume=`
- Status: fixed (live 2026-09-14: check 16 -- `set_channel_volume(volume=)`, `set_mixer_volume(volume=)` and the raw `invoke` alias were all accepted with no TypeError, reading back 9000 / 12800; deployed 2026-09-14 from stage deploy-20260914-h, backup-installed-20260914-174218; canonical keyword stays `value=`; the generator emits `ARGUMENT_ALIASES` so `set_channel_volume`, `set_mixer_volume`, `set_master_volume` accept `volume=` and `set_channel_pan`, `set_mixer_pan` accept `pan=` (`values.resolve_alias` names the canonical keyword when none/both are given); the host accepts the same aliases on the wire (`OperationRegistry.ArgumentAliases`) so an old wheel or raw `invoke` works too; Fl-MCP `fl_mixer_set` description still says 0..12800 and should say 0..16000)
- Seen: 2026-09-14, Parking Lot Moon phase 4
- Call: `fl.ops.set_channel_volume(channel=15, volume=8000)`
- Expected / actual: same keyword as the mixer op; actual `TypeError: ... unexpected keyword argument 'volume'. Did you mean 'value'?` (one wasted request; `fl_python_api` shows the name but the two ops are inconsistent).
- Workaround: `value=`; query_channels().volume reads it back (raw 0..12800).
- Evidence: this session

### `fl_project_start` reports the template tempo (140) before the resumed project settles
- Status: fixed (live 2026-09-14: check 3 -- every resume reported `tempo 100` with `settle {stable true, 2015-2270 ms, 4-5 polls, firstTempo 100}` and no `ProjectUnsettled` warning; the fresh template start reported its own real 140; deployed 2026-09-14 from stage deploy-20260914-h, backup-installed-20260914-174218; `ManagedSession.LaunchAsync` calls `AwaitSettledAsync` after readiness: polls `status` every 250 ms until tempo, PPQ, title and path are unchanged for 2 s (20 s cap, bounded by the launch deadline); the start result carries `settle {{stable, milliseconds, polls, firstTempo}}` and a `ProjectUnsettled` warning with the last observed values when the wait runs out; `fl_status` shows `settle: null`; every start takes about 2 s longer)
- Seen: 2026-09-14, Parking Lot Moon phase 4 (`fl_project_start(projectPath=v011, sourceProjectPath=<render snapshot v010>)`)
- Call: the start result said `"tempo":140` for a 100 BPM project; `fl_status` and `fl.ops.get_tempo()` a few seconds later both said 100, and the following full render was 259.2 s (correct for 108 bars at 100 BPM).
- Expected / actual: the readiness result should carry the loaded project's tempo (it is documented as "Returns readiness, tempo and PPQ"); actual it can carry the template default (140) if FL has not finished applying the loaded project when the probe runs. Every other resume this session reported 100, so it is a race, not a corruption.
- Workaround: re-read with `fl_status` before using the tempo from the start result.
- Evidence: this session (v011 start result vs fl_status)

### Channel volume raw scale is a power curve: 5000 is about -18 dB, 3200 about -29 dB; quiet texture samples vanished
- Status: **fixed and live-verified (2026-09-17, FL 26.1.3.5570, managed session from the E-minor smoke song)**: pad channel raw 12800 -> 6400 measured -12.97 dB on a master capture against the model's -12.58 dB.
- Seen: 2026-09-14, Parking Lot Moon phase 4 (textures and the vocal loop inaudible in v009/v010; isolated stem render `scratch/v011-stem-texvox-49-56.wav`)
- Call: phase 1 `set_channel_volume` 3200/2800/3600 (textures), 5000 (vox), 10000 (kick); `query_channels().volume` reads them back but no dB is documented for channels either
- Expected / actual: phase 1 treated 3200/12800 as roughly -12 dB; actual the mixer-style curve (0.8 = 0 dB, 1.0 = +5.6 dB, exponent about 2.9) gives 3200 -> about -29 dB and 5000 -> about -18 dB, and the Splice texture files are themselves very quiet (`Tex_TapeHiss.wav` RMS -53.6 dBFS, `Tex_Roomtone_Suburb-8bars.wav` -43.6, crackle -35.4; measured with ffmpeg astats), so the texture bed sat near -80 dBFS and the vocal loop at -43 dBFS RMS with everything else muted. Nothing in the SDK reports a sample's level at `add_sample_channel` time even though `fl.analysis.wav` could.
- Workaround: measure sample files before choosing channel volumes; use plugin gains for large boosts (PEQ 2 main level up to +18 dB, Pro-Q 4 output up to +36 dB) and keep channel volumes near 10000. Docstring for `set_channel_volume` should state the curve (or expose dB), and `add_sample_channel` could return peak/RMS.
- Evidence: this session; `records/phase-4-master.md`

### Full render died mid-way (FL render exit code 250477278), left a 94 s partial WAV with a drifting timeline; retry from the preserved snapshot succeeded
- Status: fixed (live 2026-09-14: check 42 -- the full render returned `seconds 259.2` against `expectedSeconds 259.2`, one attempt with `outcome ok` in 105.6 s, no warnings; the retry path itself cannot be provoked deliberately; deployed 2026-09-14 from stage deploy-20260914-h, backup-installed-20260914-174218; new `ManagedSession.Render.cs`: before the editor stops the server reads the new plugin op `song` (`SongExtent`: later of last clip end and last marker, seconds at constant tempo; `expectedSeconds: null` on an older plugin), classifies each attempt (nonzero exit, invalid WAV or shorter than 90 % of the span = failed), moves the partial to `<name>.failed-attempt<n>.wav` (never deleted) and renders the same snapshot once more with a fresh dialog monitor; the result gains `seconds`, `expectedSeconds`, `attempts[]` and `RenderRetried` / `RenderShorterThanExpected` warnings; a second failure throws one IOException naming the snapshot, both attempts and the last 8 lines of today's `plugin-host-*.log`; deadline expiry and unanswerable dialogs are not retried but their partial is moved aside)
- Seen: 2026-09-14, Parking Lot Moon phase 4 (`fl_project_render(outputPath="Parking-Lot-Moon/Parking-Lot-Moon-v011.wav")`, first attempt)
- Call: same call that succeeded for v009 and v010 (108 bars, 259.2 s, 25 inserts of FabFilter/iZotope/Baby Audio/stock effects)
- Expected / actual: WAV or a clean error; actual "Render failed. Snapshot preserved at ...\5bce15ae...\Parking-Lot-Moon-v011.flp. FL render exited with code 250477278" after about 40 s, the session gone, and a 36 MB `Parking-Lot-Moon-v011.wav` (94.3 s) left at the output path. Its first 16 bars match the previous render but from about bar 16 the content runs ahead (chorus material at bar 24, 7 dB hotter) as if the renderer changed tempo or dropped buffers before dying. Because the partial file occupies the output path, an immediate retry would be refused ("must not exist"). `fl_project_start` from the preserved snapshot and the same render call then produced the full 99.5 MB file; the start result again showed `tempo 140` before `fl_status` read 100.
- Workaround: move the partial file aside (never delete evidence), resume from the preserved snapshot, render again. The tool should delete or rename a partial output on failure (or at least say it is there) and include FL's stderr / the last log lines in the error.
- Evidence: `Parking-Lot-Moon/scratch/v011-render-crash-partial.wav`; snapshots `5bce15ae2ca84133a2aa37cf6c223892` (failed) and `64d771eb4a9a48baa1ca1e90a9fe6ab6` (retry)

### Plugin shadow copies are never pruned: `%LocalAppData%\FruityLink\plugin-shadow` reached 81 GB
- Status: verified (deployed 2026-09-14 from stage deploy-20260914-g, backup-installed-20260914-162830; the root was purged by hand (820 entries) before the deploy; live check = a "shadow: pruned N stale plugin copies" line in the plugin-host log after a later FL launch. Source: `ShadowCopyStore` now writes a `.owner` marker (pid + start time) into every copy and sweeps the root on host start, deleting copies whose owner process is gone; marker-less legacy copies are deleted only when none of their files is locked; 3 new tests in `ShadowCopyStoreTests`; needs a host redeploy; batch-2 evidence 2026-09-14 (deploy state verified by DLL comparison, sweep not yet exercised): the installed `FruityLink.Plugins.Host.dll` (sha256 1293c884…, 87,040 B) is byte-identical to deploy-20260914-g and contains the "shadow: pruned" / `.owner` strings while `backup-installed-20260914-162830` (84,480 B) contains neither, `plugin-shadow` is empty (0 entries, mtime 16:25 = the hand purge), no FL launch has happened since the 16:28 deploy (last host log line 16:09:52) so the sweep has never run, the 15:25-16:09 "shadow: could not delete … FruityLink.Ui.Avalonia.dll is denied" lines came from the OLD host's discovery-probe unload (not the sweep), and `PruneStale` logs only when removed > 0 so the "shadow: pruned N" line can only appear on the SECOND launch after the purge; no code changed, redeployed unchanged in stage deploy-20260914-h)
- Seen: 2026-09-14, reported by the user during Parking Lot Moon phase 4
- Call: every `fl_project_start` / render / close cycle (the host shadow-copies `plugins\fl-agent` 281 MB, `fl-python-ide` 129 MB and `fl-mcp` 1 MB per FL launch)
- Expected / actual: copies removed when FL exits; actual 772 GUID folders (after the user had already deleted many) dated 2026-09-12..14, ~410 MB per launch. `ShadowCopyStore` only deleted a copy in `UnloadAlc`/`SafeUnload`, which never runs when FL is closed or killed by the MCP, and the constructor deliberately did not purge the root (multi-host safety), so nothing ever cleaned up.
- Workaround: with no FL process running, delete everything under `plugin-shadow` (they are caches; a live host recreates its own copies).
- Evidence: `ls plugin-shadow | wc -l` = 772; per-entry contents = the plugin folders; `du` of the installed plugin folders 281/129/1 MB

### Master-chain scales had to be probed: Pro-L 2 Gain = 30 v dB, Output Level = 30 v - 30 dBTP; Super VHS Output 80 % is about -8 dB
- Status: fixed (live 2026-09-14: check 23 -- `scale_for("Pro-L 2", "Gain").to_normalized(12.0)` displayed "+12.00 dB", `verified` true; deployed 2026-09-14 from stage deploy-20260914-h, backup-installed-20260914-174218; Pro-L 2 (Gain 30 v dB, Output Level 30 v - 30 dBTP), Pro-Q 4 (band freq/gain/Q/shape/slope and Output Level from the session helper, confidence `inferred`) and Super VHS Output (`rough`, one render-derived point) are in `data/parameter-scales.json` via `scale_for(plugin, parameter)`; Pro-L 2 "param 9/10/19..31" stay unnamed)
- Seen: 2026-09-14, Parking Lot Moon phase 4 (mastering on insert 0)
- Call: `fl.mixer[0].effects[2].parameters.set_verified(0, 0.6)` on FabFilter Pro-L 2 -> "+18.00 dB" (0.5389 -> "+16.17 dB", so Gain is linear 0..+30 dB, not centred at 0.5); `set_verified(18, 0.9)` -> "-3.00 dBTP" (Output Level linear -30..0 dBTP); Super VHS (16/4) index 5 "Output" 80 % -> 100 % raised the whole texture bus by about 8 dB (reverse swell 250-4 kHz -29 -> -15 dBFS together with +6 dB of Pro-Q output), so 80 % is roughly -8 dB, not -2 dB
- Expected / actual: a documented mapping for the FabFilter wrapper parameters (the friction log's "FabFilter parameter map ... still needs the live set-then-read harvest" is still open); actual two probe writes per knob, one of which overshot the swell by 14 dB and cost a full render (v011). Pro-L 2 also exposes its meter settings as unnamed "param 19..31" and true-peak limiting / oversampling as "param 9/10" (On/Off) with no names, so they were left at defaults. Ozone 11 was not tried: its parameters are only reachable through the wrapper's names and, after the earlier unlicensed-plugin hang and today's renderer crash, a 2000-parameter plugin on the master of a chain that cannot be auditioned was judged not worth the risk (skipped, per the brief's "else skip").
- Workaround: calibrations above; Pro-Q 4 as in `scratch/p2helpers.py` (freq v = ln(Hz/10)/ln 3000, gain v = (dB+30)/60, Q v = ln(Q/0.025)/ln 1600, shape index/9, slope index/10, Output Level index 556 dB = 72 v - 36).
- Evidence: this session; `records/phase-4-master.md`

### Deleting a note shrinks every playlist clip of that pattern to the new pattern length
- Status: fixed (live 2026-09-14: check 27 -- after deleting one note both clips still read 3072 (1536 before the fix), `deleted == 1`; deployed 2026-09-14 from stage deploy-20260914-h, backup-installed-20260914-174218; C# root cause: `WriteNoteStructsAsync` (shared by `DeleteNotesAsync` and `EditNotesAsync`) snapshots the length of every clip whose source is the pattern (`PatternClipLengthsAsync`), runs the rebuild, then re-pins any clip at the same slot/start/track whose length changed (`RestoreClipLengthsAsync` -> `ResizeClipsAsync`); an unchanged pattern costs no write; `add_notes` is deliberately untouched (a following clip still grows); safety net `notes.delete(..., preserve_clips=True)` / `notes.edit(..., preserve_clips=True)`; interface summaries and the generated `operations.py` docstrings state the rule)
- Seen: 2026-09-14, Parking Lot Moon phase 5 (v014 revision: chopping the second vocal entry)
- Call: `fl.ops.delete_notes(pattern=38, targets=[{"channel": 18, "key": 60, "startTick": 1536}])` (and the same on pattern 39); the Vox Interlude / Vox Double clips on playlist tracks 10 / 12 were 3072 ticks long
- Expected / actual: only the note goes; actual `fl.clips.list()` afterwards shows both clips resized to 1536 ticks (the remaining note's extent), so the clip end moved from bar 57 to bar 53 without any clip call. Harmless here (the 8.57 s sample plays out inside 4 bars and the new chop clip starts at bar 53), but a clip that relied on the old length would silently lose its tail, and `delete_notes` does not report it.
- Workaround: re-list clips after any note delete on a pattern that is placed in the playlist and `fl.clips.resize` back if needed; the op docs should state that FL re-derives clip lengths from the pattern.
- Evidence: this session (clips 48 / 51 before 3072, after 1536)

### `AutomationTarget.plugin_parameter` takes `(track, parameter, *, slot=)`, not the record's `track/slot/param` order
- Status: fixed (live 2026-09-14: check 6 -- all four construction forms compared equal -> `kind plugin_parameter, index 1, slot 0, parameter 556`; deployed 2026-09-14 from stage deploy-20260914-h, backup-installed-20260914-174218; `plugin_parameter(index, parameter, slot=-1)` still works, `plugin_parameter(track, slot, parameter)` (three positionals = record order) now works, keywords `track=`/`channel=`/`channel_or_track=`, `slot=`, `parameter=`/`param=`, ambiguous mixes raise `TypeError`; new unambiguous `generator_parameter(channel, parameter)`, `effect_parameter(track, slot, parameter)`, `from_record("1/0/556" | (1, 0, 556) | {"track":1,"slot":0,"param":556})`; api.md documents all forms)
- Seen: 2026-09-14, Parking Lot Moon phase 5
- Call: `AutomationTarget.plugin_parameter(1, 0, 556)` written from the phase-3 record's "1/0/49" notation
- Expected / actual: a target for insert 1 slot 0 param 556; actual `TypeError: ... takes 3 positional arguments but 4 were given` (signature is `(channel_or_track, parameter, *, slot=-1)`, slot keyword-only). One wasted request; `fl.transport.seek` and `fl.playlist.tracks` were also guessed wrong in the same pass (the seek is `fl.ops.seek(tick=)` / `fl.transport.seek_ticks`, track names are `fl.ops.set_track_name`), none of which the docs index by task.
- Workaround: `AutomationTarget.plugin_parameter(1, 556, slot=0)`; `inspect.signature` before calling; records should quote the call, not a shorthand.
- Evidence: this session

### Parameter display read immediately after `seek` returns the previous position's automated value
- Status: fixed (live 2026-09-14: check 14 -- `display_at` on the Pro-Q 4 Output Level read "-12.00 dB", "-8.00 dB", "-4.00 dB" at bars 5 / 8.5 / 20, the values automated there; deployed 2026-09-14 from stage deploy-20260914-h, backup-installed-20260914-174218; `fl.transport.read_at(tick, reader, settle=0.3, attempts=6, delay=0.1)` does a settled seek, waits the observed 300 ms, then reads until two consecutive values agree; `parameters.read_at(index, tick)` / `display_at(index, tick)`; a documented wait with injectable defaults, no bridge signal exists)
- Seen: 2026-09-14, Parking Lot Moon phase 5 (verifying the new Pro-Q 4 Output Level automation on insert 1)
- Call: `fl.ops.seek(tick=...)` then `fl.mixer[1].effects[0].parameters.page(offset=556, limit=1)` in the same request, three seeks in a row (bars 5 / 8.5 / 20)
- Expected / actual: -12 / -8 / -4 dB; actual -4 / -4 / -8 dB, i.e. each read shows the value FL had applied for the *previous* seek (the automation is applied asynchronously after the playhead moves). With `time.sleep(0.3)` between the seek and the read every value was right (-12 / -8 / -4 / -4 / -12 at bars 5 / 8.5 / 20 / 40 / 3), and a second read 300 ms later agreed. Related to the phase-3 "seek readback drifts 14-20 ticks" entry but a different failure: the value is from the wrong position, not a nearby one.
- Workaround: sleep about 300 ms after `seek` before reading a display, and read twice; a `seek_and_read` helper (or an op that seeks, waits for the automation pass, then reads) would remove the guesswork.
- Evidence: this session (same request, with and without the sleep)

### A single long heredoc (about 40 lines, Markdown with apostrophes and backticks) fails to parse in the harness Bash tool
- Status: wontfix (harness Bash tool, reproduced again in this batch with a 138-line quoted heredoc; workaround: write multi-line Markdown with the client's Write tool (or a Python script reading a file) and keep Bash heredocs short; two-sentence note added to Fl-MCP `docs/troubleshooting.md` under "Report a reproducible issue")
- Seen: 2026-09-14, Parking Lot Moon phase 5 (writing `records/phase-5-v014.md`)
- Call: `cat > records/phase-5-v014.md <<'EOF' ... EOF` (quoted delimiter, 60 lines of Markdown), once combined with a second python heredoc and once alone
- Expected / actual: the file; actual `/usr/bin/bash: -c: line 35: unexpected EOF while looking for matching ''` both times, although three shorter quoted heredocs with apostrophes appended to this log fine in the same session. Same family as "Multi-heredoc Bash command failed to parse in the harness" (Ember Tides), but it also hits a single heredoc once it is long enough.
- Workaround: write long files with the Write tool (or a Python script reading a file), keep Bash heredocs short.
- Evidence: this session (two failed attempts, Write tool succeeded)

## 2026-09-14 — Post-Parking-Lot-Moon fix batch

Six worktree agents (arrangement, channels-mixer, plugins, server, waveform, live-capture) worked the 37
Parking Lot Moon entries plus two new capabilities; their batches were merged into the SDK, installer and
Fl-MCP main trees, built, and deployed 2026-09-14 17:42 as stage `deploy-20260914-h` (backup
`backup-installed-20260914-174218`), including the rebuilt native FlBridge.dll (harvested
`FLmx_SetTrackArmed` / `MixerTrackArmedOffset`) and the new `python/extensions/analysis-support`
extension carrying numpy 2.5.1. Nothing is live-checked yet: FL has not been launched since the deploy.
The ordered live plan is `batch2-live-checklist.md` in the session scratchpad; batch reports sit beside it.

| Item | Status | New API / where |
| --- | --- | --- |
| `playlist.add_patterns` ignores `length_tick` | fixed (live 2026-09-14, check 25) | `AddPatternClipsAsync` pins the explicit length (`PinClipLengthAsync`); `PatternClipSpec.length_tick` defaults to 0 = follow the pattern; `fl.playlist.add_patterns(specs, enforce_lengths=True)` |
| `fl.clips.resize` takes a sequence of `ClipResize` | fixed (live 2026-09-14, check 26) | `fl.clips.resize(index, length_tick)`, `resize([(index, length_tick), ...])`, `move(index, start_tick, track)`, `delete(7)`, `set_muted(7, True)`; mixed forms raise `TypeError` |
| `AutomationPointSpec.tension` direction | docs (stage h) | docstring states the sign convention; `fruitylink.TENSION_EASE_OUT = 0.5`, `TENSION_EASE_IN = -0.5`; api.md conventions table |
| No automation inventory | fixed (live 2026-09-14, check 5) | `fl.automation.list(with_points=True) -> AutomationChannelInfo`, `fl.automation.describe()`, `parse_automation_link` |
| Automation clips cannot loop/offset | fixed (live 2026-09-14, check 29) | `fl.playlist.onsets(channel, start, end)`, `fl.automation.duck(target, hits_ticks, ...)`, `fl.automation.tile(target, shape, ...)`, pure `duck_points`/`tile_points` |
| No `AutomationTarget` for mixer send levels | wontfix | no send-level event id in any evidence; recipe: automate the return insert's `mixer_volume` or the send effect's wet parameter via `AutomationTarget.effect_parameter(track, slot, index)`; probe snippet in the arrangement report |
| Transport `seek` readback drifts 14-20 ticks | fixed (live 2026-09-14, check 14) | `fl.transport.position_tick`, `seek_settled(tick) -> SeekResult`, `seek_ticks(tick, settle=True)` (client-side settle, no bridge signal) |
| Parameter display after `seek` is stale | fixed (live 2026-09-14, check 14) | `fl.transport.read_at(tick, reader, settle=0.3)`, `parameters.read_at(index, tick)`, `display_at(index, tick)` |
| Deleting a note shrinks the pattern's playlist clips | fixed (live 2026-09-14, check 27) | `WriteNoteStructsAsync` restores clip lengths after the rebuild (`PatternClipLengthsAsync`/`RestoreClipLengthsAsync`); `notes.delete/edit(..., preserve_clips=True)` |
| `AutomationTarget.plugin_parameter` argument order | fixed (live 2026-09-14, check 6) | `plugin_parameter(track, slot, parameter)` accepted; `generator_parameter`, `effect_parameter`, `from_record("1/0/556")` |
| Template mixer has 16 inserts; insert 17 refused | fixed (live 2026-09-14, check 19) | `fl.mixer.insert_count`, `fl.mixer.capacity` (500), `fl.mixer.ensure_inserts(n)`; range error names `ensure_inserts(17)` |
| No channel delete | wontfix | UI-only in FL; `Channel.retire(name=None)` / `fl.channels.retire(index)` mutes, routes to Master, renames "(unused) ..." |
| No send/route readback | fixed (live 2026-09-14, check 18) | `query_mixer_sends(track)` / `FlMixerSendInfo`; `MixerTrack.sends()`, `send_level(dst)`, `fl.mixer.routes()`, `set_mixer_send(..., active=)`, `MixerTrack.disconnect(dst)` |
| Send unity is 0.8, not 1.0 | fixed (live 2026-09-14, check 18) | `fruitylink.SEND_UNITY = 0.8`, `send_level_to_db/from_db`, `send_to(dst, level=1.0, *, db=None, active=True)` (docstring says 1.0 is about +5.6 dB) |
| No Sampler channel-settings ops | fixed-unverified (live PARTIAL 2026-09-14, check 21: ids read/write, snippet unserialisable; fix in progress for stage i), partial | `get/set_channel_control` (REC_Chan bus), `Channel.control(i)`, `set_control(i, v)`, `ChannelControl`, `Channel.stretch_time` (14), `Channel.sample_offset` (13, unverified ids); reverse/fades/trim/stretch mode have no bridge path (offline `wave` recipe in api.md) |
| `set_verified` false when already at the value | fixed-unverified (live FAIL 2026-09-14, check 15: verified=False, unchanged=False, attempts=6; fix in progress for stage i) | `VerifiedWrite.unchanged`; `verified=True, unchanged=True, attempts=1` |
| No sidechain routing | wontfix | flag not in the verified mixer layout (+0x12A4 table unprofiled); `set_mixer_send` docstring + api.md "Verify sends and bus routing"; workaround `fl.automation.pump`/`duck` on `mixer_volume(insert)` |
| Mixer-volume dB curve undocumented | fixed-unverified (live FAIL 2026-09-14, check 45: 12.70 dB measured vs 17.4 dB modelled; fix in progress for stage i) | `fruitylink.levels` (`mixer_volume_to_db/from_db`, `fader_db`, `volume_table`, `FADER_EXPONENT` about 2.889), `MixerTrack.volume_db`, `set_volume(db=)`; bridge clamp raised to 16000; api.md "Volume in decibels" |
| Channel volume power curve | fixed-unverified (live FAIL 2026-09-14, check 45: 12.54 dB measured vs 17.4 dB modelled; fix in progress for stage i) | `channel_volume_to_db/from_db`, `CHANNEL_VOLUME_UNITY=10240`, `Channel.volume_db`, `set_volume(db=)`; `fl.samples.describe(channel)` reports sample levels |
| `value=` vs `volume=` keywords | fixed (live 2026-09-14, check 16) | `ARGUMENT_ALIASES` in `operations.py` (`volume=`, `pan=`), `values.resolve_alias`; host `OperationRegistry.ArgumentAliases`; Fl-MCP `fl_mixer_set` text still says 0..12800 |
| "FL event 254 is truncated" on every channel | fixed (live 2026-09-14, check 7) | `FlpPluginStateReader` tagged/legacy framing (`Framing.Tagged`, `ParseWith`, `ReadVersion`); `ExtractStateAsync` retry; `InvalidDataException` tolerated in evidence reads |
| Sample channels "hosts no generator plugin" | fixed (live 2026-09-14, check 8) | `RequireGeneratorAsync` names automation clips and built-in Sampler channels; used by get_state/load_state |
| `inventory.query_index(text="soft")` misses names | fixed (live 2026-09-14, check 9) | word matching in Python (all words, case-insensitive, name/location/description/comment/author/tags), `limit` after filtering |
| `parameters.page(limit=4240)` refused | fixed (live 2026-09-14, check 10) | `Parameters.all(filter=None, unique=False)`, `plugins.PAGE_LIMIT = 512`, `IndexError` names the cap |
| Serum 2 FX slots / unnamed enums | fixed-unverified (live PARTIAL 2026-09-14, check 11: describe right, result unserialisable; fix in progress for stage i), partial | `fruitylink_serum.describe.explain_parameter`, `explain_parameters(fl, channel, filter=, state=)`, `fx_slot_names(state)`; "FX Main Param n" proxy slots stay opaque (state-based `SerumPatch.fx.*` -> `load_preset` route) |
| "^b^a" prefix on stock effect names | fixed (live 2026-09-14, check 12) | `plugins.clean_parameter_name`; `PluginParameterInfo.name` clean, `raw_name` original; `find`/`set_named` accept either |
| Fruity Delay 3: three "Distortion" | fixed (live 2026-09-14, check 22) | `find`/`set_named` raise `LookupError` on duplicates; `"Distortion [19]"` form; `unique_names(rows)`, `all(unique=True)` |
| Unlicensed Super VHS hung the next request | docs (stage h) | troubleshooting.md section; `InProcBridge.RawAsync` + `UiThreadProbe.Describe()` name the visible FL windows in the `TimeoutException` |
| Reeverb 2 scales undocumented | fixed (live 2026-09-14, check 23) | `data/parameter-scales.json` + `fruitylink.scales` (`scale_for`, `scales_for`, `known_plugins`, `add_scale`, `ParameterScale.to_display/to_normalized`); api.md "Known parameter scales" |
| Master-chain scales (Pro-L 2, Pro-Q 4, Super VHS) | fixed (live 2026-09-14, check 23) | same table: Pro-L 2 Gain / Output Level, Pro-Q 4 bands + Output Level (`inferred`), Super VHS Output (`rough`) |
| `raw_value` is a float bit pattern | fixed (live 2026-09-14, check 13) | `PluginParameterInfo.normalized` (via `normalized_from_raw`), serialised as `normalized`; tolerant `decode_record` |
| GMS by parameter is silent; `.gmsynth` loads | docs (stage h) | `Channel.load_preset(path)`, `Channels.load_preset_file(index, path)`; `load_channel_plugin_state` docstring lists accepted formats; examples.md recipe |
| SDK venv has no numpy; `fruitylink.analysis` slow | fixed (live 2026-09-14, checks 32/43) | unpacked `<extension>/site-packages` extensions (`EmbeddedPythonRuntimeLocator`); `python/extensions/analysis-support` (numpy 2.5.1, `stage-analysis-support.ps1`, `python-analysis.json`); `pyproject` `analysis` extra; `_kernels.USE_NUMPY` |
| `fl_project_start` reports the template tempo | fixed (live 2026-09-14, check 3) | `ManagedSession.AwaitSettledAsync` (250 ms polls, 2 s window, 20 s cap); start result `settle {stable, milliseconds, polls, firstTempo}`, `ProjectUnsettled` warning |
| Full render died mid-way, partial WAV left | fixed (live 2026-09-14, check 42) | `ManagedSession.Render.cs`; plugin op `song` (`SongExtent`); `<name>.failed-attempt<n>.wav`; result `seconds`, `expectedSeconds`, `attempts[]`, `RenderRetried` / `RenderShorterThanExpected`; `Artifacts.ReadWave -> WaveInfo` |
| Plugin shadow copies never pruned (81 GB) | verified (deploy state; sweep unexercised) | no code change; installed host DLL = deploy-g bytes with the `.owner`/"shadow: pruned" strings; the "pruned N" line can only appear on the second launch |
| Long heredoc fails in the harness Bash tool | wontfix (harness) | Fl-MCP troubleshooting.md note: use the Write tool, keep heredocs short |

### New capabilities (not friction fixes)

Audio description layer (waveform batch). `fruitylink.analysis.describe_audio(source, *, bpm=None, ppq=None, start_bar=None, beats_per_bar=4, detail="normal")` returns an `AudioDescription` (`.data`, `.text` of 16-24 lines, `.tags`) covering level, envelope sketch, onsets in `bar:beat`, decay/tail, silence, seven-band spectral segments, tonality with root note, stereo width, loop hints and taste-rule tags (`sub-heavy`, `harsh 2-4 kHz`, `wide AND bright`, ...); `compare_audio(a, b)` gives `b minus a` deltas with verdict phrases and `describe_samples(paths, detail="brief")` is a cached one-line-per-file table. The same calls are static members `fl.analysis.describe/compare/describe_samples`, `AudioAnalysis.describe(...)`, and `fl.samples.describe(channel, *, path=None)` (new `fruitylink/samples.py`, fed by `add_sample`/`replace_sample` because FL exposes no Sampler-file query). Measured on numpy: the Parking Lot Moon master (259 s) describes in 2.69 s (`full` 2.71 s) against 7.02 s / 19.13 s pure Python, a 1.1 s Splice snare in 0.09 s, and a 98-file Splice folder in 11.3 s cold / 0.83 s warm; the master reads -13.8 LUFS, 2.6 s trailing silence, 108.00 bars, tags `dark, wide`. Proposed MCP tool `fl_audio_describe(path?, channel?, detail, bpm, ppq, startBar, compareWith)` returning `.text` (companion `fl_audio_browse`); the one SDK gap found is a native `get_channel_sample_path`.

Live capture design (live-capture batch). Route a (FL disk recording) was chosen over a tap VST3, in-process buffer hooks, WASAPI loopback and Edison: arm inserts, seek, record + play a bar range, stop, read the per-insert WAVs FL writes to `Documents\Image-Line\FL Studio\Audio\Recorded` (all inserts sample-aligned in one engine pass). Implemented offline: `SetMixerTrackArmedAsync`/`GetMixerTrackArmedAsync` over the harvested `FLmx_SetTrackArmed` (setter thunk 0x12c3980 on 26.1.3.5570 / 0x11c59d0 on 2025, `RCX = trackStruct, DL = armed`) and `MixerTrackArmedOffset` (+0x1470 / +0x145c), gated by `OperationAvailability` until both resolve; `MixerTrack.armed`; `fruitylink.capture` as `fl.audio` (`capture(inserts, start_bar, end_bar, tail_beats=)` -> `CaptureResult`, `decide(start_bar, end_bar)` -> `CaptureDecision`, `measure_section(...)` -> `SectionMeasurement` or `RenderRequired`, `measure_wav`, `envelope`); 24 tests against a fake FL; native fixture `testArmTrackSymbols`. Design and harvest evidence are in `docs/live-audio-capture.md`; the `CapturePolicy` break-even (live 8 s overhead, render 45 s + 1/8 real time, 120 s live cap, so about 42 s of music) is reasoned, not measured. Proposed MCP tools: `fl_audio_capture(inserts, startBar, endBar, tailBeats=0, name=null)` and `fl_section_measure(startBar, endBar, inserts=null, prefer="auto", tailBeats=0)` (the render branch is the only place a session closes). Remaining live checks: the symbols in `syms`, an arm/disarm round trip, dialog behaviour (`AutoCreateClip`/`AutoUnarm`, recording filter), then the capture pass.

## 2026-09-14 — Live verification results (Parking Lot Moon batch)

Ran 2026-09-14 17:50-18:25 against disposable copies `lc-001` .. `lc-008` under `FlMcp\Projects\Parking-Lot-Moon\live-checks\`
on FL 26.1.3.5570 with stage `deploy-20260914-h` (deployed 17:42); the v015 master and the final-master files were never
opened or written. The plan was `batch2-live-checklist.md`: 45 numbered checks in order (read-only, then mutating, then the
shadow sweep, then the renders, each of which closes the session), every snippet one `fl_execute_python` body — 28 PASS,
2 FAIL, 3 PARTIAL, 12 SKIPPED. The capture block (34-41) and its cross-check (44) were skipped in this run because the
orchestrator verified the capture route separately (see "Live capture" below); 30 is GUI-attended, 31 needs an unlicensed
cloud plugin, 32 was verified by the orchestrator. Evidence — `lc-001` .. `lc-008`, `live-checks-full.wav` and the five
calibration WAVs — stays under `live-checks/`, snapshots under `Projects\snapshots\`.

| Check | Entry | Result | Evidence |
| --- | --- | --- | --- |
| 3 | `fl_project_start` reports the template tempo | PASS | every resume `tempo 100`, `settle {stable true, 2015-2270 ms, 4-5 polls, firstTempo 100}`, no `ProjectUnsettled`; fresh template start reported its own real 140 |
| 4 | Plugin shadow copies never pruned (first launch) | PASS | root held this host's three `.owner` dirs (pid 58264 = `fl_status.processId`) plus one 1-file probe dir; single non-repeating `could not delete ... FruityLink.Ui.Avalonia.dll` |
| 5 | No automation inventory | PASS | `describe()` = 17 clips with decoded targets (`insert 4 slot 0 parameter 48`, `mixer_volume 24`, `mixer_volume 6` 488 points); `list()[0].target` is an `AutomationTarget` |
| 6 | `AutomationTarget.plugin_parameter` argument order | PASS | all four construction forms equal -> `kind plugin_parameter, index 1, slot 0, parameter 556` |
| 7 | "FL event 254 is truncated" | PASS | `fl.channels[1].get_state()` 17,640 bytes, head `0c00000001000000`; fresh build-4726 template variant 13,079 bytes, no error |
| 8 | Sample channels "hosts no generator plugin" | PASS | ch 8 "it is a built-in Sampler channel (or an audio clip / layer) ... `replace_channel_sample`"; ch 21 "it is an automation clip (automates event 0x71008030)" |
| 9 | `query_index(text=...)` misses single words | PASS | all six queries non-empty; `cotton` -> ["LD - Analog Crispy Cotton", "PD - Analog Soft Cotton"]; `soft` == `Soft` (5 hits) |
| 10 | `parameters.page(limit=4240)` refused | PASS | `count 4240` in one request; message names the 512 host cap and `all()`/`list()`/`iter()` |
| 11 | Serum 2 FX slots / unnamed enums | PARTIAL | describe correct (Sub Shape 199 "Sine" -> `meaning.value` "sine"; A WT Pos frame 1 of `S2 Tables/Analog/DM - OSCAR.wav`; `fx` Delay then Reverb), but the checklist snippet raises `TypeError: JSON object keys must be strings.` |
| 12 | "^b^a" prefix on stock effect names | PASS | `name` "Low cut" / `raw_name` "^b^aLow cut" for six rows; `find("Wet level")` -> 12 |
| 13 | `raw_value` is a float bit pattern | PASS | raw 1062303685 = 0x3F5178C5 -> `normalized` 0.8182337880134583, display "7000.0 Hz", no `struct` decoding |
| 14 | `seek` drift + stale display after `seek` | PASS | `requestedTick 3072 -> positionTick 3072, settled true, reads 2` (zero drift); `display_at` -> "-12.00 dB", "-8.00 dB", "-4.00 dB" at bars 5 / 8.5 / 20 |
| 15 | `set_verified` false when already at the value | FAIL | `{verified false, unchanged false, attempts 6, display "On"}` on a Fruity Delay 3 "Tempo sync" already On (`rawValue 1`, `normalized null`) |
| 16 | `value=` vs `volume=` keywords | PASS | `{chan 9000, mixer 12800}`, no TypeError, raw `invoke` alias accepted too |
| 17 | Volume curves, readback half | PASS | `{chan_raw 10240, mixer_top 16000, mixer_top_db 0.0}` (the clamp no longer pulls 16000 back to 12800) |
| 18 | No send/route readback; send unity is 0.8 | PASS | before/mid/after sends as expected, `disconnect(0)` silent (no dialog, no stall), `routes 49`, `list_text()` "sends:" line agrees with `sends()` |
| 19 | Template mixer inserts; naming past the count | PASS | "Insert 25 does not exist yet ... `ensure_inserts(25)`"; `added 1`, `after 25`, name "Vox"; template variant 16 -> 17 |
| 20 | No channel delete (`retire` fallback) | PASS | `"(unused) (unused template)"`, `muted true`, `mixerTrack 0`, channel still present; the prefix is applied again (not idempotent) |
| 21 | No Sampler channel-settings ops | PARTIAL | id 14 is the stretch-time field (1000 -> 1500 -> 1000, id 13 unmoved, `sample_offset` 0); the checklist snippet raises `TypeError: JSON object keys must be strings.` and the GUI knob was not eyeballed |
| 22 | Fruity Delay 3: three "Distortion" | PASS | `LookupError` lists indices 18, 19, 20 and writes nothing; `"Distortion [19]"` -> idx 19; `all(unique=True)` lists the bracketed names |
| 23 | Reeverb 2 / master-chain parameter scales | PASS | Pro-L 2 Gain "+12.00 dB", Reeverb 2 Low cut "302Hz" against a 300.0 Hz prediction, both `verified` |
| 24 | GMS by parameter silent; `.gmsynth` loads | PASS | ch 56 "same instance; params 205->205; state record changed (4968 -> 4968 bytes, 231 differing bytes = 4.6%)" |
| 25 | `add_patterns` ignores `length_tick` | PASS | `fixed 0`; both new clips read 3072 straight after `add_patterns`, no resize call |
| 26 | `fl.clips.resize` forms | PASS | scalar then sequence form -> `(1536, 3072)` |
| 27 | Deleting a note shrinks the pattern's clips | PASS | `deleted 1`, clip lengths stay 3072 (were 1536 before the fix) |
| 28 | `AutomationPointSpec.tension` direction | PASS | on a clean insert: `ease_out (+0.5) [13168, 15510, 15926]`, `ease_in (-0.5) [74, 490, 2832]` of 16000 — +0.5 is a fast start; the insert-6 run was muddled by the existing bass duck |
| 29 | Automation clips cannot loop/offset | PASS | duck over bars 9-25: 33 kick hits, 99 points (3 per hit), head 0.69 / 0.80 with tension 0.5 |
| 30 | No `AutomationTarget` for mixer send levels | SKIPPED | GUI-attended by design |
| 31 | Unlicensed Super VHS hang | SKIPPED | no unauthenticated cloud plugin available; deliberately provokes a 60 s timeout |
| 32 | SDK venv has no numpy (presence half) | SKIPPED | verified by the orchestrator; the timing half is check 43 |
| 33 | Plugin shadow copies never pruned (sweep) | PASS | every launch from 18:13 logs `shadow: pruned 4 stale plugin copies (410 MB)`; the undeletable probe dir is a different one each launch and is always gone after the next sweep |
| 34 | Arm gate / native symbols | SKIPPED | capture block, skipped by instruction; covered by the orchestrator (symbols resolved) |
| 35 | Arm round trip | SKIPPED | capture block; covered by the orchestrator |
| 36 | Recorded folder + disk-recording settings | SKIPPED | capture block; covered by the orchestrator |
| 37 | Transport record pass | SKIPPED | capture block; covered by the orchestrator |
| 38 | Recorded file names / provenance | SKIPPED | capture block; covered by the orchestrator |
| 39 | Slice alignment | SKIPPED | capture block; covered by the orchestrator |
| 40 | End-to-end `fl.audio.capture` | SKIPPED | capture block; covered by the orchestrator |
| 41 | `fl.audio.decide` / `measure_section` policy | SKIPPED | capture block; covered by the orchestrator |
| 42 | Full render died mid-way | PASS | `seconds 259.2` vs `expectedSeconds 259.2`, one attempt `outcome ok` in 105.6 s, no warnings, 99,533,084 bytes; the retry path cannot be provoked deliberately |
| 43 | numpy timing on the full render | PASS | `scan_bars(bpm=100)` over 259.2 s of audio in 33.97 s (108 bars) — seconds, not minutes |
| 44 | Capture cross-check | SKIPPED | depends on `r.measurements` from check 40 |
| 45 | Volume-curve calibration (renders) | FAIL | measured drops 12.54 dB (channel 12800 -> 6400), 12.70 dB (mixer 12800 -> 6400), 14.21 dB (mixer 12800 -> 5769) against the model's 17.4 / 17.4 / 20.0 dB |
| 46 | Describe / compare / samples (new capability) | PASS | 17 lines in 2.77 s: 108.00 bars, 2.614 s tail, `wide`, structure matches the recorded master; `compare_audio` vs the final master `rms +3.4 dB, loudness +3.3 LU, -[dark]` (this render carries check 23's +12 dB); 21 samples 14.97 s cold / 0.05 s warm |
| 47 | Optional renders | PARTIAL | the GMS "Pad check" chord is clearly audible (-21.8 dBFS RMS at unity, -36.0 dBFS through the -20 dB insert, against the -58 dB fresh-GMS baseline); the bars 9-25 bass-duck render was not done |

### Failures and follow-ups

1. **Check 15 — `Parameters.set_verified` on a slot that already holds the value.** Exact result
   `{"verified": false, "unchanged": false, "attempts": 6, "display": "On"}` for `fl.mixer[4].effects[3].parameters.set_verified("Tempo sync", 1.0)`
   (Fruity Delay 3, already On); the parameter row reads `rawValue: 1, displayValue: "On", normalized: null`. Component at
   fault: SDK Python `python/src/fruitylink/plugins.py` — stock FL effects report the raw value as a plain integer and
   `normalized_from_raw(1)` decodes 1 as a float32 denormal and returns `None`, so `unchanged` never becomes True and
   `_write_applied` is False; the six 50 ms readbacks then all fail. **Fix in progress** (stage i): treat a non-float-bit
   raw value that is unchanged with an unchanged display as "already in place", or decode small integer raws on the native
   scale.
2. **Check 45 — the fader dB curve is wrong by 5-6 dB.** `FADER_EXPONENT = 2.89` in `python/src/fruitylink/levels.py`
   predicts a 17.4 dB drop for halving a channel or mixer volume; the isolated renders measure 12.54 dB (channel
   12800 -> 6400), 12.70 dB (mixer 12800 -> 6400) and 14.21 dB (mixer 12800 -> 5769, model 20.0 dB) — solved exponents
   2.083 / 2.109 / 2.053, i.e. about 12.6 dB per halving and an exponent near 2.1, not 2.89. Component at fault: SDK Python
   `fruitylink.levels` (and everything derived from it: `mixer_volume_to_db/from_db`, `channel_volume_to_db/from_db`,
   `fader_db`, `volume_table`). **Fix in progress** (stage i): re-set the exponent to about 2.05-2.1 and re-derive
   `FADER_MAX_DB` (16000 then reads about +3.9 dB, not the +5.6 dB the FL fader hint shows — the hint and the audio
   disagree and the audio is what the SDK promises); `volume_db` keeps 0 dB at position 0.8 (10240 / 12800) either way.
   Evidence: `live-checks/cal-12800b.wav`, `cal-6400.wav`, `cal-mix-12800.wav`, `cal-mix-6400.wav`.
3. **Checks 11 and 21 (PARTIAL) — the embedded worker rejects `result` dicts with non-string keys.** Exact error:
   `TypeError: JSON object keys must be strings.` raised from `fruitylink/values.py` line 42 (`to_json`, called by
   `worker.py _safe_result`). `to_json` stringifies keys on the very next line, so the guard is stricter than the
   conversion. Components at fault: SDK Python `python/src/fruitylink/values.py`, and
   `extensions/serum-support/src/fruitylink_serum/describe.py`, whose `explain_parameters` returns `meaning.values` keyed by
   floats. Both entries' own fixes verified when the keys were stringified by hand (check 11's describe output is correct;
   check 21's control ids read and write consistently, id 14 = stretch time), so only the return path is broken.
   **Fix in progress** (stage i): let `to_json` stringify int/float keys, and key the Serum enum table by strings.
4. **Check 47 (PARTIAL)** — not a defect: the GMS half passed through the calibration renders; the optional bars 9-25
   bass-duck render was simply not run. No follow-up beyond re-running it if the duck shape is ever questioned.

### Live capture

Verified by the orchestrator in the same window, outside the numbered checks (block 34-41 was skipped in the check run to
avoid two sessions arming the same inserts). The harvested native symbols `FLmx_SetTrackArmed` / `MixerTrackArmedOffset`
resolve in the stage-h bridge, and `fl.audio.capture` records the master and individual inserts end to end. Cross-check:
the live-captured master over bars 33-36 measured **-13.83 dBFS RMS / -1.0 dBFS peak** against the offline master's
**-13.8 LUFS / -1.0 dBTP** — the two routes agree.

FL prerequisites — **update 2026-09-17: none of these has to be set by hand any more**, the SDK sets and restores them itself (see the status line under the entry below):

- Record button, right-click > **Recording filter must include Audio**. Registry
  `HKCU\Software\Image-Line\FL Studio 26\General\FruityLoopsMainForm` value `RecordingFilter2` read 3 here, which is Audio
  **off**, and a pass then writes no files at all.
- Mixer menu > **Disk recording > Auto-create audio clip off** for the pass (`AutoCreateClip` / `AutoUnarm` are not read or
  written by the SDK).

FL quirks found and worked around in the SDK:

- A **solo armed insert records only after a second arm-state change**: arming one insert alone produces no file until
  another insert's arm state is toggled. `Audio._arm_refresh` now arms and disarms one non-requested insert after the
  requested set is armed, verifies both readbacks, and appends an `"Arm-refresh workaround applied: insert N ..."` warning;
  `arm_refresh=False` disables it.
- **Every recording adds a sample channel** to the rack. New clips are deleted after the pass and new channels retired
  (`CaptureResult.deleted_clips` / `retired_channels`), but the channels themselves cannot be removed — see the new entry
  below.

### New friction

### The recording filter's Audio bit and the sticky record button made capture need manual FL setup
- Status: **fixed and fully live-verified (2026-09-17/18) in a user-launched FL 26.1.3.5570**
- Seen: 2026-09-17, FL 26.1.3.5570, background managed project (`fl_project_start(background=true)`, pid 4828)
- Call: `fl_audio_capture(startBar=1, endBar=1)` on a machine whose `RecordingFilter2` is the stock `3`
- Expected / actual: a WAV per armed insert; actual `CaptureError: FL wrote no WAV ... within 15s`, with the old error
  text telling the user to fix the recording filter in FL's UI. Two causes, both now set by the SDK:
  1. **Recording filter, Audio bit.** FL's global filter (record button right-click > Recording filter) is a bitmask
     whose bits are the menu items' own DFM tags: 1 Automation, 2 Notes, **4 Audio**, 8 Clips. Stock value 3 = Audio
     off, and FL then arms, records and writes nothing. It lives only in the running engine (FL reads `RecordingFilter2`
     at startup and writes it at exit), so the registry is useless while FL runs. New SDK operations
     `get_recording_filter` / `set_recording_filter` read the int behind FL's own menu handler and invoke FL's own
     setter on the main thread; `fl.audio.capture` turns Audio on before arming and restores the whole bitmask
     afterwards (`ensure_recording_filter` / `restore_recording_filter`, reported as `CaptureResult.recording_filter`).
  2. **Sticky record button.** `toggle_record` only flips FL's toolbar toggle and FL leaves it engaged after a pass, so
     the *next* capture switched recording off and wrote nothing: back-to-back captures alternated between working and
     silently failing. New operation `get_record_pressed` (the byte FL's own `ui.isRecording` reads) lets the pass
     engage the button only when it is not already engaged and release it again afterwards.
- Workaround: none needed now; `ensure_recording_filter=False` reproduces the old behaviour.
- Live evidence (2026-09-17): filter read `3` in the running engine (matching the registry); identical starting state,
  `ensure_recording_filter=False` -> no WAV, default -> `recfilter-live_..._Master.wav` written and the filter restored
  to `3`; two back-to-back passes both wrote a WAV. The captures were **silent**, because the background instance
  cannot open FL Studio ASIO while the user's FL holds it (master peak read exactly 0.0) — an environment limit, not a
  capture one, and the new zero-file error names it.
- Live outcome (user-launched instance, installed rebuilt payload): the recording filter, the record button and
  the fixed readback all check out. `record_pressed` now tracks the button (false -> true -> false), and two
  back-to-back `fl_audio_capture` passes of bar 1 both recorded real audio -- peak **-13.4 dBFS**, four onsets at
  1:1 / 1:2 / 1:3 / 1:4, `recording_filter` before 3 / used 7 / restored 3. Three distinct defects were involved:
  (1) the filter's Audio bit, (2) FL's sticky record button, and (3) `GetRecordPressedAsync` reading the toolbar
  form through ONE deref instead of two (`mov rax,[rip+d]; mov rax,[rax]`), which returned an unrelated byte --
  it read true with the button dark and false with it lit, and is what made the record toggle look like a no-op.
  The dispatch args `(12, 1, 2, 8)` were never at fault and are live-verified.
- Follow-up fixed offline the same day: both passes warned "Playhead readback did not pass tick 840 within 12.1s"
  and wrote 12.2 s files for a 1.7 s span, because the project was in **pattern mode** and FL loops the current
  pattern. The pass now selects song mode for itself (restored afterwards, reported in `warnings`) and treats a
  playhead **wrap** as the end of the pass, so a short song or loop range stops promptly.
- Evidence: `Fl-Automation/sdk/docs/live-audio-capture.md` "FL settings the capture sets for itself";
  `sdk/src/FruityLink.FlStudio/Inject/FlInjectBridge.Recording.cs`; `sdk/native/bridge/sigscan.cpp`
  (`RecordingFilterPtr`, `FLrec_SetRecordingFilter`, `RecordButtonFormPtr`, `RecordButtonOffset`,
  `RecordButtonPressedOffset` — all five resolve by signature on both installed engines)

### A solo armed mixer insert records nothing until a second arm-state change
- Status: **fixed and live-verified (2026-09-17, FL 26.1.3.5570, managed session from the E-minor smoke song)**: every capture today reported `Arm-refresh workaround applied: insert N (...)` and produced one WAV per armed insert, including an 8-insert stem pass.
- Seen: 2026-09-14, live capture run (Parking Lot Moon disposable copy, FL 26.1.3.5570, stage deploy-20260914-h)
- Call: `fl.mixer[5].armed = True` then a record + play pass over a bar range (`fl.audio.capture([5], 33, 40)`)
- Expected / actual: one `<project>_<n>_<track>.wav` per armed insert; actual no file at all for a single armed insert, while the same pass with two or more inserts armed writes every file. FL appears to latch the recording set at the last arm-state change and to exclude the insert being changed, so a lone arm leaves the set empty; the readback still reports `armed True`, so nothing in the SDK could see the problem before the pass timed out with zero files.
- Workaround: change a second insert's arm state (arm then disarm any other insert) after arming the one you want; this is what the SDK now does automatically.
- Evidence: `S\batch2-report-live-capture.md` "Live findings and fixes" item 1; `docs/live-audio-capture.md` "Prerequisites in FL" and step 6b

### Every disk recording adds a sample channel that cannot be deleted
- Status: open (FL limitation; no channel-delete call exists in the engine or in FL's own scripting API — see "No channel delete". The capture layer deletes the new clips and retires the new channels, reporting them as `CaptureResult.deleted_clips` / `retired_channels` with a warning naming the retired indices; `cleanup=False` skips it)
- Seen: 2026-09-14, live capture run (Parking Lot Moon disposable copy)
- Call: `fl.audio.capture([5, 0], 33, 40, tail_beats=2)`
- Expected / actual: a capture that leaves the project as it found it; actual FL adds one sample channel per recorded insert (plus an audio clip each, which can be deleted). The clips go away, but the channels can only be muted, routed to Master and renamed `"(unused) ..."`, so a project accumulates retired channels at one per recorded insert per pass. The MCP `fl_audio_capture` description still says "the project is not edited" and should be corrected to surface `retired_channels` / `deleted_clips` / `removed_originals`.
- Workaround: `fl.channels.retire(index)` (what the capture layer does), and capture in a disposable copy when the litter matters; budget one retired channel per insert per pass.
- Evidence: `S\batch2-report-live-capture.md` "Live findings and fixes" item 3 and the MCP contract check (b)

### FL re-applies automation-clip values over a saved fader write after save/reopen
- Status: docs (a fader that an automation clip targets cannot be pinned by a plain write across a save/reopen; worth a line in the automation docs next to `AutomationTarget.mixer_volume`)
- Seen: 2026-09-14, Parking Lot Moon live verification (lc-002 write, read back after the check-45 render/resume; surfaced while collecting the check-46 render evidence)
- Call: `fl.mixer[25].volume_db = -20.0` (raw 5769), `fl_project_render(...)`, then `fl_project_start(... sourceProjectPath=<-full.flp>)` and `fl.mixer[25].volume`
- Expected / actual: 5769 on reopen; actual 15926 — the last value of the "tension2 ease_out" clip created by check 28, which targets `mixer_volume(25)`. The write itself succeeds and reads back correctly in the same session; the reopen re-applies the automation clip's value at the playhead. The related render observation: a section render past the song end reports `cut_clips 0, deleted_clips 116` because whole-song automation clips end before the range and are deleted rather than cut, so each automation target's *current* value at save time is what the trimmed render hears. `cal-12800.wav` was discarded for exactly this reason.
- Workaround: write the fader on an insert no automation clip targets (check 45 was redone on a fresh "Cal" insert 26), or flatten/delete the clip first, or set the value through the clip instead of the fader.
- Evidence: `live-checks/cal-12800.wav` (discarded) vs `cal-12800b.wav`; `S\batch2-live-results.md` "Observations (not failures)"

## 2026-09-17 — Installed-wheel verification

### Installed fruitylink-python wheel silently predates the source it claims to be
- Status: **fixed and live-verified (2026-09-17, FL 26.1.3.5570, managed session from the E-minor smoke song)**: after the elevated reinstall `check_installed_wheels.py` reports all three installed wheels match source, and the in-FL imports that failed before (`from_event_id`, `describe_audio`, `bar_start`) all work.
- Seen: 2026-09-17, Serum preset-loading verification (FL 2026, installed FruityLink tree)
- Call: `fruitylink_serum.loading.automation_links(...)` via `load_preset`; `fl_audio_capture`; `fl_project_render(startBar=, endBar=)`
- Expected / actual: the installed `fruitylink_python-0.2.0-py3-none-any.whl` should be the 0.2.0 in `sdk/python/src/fruitylink`; actual both installed copies (`FruityLink/python/` and `FruityLink/tools/fl-mcp/python/`, both sha256 `8e95bcd6…`) were built before the 2026-09-14 batch — 21 modules differ and 12 are absent (`levels.py`, `samples.py`, `capture.py`, `audition.py`, `scales.py`, `_gaps.py`, `data/parameter-scales.json`, `analysis/{describe,bands,bars,pitch,_kernels}.py`). Symptoms: `AttributeError: type object 'AutomationTarget' has no attribute 'from_event_id'` (swallowed into a `load_preset` warning), `ImportError: cannot import name 'describe_audio' from 'fruitylink.analysis'`, `AttributeError: 'Timebase' object has no attribute 'bar_start'`. Serum preset loading itself is healthy (912/912 presets convert; factory, `SerumPatch`, `build_preset` and snapshot round-trip all read back correctly live), and `FruityLink/python/extensions/serum-support/fruitylink_serum.whl` matched source byte for byte.
- Root cause: the version string and file name are identical for every 0.2.0 build, so the wheel imports and reports 0.2.0 whatever it contains, and every gate in the pipeline checked only the name, the METADATA version and a handful of entry paths. The installer's MCP staging also reused an existing `sdk/python/dist/*.whl` instead of rebuilding, and a copy of that stale wheel was checked into the installer payload.
- Workaround: rebuild with `python -m pip wheel . --no-deps -w dist` in `Fl-Automation/sdk/python` (sha256 `1dd5f024…`, 51 files) and reinstall over both copies; `python sdk/scripts/check_installed_wheels.py "<FruityLink tree>"` reports the drift.
- Fix path: build-from-source by default in `Fl-MCP/scripts/package.ps1` (`-UsePrebuiltWheel` opts back in), content verification in all three staging scripts, `fruitylink_serum.loading` degrades to `attribution="unknown"` instead of raising when the host SDK lacks `from_event_id`.
- Evidence: `Fl-Automation/sdk/scripts/check_installed_wheels.py` output against `C:\Program Files\Image-Line\FL Studio 2026\FruityLink` (67 differences) vs `sdk/python/dist` (clean)

## 2026-09-17 — Managed-session samples and native generator presets

### list_samples entries are not accepted by managed sessions
- Status: **fixed and live-verified (2026-09-17, FL 26.1.3.5570, attached untitled project)**: seven `[P]Drums\...` entries passed verbatim to `fl.channels.add_sample` / `replace_sample` on the installed build landed as copies under `%LOCALAPPDATA%\FlMcp\Projects\staged-samples\packs\Drums\...` and played. `fl_sample_add` takes the same route (`CommandDispatcher` calls `ScriptingSessionPolicy.ResolveSample`). The SDK's `list_samples` header now says a managed session copies the entry into its workspace.
- Seen: 2026-09-17, FL 26.1.3.5570, managed project `smoke-eminor`
- Call: `fl.ops.list_samples(filter="Kick")` -> `[P]Drums\Kicks\...`, then `fl.ops.add_sample_channel(sample_path="[P]Drums\Kicks\Kick.wav")` (same for `replace_channel_sample`)
- Expected / actual: `list_samples` ends its own header with "Pass an entry verbatim to native_add_sample_channel", so a `[P]`/`[U]` entry should be accepted where it is emitted. Actual: in a managed session the sample operations are path-gated to the workspace (`src/FlMcp.Plugin/ScriptingSessionPolicy.cs`: "Stage the sample inside the managed workspace") and the workspace root is `%LOCALAPPDATA%\FlMcp\Projects`, so only workspace-relative paths such as `smoke-eminor/samples/Kick.wav` get through. The SDK resolves `[P]`/`[U]` itself (`FlInjectBridge.ResolveSamplePath`), so the entry works in an unmanaged session and fails only behind the MCP policy — the lister and the gate disagree about what a valid reference is.
- Workaround used today: copy the WAVs into the workspace first (`<workspace>\<project>\samples\`, i.e. `%LOCALAPPDATA%\FlMcp\Projects\smoke-eminor\samples\Kick.wav`) and pass the workspace-relative path.
- Fix path: the server should resolve a library entry itself rather than refuse it — copy `[P]`/`[U]` (and full paths under either root) into the workspace and rewrite the argument, so the FLP still only references files inside the workspace. `ScriptingSessionPolicy.ResolveSample` / `TryStageLibrarySample` now do exactly that, staging into `<workspace>\staged-samples\{packs,user}\<relative>` and re-copying when the source is newer; `tests/FlMcp.Tests/ScriptingSessionPolicyTests.cs` covers it. Still open even with that in place: the SDK's `list_samples` header itself says nothing about staging, so its "verbatim" promise is only true for callers that go through this policy — either say "entries are copied into the managed workspace" in the lister's own output or keep the two texts in sync deliberately.
- Evidence: `src/FlMcp.Plugin/ScriptingSessionPolicy.cs` (remarks + `StagedSamplesFolder`); `src/FlMcp.Server/ServerSettings.cs` (workspace root); `Fl-Automation/sdk/src/FruityLink.FlStudio/Inject/FlInjectBridge.Channels.cs` (`ListSamplesAsync` header text, `ResolveSamplePath`)

### Native FL generator .fst presets: refused by the identity guard, then a silent no-op, then muted and renamed
- Status: **fixed and live-verified (2026-09-17, FL 26.1.3.5570)**: on the installed build, plain `fl.channels[n].load_preset(...)` on `Sytrus\X Nucleon\Lead\Sync Lead.fst` and `Harmor\Keyboard\Rhodes.fst` returned "via FL's channel loader (automatic for an FL-native generator .fst ...; restored name 'Sync Lead' -> 'Lead')", state records changed (99% / 47.5%), channels stayed unmuted and routed, and both stems recorded audio on the first capture pass (before the fix both were silent). Related observation from the same pass: per-insert disk recording is PRE-fader (the Chords stem measured the same at -8 dB and -14 dB insert volume); judge balance on the master capture.
- Seen: 2026-09-17, FL 26.1.3.5570, loading factory presets from `C:\Program Files\Image-Line\FL Studio 2026\Data\Patches\Plugin presets\Generators\{Sytrus,Harmor}\...`
- Call: `fl.channels[n].load_preset(path)` -> `load_channel_plugin_state` -> `FlInjectBridge.LoadChannelPluginStateAsync`
- Expected / actual: one call that applies a factory preset to the generator already on the channel. Three separate defects, each hiding the next:
  1. **Identity guard, wrong encoding.** `EnsureStateFileTargetsPlugin` searched the `.fst` for the plugin name as UTF-16 only. FL's own generators store it as a single-byte NUL-terminated string right after the `FLdt` chunk's version tag (`3o3ish 2.fst`: event `0xC9`, length `0x07`, `Sytrus`+NUL at offset 0x20), so EVERY native preset was refused with `Preset '...' does not name the hosted plugin 'Sytrus'`. The guard now searches both encodings.
  2. **Dispatcher route is a silent no-op.** With the guard passed, the default route (wrapper dispatcher opcode 0x12, the one live-verified for Serum's `.vstpreset` and GMS's `.gmsynth`) reported `state record unchanged (1271 bytes ...)` for a Sytrus `.fst` — it reports success and does nothing. `use_channel_loader=True` (FL's channel file loader, vtbl+0x150) DID apply the preset: state record changed 99% for Sytrus and 47.5% for Harmor. The SDK now picks the channel loader automatically for a `.fst` whose hosted plugin is not the "Fruity Wrapper" VST host, and the verification line names the route it used.
  3. **The channel loader rewrites the channel.** It treats the file as a channel to build, so after the load the channel was MUTED and RENAMED to the preset's base name ("Sync Lead", "Rhodes"); the mixer route survived. Every caller had to unmute and rename afterwards. The SDK now snapshots name, mute and mixer route before the load and restores each one that moved, reporting e.g. `via FL's channel loader (automatic ...; restored name 'Sync Lead' -> 'Sytrus', muted -> unmuted)`.
- Workaround (before the fix): pass `use_channel_loader=True` for native generators, then unmute and rename the channel by hand; a wrapped plugin's preset keeps the dispatcher route.
- Evidence: `Fl-Automation/sdk/src/FruityLink.FlStudio/Inject/FlInjectBridge.PluginState.cs` (`RequiresChannelLoader`, `LoadThroughChannelLoaderAsync`, `EnsureStateFileTargetsPlugin`); `sdk/tests/FruityLink.FlStudio.Tests/PluginStateRoutingTests.cs` (15 tests); `sdk/docs/python/api.md` "Plugin preset files"

## 2026-09-17 — Live verification of the fixed-unverified pile

### A capture with `name=` leaves retired channels pointing at the files it deleted, and FL's renderer then hangs forever
- Status: **fixed and live-verified (2026-09-17, installed build, background managed session)**: three named captures on a copy of the E-minor song retired and repointed channels 12-17 at `fruitylink-retired-placeholder.wav` (originals removed), then `fl_project_render(startBar=33, endBar=36, tailBeats=2)` rendered 8.4375 s in 7.4 s; the same sequence on the previous build hung the renderer twice.
- Seen: 2026-09-17, FL 26.1.3.5570, managed session from the E-minor smoke song (stem pass, then a master render of the saved snapshot)
- Call: `fl.audio.capture([...], startBar, endBar, name="stem")` (which deletes FL's auto-named originals once the copies verify), save, then `fl_project_render(...)`
- Expected / actual: a render of the saved snapshot. Actual: `fl_project_render` timed out at 300 s AND at 600 s with no output file, no partial WAV and no dialog — FL's command-line renderer simply never finished. The same snapshot rendered in **7.5 s** after every `(unused)` channel had been repointed at an existing WAV with `replace_channel_sample`, which isolates the cause exactly.
- Root cause: the capture's litter cleanup retires the sample channels FL auto-creates per recording (mute, route to Master, rename `(unused) ...`) but left their sample reference alone, and then, with `name=`, deleted FL's originals (`removed_originals`). Every retired channel was left pointing at a file that no longer exists, and FL's renderer blocks on loading it with no timeout of its own. The same trap existed without `name=`, only deferred: the project referenced files in FL's recorded-audio folder that the user is free to delete.
- Workaround: before saving, point every retired channel at some existing WAV (`fl.channels[i].replace_sample(path)`), or capture with `keep_originals=True` and never tidy the recorded-audio folder, or `cleanup=False` and delete the channels by hand in the GUI.
- Fix path: `fruitylink.capture.write_placeholder(folder)` writes one tiny silent WAV — `fruitylink-retired-placeholder.wav`, 64 frames of 16-bit stereo silence at 48 kHz, a few hundred bytes — into FL's recorded-audio folder, created only when missing and reused by every later pass. `Audio.capture` now runs the litter cleanup BEFORE the copy/delete step and calls `replace_channel_sample(channel, placeholder)` for each channel it retired, also when the originals are kept, so the project never depends on a file the pass wrote. Reported as `CaptureResult.repointed_channels` / `placeholder` (JSON-safe, and in `warnings`); a refused repoint is a warning naming the channel, never a lost capture; `cleanup=False` still touches nothing.
- Evidence: `Fl-Automation/sdk/python/src/fruitylink/capture.py` (`write_placeholder`, `Audio._repoint`, `PLACEHOLDER_NAME`); `sdk/python/tests/test_capture.py` (63 tests; `test_capture_repoints_retired_channels_before_deleting_the_originals` asserts each repoint happened while the original was still on disk, plus `..._creates_the_placeholder_once_and_reuses_it`, `..._repoints_even_when_fl_keeps_its_originals`, `..._warns_when_a_retired_channel_cannot_be_repointed`, `test_capture_without_cleanup_leaves_fl_litter_and_reports_nothing`); `sdk/docs/live-audio-capture.md` step 10b and "Live results"; `src/FlMcp.Server/FlTools.cs` `fl_audio_capture` description

### An automation channel keeps pinning its target after its playlist clip is deleted
- Status: docs (FL limitation: there is no channel delete, so the automation channel outlives its clips; a line now sits in `sdk/docs/python/api.md` next to the automated-fader note)
- Seen: 2026-09-17, FL 26.1.3.5570, managed session from the E-minor smoke song
- Call: `fl.automation.pump(AutomationTarget.mixer_volume(5), ...)` (created automation channel 12 with one clip), `fl.clips.delete(...)` for that clip, then `fl.mixer[5].volume = 6400` and an `fl_audio_capture` of the master
- Expected / actual: with the only clip gone, a plain fader write should hold. Actual: `fl.mixer[5].volume` read back 6400, but the master capture measured the same level as at 12800 — the automation channel was still applying its own value to insert 5's volume. Routing the audio through insert 10 instead gave the expected -12.44 dB, which confirms the pin is the automation channel and not the fader write.
- Root cause / Workaround / Fix path: the link is the automation channel's target, not the clip; deleting or muting clips leaves the channel and its link in place, and FL exposes no channel delete the bridge can call. Retarget the automation channel (or route the audio through a different insert), set the value through the envelope (`set_point` / `set_points`) instead of the fader, or put the write on an insert no automation channel targets. Documented in `sdk/docs/python/api.md` ("Linked automation and complete envelopes"): *deleting the clip is not enough*.
- Evidence: `Fl-Automation/sdk/docs/python/api.md` ("An automated fader cannot be pinned by a plain write, and deleting the clip is not enough"); the related save/reopen case is the 2026-09-14 entry "FL re-applies automation-clip values over a saved fader write after save/reopen"

### Per-insert disk recording is PRE-fader
- Status: docs (FL behaviour, not a defect: FL's per-insert disk recorder taps the insert before its volume stage)
- Seen: 2026-09-17, FL 26.1.3.5570, managed session from the E-minor smoke song (a stem captured at two fader settings)
- Call: `fl_audio_capture(inserts=[8], ...)` with insert 8's volume at -8 dB and then at -14 dB, alongside `fl_audio_capture(inserts="master", ...)`
- Expected / actual: a stem that follows the insert fader. Actual: the insert-8 stem measured the same RMS at both settings, while the master capture followed the faders — so a stem cannot answer a balance question, and a "did the fader take?" check on a stem is meaningless.
- Root cause / Workaround / Fix path: FL's tap is pre-fader by design. Use a stem to judge *"does this part sound, and what does it sound like?"* and the **master capture** (or a render) to judge balance, gain staging and the effect of a fader or of volume automation. Stated in `sdk/docs/live-audio-capture.md` ("Live results") and in the `fl_audio_capture` / `fl_section_measure` tool descriptions.
- Evidence: `Fl-Automation/sdk/docs/live-audio-capture.md` ("Per-insert disk recording is PRE-fader"); `src/FlMcp.Server/FlTools.cs`

### The `fl_execute_python` oversize envelope carried several KB of the blob it was saving
- Status: **fixed and live-verified (2026-09-17, installed build)**: a 200 KB result now returns an envelope whose `head` is 512 bytes and `tail` 256 bytes (was several KB each). Original: fixed (offline-verified; the excerpt sizes are pure string work, so no live check is needed)
- Seen: 2026-09-17, several oversized `fl_execute_python` responses during the live run
- Call: any `fl_execute_python` whose response exceeds `FL_MCP_PYTHON_RESPONSE_LIMIT` (default 64 KiB) -> `{oversized, totalBytes, path, head, tail}`
- Expected / actual: an envelope that says *which* response was saved and where. Actual: `head` was `limitBytes / 4` (16 KiB at the default limit) and `tail` `limitBytes / 8` (8 KiB), so the envelope put roughly 24 KB of the oversized blob straight back into the context it was meant to protect — and each excerpt was mostly one long, unreadable fragment of the same data the saved file already holds.
- Root cause / Workaround / Fix path: the excerpt budgets were fractions of the limit rather than absolute sizes. `PythonResults.MaximumHeadBytes` (512) and `MaximumTailBytes` (256) now cap them (`Math.Min(limitBytes / 4, MaximumHeadBytes)`), so a small `FL_MCP_PYTHON_RESPONSE_LIMIT` still shrinks the excerpts while the default limit no longer inflates them; the workaround until then was to ignore `head`/`tail` and read `path`.
- Evidence: `src/FlMcp.Server/PythonResults.cs`; `tests/FlMcp.Tests/PythonResultTests.cs` (`ExcerptsAreCappedAtAFewHundredBytesWhateverTheLimitIs`, plus the updated ranges in `OversizedResponseIsSavedInFullAndSummarizedWithHeadTailAndPath`)

### Automation channel keeps its target after the clip is deleted; `release()` needs a placed clip
- Status: **fixed and live-verified (2026-09-17, FL 26.1.3.5570, installed build)** for discovery, the write guard AND the automatic clip placement: on a clip-less pump channel `release(ch, 0.4)` returned `placed_clip=True, track=2, start_tick=0, length_tick=1344`, the next `release(ch, 0.8)` placed nothing, and master captures measured -32.38 -> -19.46 dBFS (12.92 dB rise, model 12.58)
- Seen: 2026-09-17, FL 26.1.3.5570 (follow-up to "An automation channel keeps pinning its target after its playlist clip is deleted" above)
- Call: `fl.automation.links_to(AutomationTarget.mixer_volume(1))`, `fl.mixer[1].volume = 6400`, `fl.mixer[1].set_volume(6400, linked="raise")`, `parameters.set_verified(...)`, then `fl.automation.release(channel, 0.8)` / `release(channel, 0.4)` and `fl.automation[2].add_clip(track=2, start_tick=0, length_tick=2 * BAR)`
- Expected / actual: live-verified as designed — `links_to` found the owning channel after its clip was deleted, the bare `volume =` write raised `AutomationLinkedWarning` naming the channel, `linked="raise"` refused before writing, and `set_verified` reported the link in `automation_linked`. One gap: **with no clip in the playlist FL never evaluates the flattened curve.** `release(0.8)` then `release(0.4)` produced identical master-capture levels (-19.68 / -19.46 dBFS) because FL keeps reapplying whatever value the link last held. After one clip was placed for that curve, `release(0.4)` measured **-32.18 dBFS** and `release(0.8)` **-19.60 dBFS** — a 12.58 dB rise, exactly the SDK's fader model. Also: the mixer readback stayed 12800 the whole time, so **no readback ever reflects the automation-applied value**; only `links_to` plus a capture or render can see this.
- Workaround: place (or keep) one clip for the automation channel before releasing it.
- Fix path: `AutomationCurve.release` / `fl.automation.release` now flatten the curve AND ensure a placement — if no playlist clip references the channel they place one at tick 0 on `fl.playlist.first_free_track(...)`, as long as the envelope's span and never shorter than one bar — and return a `ReleaseResult` (a float carrying `placed_clip`, `track`, `start_tick`, `length_tick`, `clip_index`; `place_clip=False` opts out). There is still no native unlink, retarget or channel delete to expose (the native verbs are `automation_create|read|add|delete|set` only), so flattening plus a placement is the whole neutralising story.
- Evidence: `Fl-Automation/sdk/docs/automation-links.md` ("A curve is only evaluated through a placed clip"); `sdk/python/src/fruitylink/automation.py` (`release`, `_ensure_placement`), `automation_links.py`, `automation_records.py` (`ReleaseResult`); `sdk/python/tests/test_automation_links.py` (33 tests, incl. `test_release_places_a_clip_when_the_curve_has_none`, `test_release_leaves_an_existing_placement_alone`); `sdk/docs/python/api.md` ("Linked automation and complete envelopes")

## 2026-09-17 — Fl-Agent DeepSeek trace (F-minor example song)

Trace facts shared by every entry below: one DeepSeek-driven Fl-Agent run wrote an F-minor example song in **13 rounds / 21 tool calls / 199 s** against FL 26.1.3.5570 in an attached session. A single call accounted for **21 s** of that (the first `add_mixer_effect` of the session), and one call returned nothing but the MCP SDK's generic `An error occurred invoking 'fl_plugins_list'.`

### The first plugin instantiation of a session outran the 5 s bridge guard, then succeeded on retry

- Status: **fixed-unverified**. A live check must confirm, in a fresh FL session with a cold plugin cache: (1) `add_mixer_effect(track=20, slot=0, plugin_name="Fruity Parametric EQ 2")` as the FIRST plugin load now returns a verification line instead of throwing; (2) when it needs more than 20 s, the line carries `loaded after N ms; FL's UI was blocked while the plugin initialised` and the slot really holds the effect; (3) a genuinely empty slot still fails, with the new "still initialising or waiting on a dialog" wording and no licence claim; (4) the wider guard did not slow down or wedge any other bridge call.
- Seen: 2026-09-17, FL 26.1.3.5570, attached session (13 rounds / 21 tool calls / 199 s; this one call took 21 s)
- Call: `add_mixer_effect(track=20, slot=0, plugin_name="Fruity Parametric EQ 2")` (`fl_effect_add`), the first plugin load of the session
- Expected / actual: expected the effect loaded, or an error naming a real obstacle. Actual: `FL Studio did not respond within 5000 ms (visible FL windows: 'FL Automate', '<TApplication>'; a plugin waiting for a sign-in/licence or message box blocks FL's UI thread ...)` — and an IMMEDIATE retry succeeded in well under a second. There was no dialog: the 5 s guard was measuring first-load latency (FL constructs the plugin on its own UI thread, so the UI-thread probe correctly saw a wedged UI thread and then guessed the wrong cause), and the message sent the agent looking for a licence prompt that did not exist.
- Root cause: `LoadIntoChannelAsync` (channel generator, vtbl+0x150) and `LoadIntoMixerSlotAsync` (mixer FX slot, `EffectLoadVtableOffset`) both used `CallVtblAsync`'s default 5000 ms guard, while the preset/state loader next door already passed `timeoutMs: 20000` for exactly this reason. The guard's expiry was also treated as fatal without ever asking whether the plugin had in fact arrived.
- Workaround (before the fix): retry the same call once; the second attempt lands because FL has finished the cold scan.
- Fix path: one shared `FlInjectBridge.PluginInstantiationTimeoutMs = 20000` now guards generator loads, mixer FX slot loads AND the preset-state dispatcher (previously a bare `20000` literal), so all three instantiation paths share one budget; every other bridge call keeps its own guard. `LoadPluginWithRecoveryAsync` wraps the load: on a `TimeoutException` it settles for `PluginInstantiationSettleMs = 1500` (the orphaned native load still owns FL's UI thread, so an immediate peek would time out too) and re-inspects the target EXACTLY ONCE — the channel's plugin-holder name, or the FX slot's index+name fields `list_mixer_effects` reads. If the plugin is there the call returns normally; if the slot is empty, or FL will not answer the follow-up either, it raises a `TimeoutException` naming which of the two happened plus "the plugin is still initialising or waiting on a dialog", with no licence assertion. `AddMixerEffectAsync` changed from `Task` to `Task<string>` and returns a verification line (`mixer track 20 slot 0: 'Fruity Parametric EQ 2' loaded` plus the recovery note when applicable); `fl_effect_add` passes that through as `verification`, and `EffectSlot.load(plugin)` returns it. `AddChannelAsync` keeps returning the channel index (that is its contract), so its recovery note goes to the op log instead. A post-recovery rack/slot refresh that FL is still too busy to answer no longer fails a load that worked.
- Evidence: `Fl-Automation/sdk/src/FruityLink.FlStudio/Inject/FlInjectBridge.PluginLoading.cs` (new); `FlInjectBridge.Mixer.cs` (`AddMixerEffectAsync`, `LoadIntoMixerSlotAsync`, `ReadMixerSlotPluginNameAsync`), `FlInjectBridge.Channels.cs` (`AddChannelAsync`, `LoadIntoChannelAsync`), `FlInjectBridge.PluginState.cs` (`DispatchLoadStateFileAsync`); `sdk/src/FruityLink.Core/Abstractions/INativeFlControl.cs` (the 20 s budget is documented in both operation descriptions, so it reaches the catalog and the Python docstrings); `sdk/tests/FruityLink.FlStudio.Tests/PluginInstantiationTests.cs` (7 tests: shared budget, no-note fast path, recovered note after one settled re-inspection, empty slot still fails without a licence claim, unanswerable follow-up reported as such, non-timeout failures not recovered); `Fl-MCP/src/FlMcp.Plugin/CommandDispatcher.cs` (`add_effect` returns `verification`), `src/FlMcp.Server/FlTools.cs`, `docs/tools.md`. The generic bridge text still lives in `InProcBridge.TimeoutMessage` / `FlInjectBridge.Transport.cs` and is unchanged for every other call.

### `fl_plugins_list` failed with the MCP SDK's generic "An error occurred invoking ..." and no detail

- Status: **fixed-unverified**. A live check must confirm, in an attached session, that whatever made `fl_plugins_list` fail now arrives as a readable message (the bridge's own text, or `<TypeName>: <message>`, or the new bridge-deadline `TimeoutException` naming `plugins` and its budget) — and that a client-side cancellation of a running tool still reads as a cancellation rather than an error.
- Seen: 2026-09-17, FL 26.1.3.5570, attached session; the same operation worked through embedded Python in the same session
- Call: `fl_plugins_list(effects=false)` -> bridge operation `plugins` -> `InvokeAsync<string>("list_available_plugins")` (`CommandDispatcher.cs:52`)
- Expected / actual: expected the generator catalogue, or an error saying what went wrong. Actual: `An error occurred invoking 'fl_plugins_list'.` — the ModelContextProtocol SDK's placeholder for a tool body that threw something it will not disclose. Nothing about the failure reached the model, and the agent fell back to `fl_execute_python`.
- Root cause: `ToolErrors.Run` converted only `InvalidOperationException`, `ArgumentException`, `IOException`, `InvalidDataException` and `TimeoutException` into an `McpException`; every other type escaped the tool body unconverted and the SDK replaced it with that placeholder. The *cause of the missing detail* is therefore certain and fully fixed. The underlying throw is NOT identifiable offline: the plugin side of the pipe catches every exception and returns it as an error string (which becomes an `InvalidOperationException` and was always converted), so the escaping type had to be raised in the SERVER process, and the two reachable non-listed candidates are (a) the server's own `BridgeClient` deadline (`timeoutSeconds + 2`, i.e. 32 s for a tool call), which surfaced as a bare `OperationCanceledException`/`TaskCanceledException`, and (b) a `JsonException` from `result.Deserialize<SessionStatus>` in `ManagedSession.TryStatusAsync`, which needs a protocol/assembly skew between the server and the FL-side plugin and cannot be reproduced offline. A JSON shape mismatch on the `plugins` payload itself was ruled out: the handler returns a bare `string`, `Messages.Element` serializes it as a JSON string, and nothing on the server deserializes that into an object.
- Workaround (before the fix): run the same operation through `fl_execute_python` (`fl.plugins.available_text(effects=False)`), which reaches the SDK on its own pipe with no server-side deadline.
- Fix path: `ToolErrors.Run` now catches EVERY exception, rethrowing only (i) a cancellation of the MCP request's own token — the token is now threaded into all 31 `ToolErrors.Run(...)` call sites in `FlTools.cs` — and (ii) an `McpException` that is already actionable. Messages go through the same 64-hex-char token redaction; a type outside the known five is prefixed with its name (`JsonException: ...`); an empty message becomes `(the exception carried no message)`; and a cancellation that is not this request's says so in words instead of "A task was canceled.". Candidate (a) is fixed at the source as well: `BridgeClient.CallCoreAsync` converts its own deadline into `TimeoutException("The FL bridge did not answer 'plugins' within 32 s ...")`, naming the operation and the budget. `ManagedSession.TryStatusAsync` gained a `catch (TimeoutException) { return null; }` so the launch-readiness and project-settle polling loops keep treating a per-poll bridge timeout as "not ready yet" rather than a failure.
- Evidence: `Fl-MCP/src/FlMcp.Server/ToolErrors.cs`, `BridgeClient.cs` (`CallCoreAsync` / new `ExchangeAsync`), `ManagedSession.cs` (`TryStatusAsync`), `FlTools.cs` (every call site passes `ct`); `tests/FlMcp.Tests/ToolErrorTests.cs` (8 tests: the five known types keep their bare message, four other types are named, redaction applies to them too, an empty message still says something, own-token cancellation propagates, a foreign cancellation reads as a deadline, an `McpException` is not double-wrapped) and the updated `AttachmentTests.ActionableErrorsPreserveGuidanceButRedactTokens`.

### `set_song_end(bar=96)` on a 96-bar song cut the last bar

- Status: **fixed-unverified**. A live check must confirm that `fl.transport.set_song_end(after_bar=96)` puts the End marker at tick `96 * ticks_per_bar` and that a full-song render of a 96-bar arrangement then contains all 96 bars (the same tick `bar=97` produces).
- Seen: 2026-09-17, during the same run; the finished example song was one bar short
- Call: `fl.transport.set_song_end(96)` for a 96-bar arrangement
- Expected / actual: the agent read `bar=N` as "end after bar N". It means "marker at the START of bar N", so the song ended before bar 96 and the last bar never played or rendered. Placing it correctly needed `bar=97`, an off-by-one the docstring and the API table did not spell out.
- Root cause / Workaround / Fix path: `bar` was the only bar-shaped argument and its one-based bar-START semantics were implicit. The workaround was `bar=N+1` (or an absolute `tick=`). Fix: `set_song_end` gained an explicit `after_bar=` keyword — the marker lands at the start of bar `after_bar + 1`, so `after_bar=96` keeps bar 96 — mutually exclusive with `bar` and `tick` (passing two, or none, raises before any write, as does a non-integer, boolean or `< 1` `after_bar`). The docstring, the `fl.transport` member list, the composition-helpers row and the examples page now all say `bar=N` puts the marker at the start of bar N (the song ends before it) and point at `after_bar=N` to keep bar N.
- Evidence: `Fl-Automation/sdk/python/src/fruitylink/project.py` (`Transport.set_song_end`); `sdk/python/tests/test_composition_helpers.py` (`test_set_song_end_after_bar_keeps_that_bar` asserts the 96-bar case directly, `test_set_song_end_after_bar_respects_beats_per_bar`, plus six new rejection cases); `sdk/docs/python/api.md`, `sdk/docs/python/examples.md`

### Finding kick/clap/hat samples took three rounds of a capped free-text listing

- Status: **fixed-unverified**. A live check must confirm that `fl.samples.query("kick", limit=5)` returns `SampleInfo` records against the real library, that each `entry` loads unchanged through `fl.channels.add_sample` / `fl_sample_add`, that `next_offset` walks a filtered result set without repeats or gaps, and that one call over the full library returns in a usable time (it is a directory scan of FL's packs plus the user's Image-Line content on every page).
- Seen: 2026-09-17, during the same run (three of the 21 tool calls went on sample discovery)
- Call: `list_samples(filter=...)` (`fl.plugins.samples_text()`), repeated with different filters
- Expected / actual: expected to ask for kick/clap/hat paths and get them. Actual: one free-text blob — a legend line plus `[P]...`/`[U]...` entries, hard-capped at 40 entries unfiltered and 150 filtered with a `(N more — pass a filter)` nudge — which the model had to read, guess a narrower filter from, and re-request. Nothing in the output is machine-addressable: no per-entry fields, no paging, and the cap silently hides the rest.
- Root cause / Workaround / Fix path: sample discovery had no structured counterpart, unlike notes, clips and plugin parameters. The workaround was guessing filters until the cap stopped biting. Fix: a new `query_samples(filter, offset=0, limit=50)` operation on `IFlStructuredQuery` returns `FlQueryPage<FlSampleInfo>` with `entry` (the verbatim `[P]`/`[U]` string to pass straight to the sample loaders), `root_tag`, `relative_path`, `name`, `extension`, plus `next_offset` and `total`, following the existing `query_plugin_parameters` shape and the shared 1..512 page bound. Ordering is a stable case-insensitive sort of the entry strings so paging is consistent between calls; unlike the note and clip pages, `offset` and `total` count MATCHED samples rather than raw slots, because the filter is applied during the directory scan (documented in the contract, the Python docstring and the api.md paging paragraph). At most `MaximumSampleIndex = 10000` matches are indexed per call, since every page re-walks the roots. Exposed as `fl.samples.query(filter=None, offset=0, limit=50)`; `list_samples` is untouched and `fl.plugins.samples_text()` still returns it.
- Evidence: `Fl-Automation/sdk/src/FruityLink.Core/Abstractions/IFlStructuredQuery.cs` (`QuerySamplesAsync`, `FlSampleInfo`); `sdk/src/FruityLink.FlStudio/Inject/FlInjectBridge.Queries.cs` (`QuerySamplesAsync`, `IndexSamples`, `DescribeSample`); `sdk/python/src/fruitylink/samples.py` (`Samples.query`), `queries.py`, `models.py` (`SampleInfo`), `studio.py`, `__init__.py`; `sdk/tests/FruityLink.FlStudio.Tests/StructuredQueryTests.cs` (field splitting incl. an untagged entry and an upper-case extension; an empty terminal page that never touches the bridge; the shared page bounds refused before any scan); `sdk/python/tests/test_queries.py` (`test_sample_query_returns_loadable_entries_and_pages_on_matches`, `test_sample_query_defaults_and_page_bounds`); `sdk/docs/python/api.md`

## 2026-09-18 — Fl-Agent DeepSeek run 2 (A-minor song with Serum 2 + FabFilter)

Run facts shared by every entry below: one DeepSeek-driven Fl-Agent run wrote an A-minor song with Serum 2 and FabFilter effects in **19 rounds / 28 tool calls / 239 s** against FL 26.1.3.5570. One `Parameters.set_verified` pass over **50 FabFilter parameters took 88.7 s** — about **1.8 s per parameter**, more than a third of the whole run — because `set_verified` writes, re-reads and re-reads the display once per parameter. **Follow-up wanted (open):** a BATCH verified set — one native pass for N (index, value) pairs plus one readback sweep — so a 50-parameter plugin patch costs a couple of seconds instead of a minute and a half; today the only alternative is `parameters.set(...)` per parameter with no verification at all.

### Pattern clips do not loop, and nothing said so: a 4-bar clip of a 1-bar pattern is 3 bars of silence

- Status: **fixed-unverified (tiling); the no-loop behaviour itself is live-verified 2026-09-18**. A live check must confirm that `fl.playlist.tile_pattern(p, track=t, start_tick=0, length_tick=16 * BAR)` places sixteen 1-bar clips whose master capture has audio in EVERY bar (the same capture that measured bars 2-4 silent for one long clip), that the last clip of a span that does not divide evenly is shortened and the run ends exactly on the span, and that `add_patterns(..., repeat=True)` does the same in one native pass on a real project.
- Seen: 2026-09-18, FL 26.1.3.5570, during the A-minor run (19 rounds / 28 calls / 239 s)
- Call: `fl.playlist.add_patterns([PatternClipSpec(p, 1, 0, 4 * BAR)], enforce_lengths=True)` (equivalently `fl_clip_add(pattern, track, startTick, lengthTick)`)
- Expected / actual: expected a 1-bar pattern to REPEAT for the four bars the clip covers. Actual: the clip was placed exactly as asked — `(start 0, length 1536)`, four 384-tick bars at PPQ 96 — and a **master capture of that span had audio in bar 1 only; bars 2, 3 and 4 measured silent**. An FL pattern clip has no loop or offset flag: it plays its pattern once from the clip start and holds silence for the rest of its length. The agent had been placing 4-bar patterns as 8- and 16-bar clips, and the user saw "huge amounts of empty space". Nothing in the SDK, the tool description or the docs warned; `PatternClipSpec`'s "a positive length is pinned by the host" reads like the opposite promise.
- Root cause: the SDK modelled clip length as *how long the clip occupies the playlist* and never as *how many times the pattern plays*, and offered no way to express "fill this span". `add_patterns` even made the trap sharper: `enforce_lengths=True` deliberately resizes a clip BACK to the over-long spec, so the one mechanism that could have revealed the mismatch re-imposed it.
- Workaround (before the fix): one `PatternClipSpec` per repetition, at `start_tick + i * pattern_length`, with the pattern length read from `fl.patterns.list()`'s `lengthTick`.
- Fix path: `fl.playlist.tile_pattern(pattern, *, track, start_tick, length_tick)` (and `tile_pattern_beats`) fills a span with ONE CLIP PER REPETITION in one native pass, the last clip shortened to fit so the run ends exactly on the span, using the pattern's host-reported length (`fl.playlist.pattern_length(p)` / `fl.patterns[p].length_tick`, from `query_patterns`); a pattern the host cannot measure is refused rather than guessed, as is a span needing more than `MAX_TILED_CLIPS = 1000` clips (both before any write). `add_pattern`, `add_pattern_ticks` and `add_patterns` gained `repeat: bool = False`: `repeat=True` tiles (in `add_patterns`, every over-long spec is expanded before the single native pass), and `repeat=False` with a length longer than the pattern now emits `PatternClipLongerThanPatternWarning` naming the pattern, its length, the clip length and the exact number of ticks that will be silent. The same text comes back in the result — `add_pattern*` return a `PatternPlacement` (an int carrying the clip count plus `pattern_length_tick`, `repeated` and `notice`), `add_patterns` a `PatternPlacementBatch` (still the number of clips RESIZED, plus `placed`, `repeated` and `notices`) — so a caller that does not watch warnings still sees it. The advice costs ONE shared `query_patterns` per call (none when every length is 0) and a host that cannot describe its patterns silently gets no advice, so a placement is never failed or slowed by the check. `fl.playlist.gaps` / `onsets` already counted each clip's pattern exactly once; their docstrings now say that this is FL's REAL behaviour, verified above, rather than a simplifying assumption.
- Evidence: `Fl-Automation/sdk/python/src/fruitylink/playlist.py` (`tile_pattern`, `tile_pattern_beats`, `tile_specs`, `pattern_length`, `PatternClipLongerThanPatternWarning`, `PatternPlacement`, `PatternPlacementBatch`, `MAX_TILED_CLIPS`), `patterns.py` (`pattern_length_ticks`, `Pattern.length_tick`), `__init__.py`; `sdk/python/tests/test_pattern_tiling.py` (15 tests: tiling counts, last-clip shortening, the beats variant, an unmeasurable pattern and a runaway count refused before any write, the warning's text and the returned notice, the quiet paths, a host that cannot answer, `repeat=True` on all three entry points, one notice per over-long spec) and the updated `test_arrangement_fixes.py` call-sequence assertions; `sdk/src/FruityLink.Core/Abstractions/INativeFlControl.cs` (`AddPatternClipAsync` / `AddPatternClipsAsync` descriptions, so the no-loop rule reaches the operation catalog and the generated Python docstrings); `sdk/docs/python/api.md` ("Pattern clips do not repeat", with the live measurement), `sdk/docs/concepts.md`, `sdk/docs/python/examples.md` ("Repeat a pattern across a span"); `Fl-MCP/src/FlMcp.Server/FlTools.cs` (`fl_clip_add`)

### `list_samples` / `query_samples` never saw the user's own sample library

- Status: **fixed-unverified**. A live check must confirm, on this machine, that `fl.samples.query("kick")` returns `[B1]...` entries from `C:\Users\poofi\Documents\Splice\Samples\packs`, that such an entry loads unchanged through `fl.channels.add_sample` and through a managed MCP session (`fl_sample_add` staging it to `<workspace>\staged-samples\b1\...`), and that `FRUITYLINK_SAMPLE_ROOTS=C:\Users\poofi\Music\Samples` makes that folder appear as its own `[B...]` root.
- Seen: 2026-09-18, during the A-minor run; the agent was asked to "use my sampled drums"
- Call: `list_samples(filter=...)` / `fl.samples.query(...)` -> `SampleRoots()` in `FlInjectBridge.Channels.cs` and `IndexSamples` in `FlInjectBridge.Queries.cs`
- Expected / actual: expected the user's drums. Actual: the lister only ever searched `[P]` = `<FL install>\Data\Patches\Packs` and `[U]` = `Documents\Image-Line\FL Studio`, and `[U]` was empty of drums — the user's library lives elsewhere and appears in FL's browser only because FL's **Browser extra search folders** point at it. There was no way to reach it at all, so the request could not be satisfied by any filter.
- Root cause / offline finding: **FL 2026 stores the browser's extra search folders in the REGISTRY, not in a settings file.** `HKCU\Software\Image-Line\FL Studio <major>\Search paths` holds one `REG_SZ` per folder, named `0`, `1`, ..., whose value is `<absolute folder>,<display name>`. On this machine (read-only `reg query` / `Get-ChildItem HKCU:\Software\Image-Line -Recurse`, 2026-09-18) both the `FL Studio 25` and `FL Studio 26` keys hold exactly one value: `0 = C:\Users\poofi\Documents\Splice\Samples\packs,splice`. **Nothing under `C:\Users\poofi\Documents\Image-Line\FL Studio\Settings\` holds them** (that tree has only `Hardware\*.ini` and `Internet\Versions.xml`; the `Browser` folder holds no folder list), and `HKLM\SOFTWARE\Image-Line` has only `Registrations` and `Shared`. Worth recording: **`C:\Users\poofi\Music\Samples` is NOT one of FL's browser folders** — it appears in the registry only inside Edison/Slicex MRU lists — so the drums the user meant are not reachable through this setting as it stands today; they need adding in FL's browser, or through the environment variable below.
- Workaround (before the fix): pass a full absolute path to `add_sample_channel` (the resolver accepts one), which no listing would ever have shown.
- Fix path: one shared definition, `FruityLink.Core.Hosting.FlSampleRoots`, now produces the roots for BOTH the SDK lister/resolver (`FlInjectBridge.SampleRoots()`) and the MCP staging policy (`ScriptingSessionPolicy.DefaultSampleRoots()`), so a tag can never mean two different folders. It reads every `FL Studio <major>\Search paths` key, newest major first, splitting `<folder>,<display name>` from the right (a folder name may itself contain a comma) and keeping only folders that exist, and tags them `[B1]`, `[B2]`, ... The documented `FRUITYLINK_SAMPLE_ROOTS` environment variable (semicolon-separated absolute folders) adds roots FL knows nothing about and is what makes `C:\Users\poofi\Music\Samples` reachable without touching FL. The extra roots are searched BEFORE `[P]` and `[U]`, because a listing is capped (40 entries unfiltered, 150 filtered) and FL's factory packs are enormous, so the user's own samples would otherwise never make the cut. A folder that does not exist, repeats, or lies inside a root already in the list is dropped, so one file is never listed twice under two tags and every entry still round-trips through `ResolveSamplePath` unchanged. The legend line and `DescribeSample` needed no change: both were already written against the root list rather than the two fixed tags. On the MCP side the staging policy takes `[B...]` entries too, one workspace subfolder per tag (`staged-samples\b1\...`).
- Evidence: `Fl-Automation/sdk/src/FruityLink.Core/Hosting/FlSampleRoots.cs` (new: `Discover`, `Compose`, `BrowserSearchFolders`, `EnvironmentFolders`, `SearchPathFolder`); `sdk/src/FruityLink.FlStudio/Inject/FlInjectBridge.Channels.cs` (`SampleRoots`, `ListSamplesAsync`); `sdk/src/FruityLink.Core/Abstractions/INativeFlControl.cs` (`ListSamplesAsync` description, so the tag legend reaches the catalog); `sdk/tests/FruityLink.FlStudio.Tests/SampleRootTests.cs` (10 tests: tag order, no install directory, repeated/nested/missing/blank folders dropped, the comma-in-folder-name split, the environment variable, an unset variable, the live registry read never throwing and never returning a missing folder, and a `[B2]` entry splitting correctly); `sdk/docs/python/api.md` ("Sample search roots"); `Fl-MCP/src/FlMcp.Plugin/ScriptingSessionPolicy.cs`, `tests/FlMcp.Tests/ScriptingSessionPolicyTests.cs` (`BrowserRootEntryIsStagedUnderItsOwnTagFolder`, `DiscoveredRootsCarryTheTagsTheSdkListerEmits`), `src/FlMcp.Server/FlTools.cs` (`fl_sample_add`), `docs/sessions.md`

### `list_samples()` could not be called without a filter

- Status: fixed (offline-verified; the signature is generated from the contract and asserted by a test, so a live check adds nothing)
- Seen: 2026-09-18, during the A-minor run, while looking for drums
- Call: `fl.ops.list_samples()`
- Expected / actual: expected a browse of the sample library, which is exactly what the operation's own description offers ("optionally filtered by name"). Actual: `Operations.list_samples() missing 1 required keyword-only argument: 'filter'` — a Python-side failure before any request was made.
- Root cause: `INativeFlControl.ListSamplesAsync(string? filter, ...)` declared the parameter nullable but gave it no DEFAULT, and the Python generator (and the wire binder, which keys off `ParameterInfo.HasDefaultValue`) faithfully made it required. "Optional" in the prose, required in the signature.
- Workaround (before the fix): pass `filter=None` explicitly.
- Fix path: `ListSamplesAsync` and `ListPluginParamsAsync` — the other `string? filter` with no default — now declare `string? filter = null`, and `python/src/fruitylink/operations.py` was regenerated (`tools/generate_operations.py`, `--check` clean). The two structured queries (`query_samples`, `query_plugin_parameters`) already defaulted. Nullable parameters that are genuinely required keep no default, and a test now pins both halves of that rule.
- Evidence: `Fl-Automation/sdk/src/FruityLink.Core/Abstractions/INativeFlControl.cs`; `sdk/python/src/fruitylink/operations.py` (regenerated); `sdk/python/tests/test_operations.py` (`test_optional_filters_default_to_none` over all four filter parameters, and `test_nullable_without_default_stays_required` re-pointed at `add_arrangement`); `sdk/tests/FruityLink.Scripting.Tests/DispatcherTests.cs` (omitting an optional nullable binds null; a parameter with no default is still refused)

### An `EffectSlot` could not say what plugin it held

- Status: fixed (offline-verified against the host's `list_mixer_effects` text; a live check would only re-confirm that text, which `add_mixer_effect` already verifies itself)
- Seen: 2026-09-18, during the A-minor run, after loading FabFilter effects
- Call: `slot = fl.mixer[64].effects[0]`, then `repr(slot)` / anything that would name the plugin
- Expected / actual: expected to confirm what had been loaded. Actual: `<fruitylink.mixer.EffectSlot object at 0x...>` — the default object repr, and no readable property at all. `EffectSlot.load()` returns a verification line, but once that line is gone there was no way back to it; the only route was parsing `fl.mixer[t].effects.list_text()` by hand.
- Workaround (before the fix): `fl.mixer[t].effects.list_text()` and read the `slot N: <name>` lines yourself.
- Fix path: `EffectSlot.plugin_name` (the plugin as FL names it, or `None` for an empty slot), `EffectSlot.is_empty` and a `__repr__` of `EffectSlot(track=64, slot=0, plugin='Pro-Q 4')`. One `list_mixer_effects` call per read, cached on the handle and dropped by `load` / `load_state` / `clear` or by `refresh()`; a fresh `fl.mixer[t].effects[s]` always reads the live slot. The name is FL's plugin database name, the same string `load` takes, so it round-trips. `repr` never throws or guesses: a host that cannot answer prints `EffectSlot(track=64, slot=0)` with no plugin field. `Effects.names()` returns the whole chain as `{slot: name}` from ONE call (ten `plugin_name` reads would be ten calls) and `Effects.loaded()` returns the non-empty slots with their names pre-cached. `Channel` was left alone: it inherits `IndexedObject.__repr__`, so it already prints `Channel(index=3)` rather than the default object repr.
- Evidence: `Fl-Automation/sdk/python/src/fruitylink/mixer.py` (`effect_names`, `EffectSlot.plugin_name` / `is_empty` / `refresh` / `__repr__`, `Effects.names` / `loaded`); `sdk/python/tests/test_mixer.py` (6 tests: one call then cached, the repr with and without a plugin, an empty slot, a repr that survives a host refusal, a load forgetting the cache, and the whole chain in one call); `sdk/docs/python/api.md` ("Confirm what an effect slot holds")

### `fl_execute_python(timeoutSeconds=400)` was refused instead of clamped

- Status: fixed (offline-verified; the clamp and the warning are pure server-side argument handling, covered by tests at 400, 0 and -5 s)
- Seen: 2026-09-18, during the A-minor run, on a long Serum/FabFilter parameter pass
- Call: `fl_execute_python(code=..., timeoutSeconds=400)`
- Expected / actual: expected the script to run. Actual: `Timeout must be 1..300 seconds.` — the whole script was thrown away over an argument the server could simply correct, and the agent had to resend it. The cap itself is real (the plugin pipe will not hold longer), but refusing is the wrong response to a number that has an obvious nearest legal value.
- Workaround (before the fix): resend with `timeoutSeconds<=300`.
- Fix path: `ManagedSession.ExecutePythonAsync` clamps to `1..ManagedSession.PythonTimeoutCap` (300) with `Math.Clamp` instead of calling `ValidateTimeout`, runs the script at the clamped deadline, and adds a `warnings` entry — `TimeoutClamped: timeoutSeconds 400 is outside 1..300; the script ran with a 300 s deadline. Work that needs longer has to be split across calls.` — through the new `PythonResults.WithWarning`, which appends to an existing `warnings` array or creates one. The note is added AFTER `PythonResults.Bound`, so an oversized response cannot bury it in the saved file. Other deadlines (launch, render, section measure) still validate rather than clamp: those are the caller's own budget for a real wait, not a hard protocol limit. The `fl_execute_python` description now states the clamp.
- Evidence: `Fl-MCP/src/FlMcp.Server/ManagedSession.Python.cs` (`PythonTimeoutCap`, the clamp), `PythonResults.cs` (`WithWarning`), `FlTools.cs` (`fl_execute_python` description); `tests/FlMcp.Tests/SessionTests.cs` (`AnOutOfRangePythonDeadlineIsClampedAndReportedInWarnings` at 400/0/-5 s, `AnInRangePythonDeadlineIsUsedVerbatimAndAddsNoWarning`, `AClampedDeadlineStillWarnsWhenTheResponseIsOversized`)
