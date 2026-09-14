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
- Status: fixed-unverified (`select_channel(channel=)` / `set_channel_solo(channel=)`, legacy wire `index` kept as an alias in `OperationRegistry.LegacyArgumentAliases`, 2026-09-14 batch)
- Evidence: records/issues-v006.md #3

### Automation endpoints protected from deletion
- Status: fixed (set_point live-verified)

### Parameter display readback lags rapid writes in one request
- Status: docs/mitigated (2026-09-14 batch). No host fix: the raw value from the wrapper's `getParamValue` is immediate, the display text comes from the plugin instance and only syncs after FL's audio-thread/idle pass; no opcode to force it is known without live RE. `Parameters.set_verified` now compares decoded normalized values (never display strings) and keeps re-reading until the display moves (`VerifiedWrite.normalized_after`, `display_changed`).
- Evidence: records/issues-v006.md #5, records/live-validation-2026-09-13.md

### `query_plugin_parameters` filter appeared to ignore spaces
- Status: caller (limit bounds the scanned slots from offset). Docs updated; `Parameters.read(index)` added.

### `fl_project_start` schema lacked `background`; title "(untitled)"
- Status: fixed-unverified for background (server rebuilt; the tool schema shown to the client
  did not include it until the client reconnected). Title: fixed (live).

### Permission classifier denied read-only `fl_python_api(filter="marker")`
- Status: open (harness). Workaround: read the SDK source.

### Oversized Python results and exceptions discarding partial results
- Status: fixed-unverified (worker keeps `result` on exception as `resultPartial:true`; `encode_response` drops only `result` over 1 MiB; MCP `FL_MCP_PYTHON_RESPONSE_LIMIT` oversize envelope with `path` to the full JSON, 2026-09-14 batch)

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
- Status: fixed-unverified (deployed; refusal path not exercised live). Dispatcher route is the default.

### `load_state` verification line always says "0/12 sampled values changed"
- Status: fixed-unverified (`DescribeStateEvidence` now reports sizes, SHA-256 prefixes, differing-byte count/% and first differing byte of the state record instead of 12 sampled values, 2026-09-14 batch)

### No render range / selection render; audition of one clip costs a full-length render
- Status: fixed-unverified (`fruitylink.audition.isolate_bars/isolate_range`, `StudioSession.render_range(...)`, `fl_project_render(startBar, endBar, cutClips)` via a disposable-project transform; no native FL CLI range exists, 2026-09-14 batch)

### Plugin waveform/wavetable identity not readable through parameters
- Status: fixed-unverified (`fruitylink_serum.describe.describe_state(fl, channel)` reads `relativePathToWT`/`tableDisplayName` plus the frame from `kParamTablePos`; frame rule still inferred from one live point, 2026-09-14 batch)

### Enum/scale mappings undiscoverable (unison count, detune, sub shape, filter cutoff)
- Status: fixed-unverified (confidence fields and vocabularies in serum-support `data/*.json`; `tools/harvest_live.py` set-then-read probe added, harvest not yet run, 2026-09-14 batch)

### `PresetFile` has no `name` attribute; `vars()` fails on slotted records
- Status: fixed-unverified (`PresetFile.name`/`to_dict()` were already present; `IndexedPreset.to_dict()`, `LoadResult.to_dict()` added and docs say use `to_dict()` instead of `vars()`, 2026-09-14 batch)

### Duplicate-note addressing (channel/key/tick) can hit several notes
- Status: fixed-unverified (`NoteRef`/`NoteEdit.length_tick` narrows the match; `Notes.edit/delete(..., allow_multiple=False)` refuses a target hitting more than one note before any write, 2026-09-14 batch)

### Preset/wavetable browsing across Serum folders from Python
- Status: fixed-unverified (`list_folders`, `find_presets`, `resolve_preset` worked live for
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
- Status: fixed-unverified (`load_preset` returns `LoadResult` with `.warnings`, `.automation` and `.signal_path`; `loading.automation_links(fl, channel)`, 2026-09-14 batch)
- Seen: v007 draft; "KY - Smart Future" on channel 1
- Expected / actual: the song automates Serum Filter 1 Freq on that channel; the preset's oscillators have direct level 0 and feed FX buses, so the voice filter is out of the path and the automation did nothing audible (intro centroid 7.9 kHz vs 2.6 kHz).
- Workaround: keep the original patch where automation matters and add the new preset as a separate channel/layer.
- Fix path: load_preset could list FL automation links on the target channel and report which state keys they drive; the schema could flag presets whose osc direct levels are 0 or whose bus routing is active.

### Raw plugin-state hash is not a persistence oracle
- Status: caller/docs. Channel.get_state() bytes differ after reopen on untouched channels; decoded parameter diffs show only float noise. Compare decoded states (as done in state-before/reopened-v007.json).

### Multi-candidate audition in one render works but needed manual scaffolding
- Status: fixed-unverified (`fruitylink_serum.audition.audition_candidates(fl, source_pattern=, mixer_track=, candidates=, ...)`, 2026-09-14 batch). Pattern: add one Serum channel per candidate, route to the target mixer track, copy the source pattern's notes to a new pattern, place blocks back to back, delete markers, render once. A fruitylink_serum.audition_candidates(fl, source_pattern, mixer_track, candidates, accompaniment) helper would make it one call.

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
- Status: fixed-unverified (`fruitylink_serum.audition.audition_candidates`, same fix as v007, 2026-09-14 batch): channel per candidate, notes copied, blocks placed, markers deleted, one render.

### Per-section measurement is manual ffmpeg scripting
- Status: fixed-unverified (`fruitylink.analysis.describe_sections(source, bpm, sections={name: (start_bar, end_bar)})`, 2026-09-14 batch). Returns LUFS/RMS/peak/crest/centroid/width/bands per named section.

## 2026-09-13 - Ember Tides v009

### add_mixer_effect plugin name must match the plugin database file name
- Status: fixed-unverified (`PluginNameResolver`: exact, then letters/digits-only, then unique containment; ambiguity lists candidates, a miss lists the 5 closest; shared by add_mixer_effect/add_channel/clone_mixer_effect, 2026-09-14 batch). "FabFilter Pro-R 2" failed, "Pro-R 2" loaded.

### No band-energy check before committing a new voice
- Status: fixed-unverified (`fruitylink.analysis.band_energy(source, bands)` and `compare_bands(a, b, bands)` with `largest_increase/decrease`, 2026-09-14 batch). The harsh arps would have been caught by comparing 2-4 kHz and >5 kHz band RMS of the chorus against the previous revision before rendering the full song.

## 2026-09-14 - Ember Tides v011

### Preset load resets global plugin parameters written just before it
- Status: fixed-unverified (`SerumPatch.bend_range(up_semitones, down_semitones)` writes kParamBendRangeUp/Dn; `load_preset` now returns `LoadResult.warnings`, 2026-09-14 batch). set_plugin_param on Serum "Bend Down" was undone by a load_preset in the same script; the value had to be baked into the preset.

### Automation clip point times are relative to the clip start
- Status: docs. fl.automation.create(...) at bar 105 then set_points with beats 0..40 mapped correctly to bars 105-115; earlier song-start clips made this look absolute.

### Pitch bend via Serum's Pitch Bend parameter works; channel pitch range is not exposed
- Status: docs. Automating plugin parameter 7 gave a clean two-octave glide (verified by f0 tracking). FL channel pitch automation would need the channel's pitch range knob, which the SDK does not expose.

### Verifying a time-varying effect needs an isolated render plus a pitch track
- Status: fixed-unverified (`fruitylink.analysis.pitch_track(source, start_seconds=, end_seconds=)` with glide summary; the isolated render is now `StudioSession.render_range`, 2026-09-14 batch). Centroid on the full mix could not show the bend; a stripped project and a pure-Python autocorrelation did.

## 2026-09-14 - Ember Tides v012

### Channel-volume automation scale
- Status: docs (written up in docs/python/api.md "Scales and conventions", 2026-09-14 batch). AutomationTarget.channel_volume values 0..1 map to the 0..12800 channel scale (0.55 -> 7040 read back).

### No masking/clash check between two instruments
- Status: fixed-unverified (`fruitylink.analysis.masking_report(a, b, bands, clash_threshold_db=6)` per band louder/overlap plus `clashes`, 2026-09-14 batch). Deciding how much to duck the pad under the lead was done by ear-proxy (band RMS in 500-1600 Hz before/after).

## 2026-09-14 - Ember Tides v013

### Finding the empty spaces is manual
- Status: fixed-unverified (`Pattern.gaps(channel=None, min_beats=1.0)` and `Playlist.gaps(start_bar, end_bar, channel=None)` returning `Gap` records, 2026-09-14 batch). Choosing where to add answering figures meant reading each pattern's note ticks by hand.

## 2026-09-14 - Ember Tides v014/v015 (details, mix, master)

### Limiter gain parameter scale had to be probed
- Status: docs. Pro-L 2 "Gain" normalized 0..1 = 0..30 dB (0.5 -> +15, 0.6 -> +18, 0.1 -> +3). Same-request readback after a write showed the old value; the next request was correct. A per-plugin scale table (like the Serum parameter map) would remove this probing for FabFilter plugins.

### Mastering EQ skipped for lack of a cheap parameter map
- Status: open. Pro-Q 4 is installed but its band parameters would need probing; a FabFilter parameter-map generator (same set-then-read loop as Serum) is the fix.

### End-of-song silence via a marker
- Status: fixed-unverified (`Transport.set_song_end(bar)` places a single "End" marker, replacing a previous one, 2026-09-14 batch). An "End" marker past the last note extends the render; the helper still relies on marker behaviour.

### Sidechain pump needed 194 automation points by hand
- Status: fixed-unverified (`fl.automation.pump(target, start_tick, length_tick, track=, depth=, recovery_beats=, ceiling=)` plus pure `automation.pump_points`, 2026-09-14 batch)

## 2026-09-14 - Ember Tides v016/v017

### Velocity does not scale some Serum patches
- Status: fixed-unverified (`SerumPatch.velocity(amount, target="amp")` and `SerumPatch.modulate(source, target, amount)` over `data/mod-matrix.json`; Velocity source id 16 is inferred from library statistics, 2026-09-14 batch). The sine-sub patch played at full level regardless of note velocity, so a "soft" velocity-60 swell was as loud as the drop sub.

### Per-bar anomaly scan should be a helper
- Status: fixed-unverified (`fruitylink.analysis.scan_bars(source, bpm, bands=((None, 90), (4000, None)), step_db=6.0)` with paged bars, anomalies and summary, 2026-09-14 batch). A pure-Python scan of per-bar RMS in full, >4 kHz and <90 Hz bands located every reported problem in one pass; run it before every master.

### Loudness target belongs in the mastering step's inputs
- Status: docs. The first master aimed at -10 LUFS by genre habit; the user wanted -14 (Spotify). Record the target in the project README and make the limiter helper take `target_lufs`.

## 2026-09-14 - Ember Tides v018

### Transition effects need an A/B transition measurement
- Status: fixed-unverified (`fruitylink.analysis.transition(a, b, bpm, bar, window_seconds=0.43)` with before/after rms/peak/centroid deltas and each render's jump, 2026-09-14 batch). Comparing the last beat before the drop and the first beat of the drop between two renders confirmed the fix numerically.

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
- Status: open, workaround found (blocks `fruitylink_serum.loading.load_preset`, `describe.describe_state`, `Channel.get_state`/`load_state` in the session that was opened from the build-4726 template copy; after `fl_project_close` -> v002 and `fl_project_start(v003, sourceProjectPath=v002)` the same calls succeed: `get_state` ok, `load_preset(fl, 5, "PD - Lush Chorus")` -> state record changed 96.9 %, `describe_state` returns the patch)
- Seen: 2026-09-14, Parking Lot Moon phase 1, managed interactive session on Parking-Lot-Moon-v001.flp (template saved by build 4726, live FL 5570)
- Call: `loading.load_preset(fl, 1, "LD - Lush and Vintage")`; `fl.channels[1].get_state()`; same on GMS (ch 4) and 3x Osc (ch 7)
- Expected / actual: LoadResult / state bytes; actual `RemoteError: operation_failed: FL event 254 is truncated.` from `FlpPluginStateReader.ReadPayload` on the temp project snapshot. `LoadChannelPluginStateAsync` takes the "before" snapshot *before* dispatching the load and `TryReadChannelStateRecordAsync` only catches InvalidOperationException/IOException, so an InvalidDataException aborts the load and nothing is applied. Parsing the same snapshot (`scratch/state-probe-v001.flp`, saved with `fl.project.save_copy`) with the reader's algorithm shows the desync: FL 5570 writes event 0xAC (172) with a 3-byte payload at offsets 48 and 182 (`ac 01 01 00 | c0 36 "FL Studio 26.1.3.5570.5570"` and `ac 00 01 00 | 00 ed 10 <ProjectTime>`), the reader assumes 4 bytes for ids 128..191, runs one byte ahead and eventually reads garbage (`fe ff ff 40` -> event 254, length 1,064,959 in a 135 KB file). Ember-Tides-v018-master.flp (same build) has the same bytes but happens to resync, which is why state reads worked there. The template v001 (build 4726) has no 0xAC event and parses cleanly.
- Workaround: close to a fresh vNNN and reopen from it (the FLP written by FL 5570 through the close path parses), then load presets. Before that was known, the Serum 2 sounds were authored with `set_plugin_param` on named wrapper parameters (filter, unison, envelopes, FX) and read back with `parameters.page`, not `describe_state`. Persistence check compares parameter displays instead of state bytes.
- Evidence: `Parking-Lot-Moon/scratch/state-probe-v001.flp`; parser transcript in this session; `sdk/src/FruityLink.FlStudio/FlpPluginStateReader.cs:114`, `FlInjectBridge.PluginState.cs:34,188`

### Sample channels report "hosts no generator plugin (automation/bus channel)" for get_state
- Status: docs
- Seen: 2026-09-14, Parking Lot Moon phase 1
- Call: `fl.channels[8].get_state()` on a Sampler channel loaded with `fl.channels.add_sample`
- Expected / actual: a message saying Sampler channels keep no wrapper state; actual "Channel 8 hosts no generator plugin (automation/bus channel)", which misclassifies a sample channel.
- Workaround: ignore; sample channels have no plugin state to read.
- Evidence: this session

### Template mixer has 16 inserts; naming insert 17 is refused
- Status: caller
- Seen: 2026-09-14, Parking Lot Moon phase 1
- Call: `fl.mixer[17].name = "Vox"` on the default template (`get_mixer_track_count` = 18 including Master and Current)
- Expected / actual: rename; actual `invalid_arguments: Mixer track must be 0..16 (Master and ordinary inserts); Current and dormant slots are unavailable`. The loop aborted mid-way but inserts 1..16 were renamed.
- Workaround: `fl.mixer.add("Vox")` returned index 17, then route to it.
- Evidence: this session

### `inventory.query_index(text=...)` misses single-word queries that match preset names
- Status: open
- Seen: 2026-09-14, Parking Lot Moon phase 1
- Call: `inventory.query_index(root, text="soft")`, `"Soft"`, `"cotton"`, `"analog"`, `"warm"`, `"tape"`, `"80s"` (root = Serum 2 Presets)
- Expected / actual: at least "PD - Analog Soft Cotton" / "PD - Analog Butter" ("You want warm pads"); actual empty tuples, while `text="analog pad"`, `"dream"`, `"synthwave"`, `"nostalgic"`, `"retro"` return rows.
- Workaround: browse with `category="Pad"` / `"Lead"` / `"Keyboard"` (limit 200) and filter `location` in Python.
- Evidence: this session

### No channel delete: the template's empty "Sampler" channel stays in the rack
- Status: open
- Seen: 2026-09-14, Parking Lot Moon phase 1
- Call: catalog search for delete/remove channel; none exists (only `clear_pattern`, `delete_clip(s)`, `remove_mixer_effect`)
- Expected / actual: a way to drop channel 0 after adding real channels; actual it stays, renamed "(unused template)" and routed to Master.
- Workaround: rename and ignore; note in records.
- Evidence: fl_python_api catalog

### Drum-insert routing to a bus cannot be verified: no send/route readback
- Status: open
- Seen: 2026-09-14, Parking Lot Moon phase 1
- Call: `fl.mixer[i].send_to(15, 1.0)` then `fl.mixer[i].send_to(0, 0.0)` for inserts 8..14 (kick..perc -> "Drum Bus")
- Expected / actual: both accepted, but there is no `get_mixer_send` / route query, so whether the master route is actually disabled (or only its level zeroed) is unknown until a render.
- Workaround: phase 2 should confirm in the FL mixer or by rendering the drum bus muted.
- Evidence: fl_python_api catalog (only `set_mixer_send`)

### `playlist.add_patterns` ignores `PatternClipSpec.length_tick`: clips take the pattern's own length
- Status: open
- Seen: 2026-09-14, Parking Lot Moon phase 1
- Call: `fl.playlist.add_patterns([PatternClipSpec(36, 9, 0, 3072), ...])` (75 clips)
- Expected / actual: 8-bar clips; actual `fl.clips.list()` reports the pattern length instead (Tex Bed 3840 = 10 bars because its last hiss note ends at tick 3840; Pad Intro 3456 because legato pad notes overhang the bar by 8 ticks; humanised drum hits push kits to 9 bars), so consecutive clips on one track overlapped by 1-2 bars.
- Workaround: `fl.clips.resize([ClipResize(index, length_tick), ...])` afterwards (works, verified 3840 -> 3072); or keep every note strictly inside the intended bars.
- Evidence: this session (clip 1 before/after)

### `fl.clips.resize` takes a sequence of `ClipResize`, not `(index, length)`
- Status: docs
- Seen: 2026-09-14, Parking Lot Moon phase 1
- Call: `fl.clips.resize(1, 3072)`
- Expected / actual: resize; actual `TypeError: Clips.resize() takes 2 positional arguments but 3 were given`; signature is `resize(resizes: Sequence[ClipResize])`. Same shape for `move`.
- Workaround: `from fruitylink import ClipResize; fl.clips.resize([ClipResize(1, 3072)])`.
- Evidence: this session

### `parameters.page(limit=4240)` refused: page limit is 512
- Status: docs
- Seen: 2026-09-14, Parking Lot Moon phase 1
- Call: `fl.channels[1].parameters.page(offset=0, limit=4240)` to dump Serum 2's parameter names
- Expected / actual: one page (docs only say "bounded reads"); actual `IndexError: Index must be an integer >= 1 and <= 512.`
- Workaround: loop on `next_offset` with limit 512 (9 requests inside one fl_execute_python, ~4240 rows in well under the timeout) and dump to `scratch/serum2-params-ch1.json`.
- Evidence: this session

### Serum 2 through the wrapper: FX slots are only "FX Main Param 1..16", oscillator wavetables and sub shape are unnamed enums
- Status: open (docs)
- Seen: 2026-09-14, Parking Lot Moon phase 1 (preset loading blocked, so patches were authored by parameter)
- Call: `set_plugin_param` on Serum 2 channels; parameter dump `scratch/serum2-params-ch1.json`
- Expected / actual: a way to enable/choose an effect (chorus) or a wavetable frame by name; actual the effect rack exposes 16 anonymous "FX Main Param n" per bus and no effect-type/enable parameter, `A WT Pos` is a bare frame index, `Sub Shape` is an enum whose order had to be probed by writing values (0.25 -> "RoundRect"). Calibration (0..1 -> display): Filter 1 Freq 0.5=425 Hz, 0.6=937, 0.7=2064, 0.8=4549, 0.9=10025; Env Attack 0.3=78 ms, 0.4=328 ms, 0.5=1.00 s, 0.6=2.49 s; Env Release 0.4=328 ms, 0.5=1.00 s, 0.6=2.49 s, 0.7=5.38 s; Unison = 1+15v; Uni Detune = v^2; Uni Width = 200v-100; Fine = 200v-100 cents; Sustain 0.9=-1.8 dB, 1.0=0 dB.
- Workaround: chorus/reverb for the lead come from mixer effects in phase 2; wavetable stays the init saw frame.
- Evidence: this session; `scratch/serum2-params-ch1.json`

### Stock effect parameter names carry a "^b^a" prefix
- Status: docs
- Seen: 2026-09-14, Parking Lot Moon phase 2
- Call: `fl.mixer[4].effects[1].parameters.page()` on Fruity Chorus / Reeverb 2 / Delay 3 / Parametric EQ 2 / Limiter / Compressor / Soft Clipper / Stereo Shaper / Hyper Chorus / Vintage Chorus
- Expected / actual: names such as "Wet level"; actual every stock (non-wrapper) parameter is named "^b^aWet level" (FL hint-formatting bytes), Vintage Chorus band 0 is "^b^a^^(shift-click for I + II) ^Mode". `find(name)` / `set_named(name, v)` therefore need the prefix; wrapper (FabFilter) names are clean.
- Workaround: address stock parameters by index (dumped once per plugin) and verify with `set_verified(index, v)`; strip `^.` sequences before matching names.
- Evidence: this session (phase-2 dumps in `records/phase-2-mix.md`)

### Send levels ARE readable (effects.list_text "sends:" line); unity is 0.8, not 1.0
- Status: docs (corrects phase-1 entry "Drum-insert routing to a bus cannot be verified")
- Seen: 2026-09-14, Parking Lot Moon phase 2
- Call: `fl.mixer[8].effects.list_text()` -> "sends: ->0 'Master' (0), ->15 'Drum Bus' (1)"; untouched inserts read "->0 'Master' (0.8)"
- Expected / actual: `set_mixer_send` docs say "1.0 ≈ unity"; actual the default Master route of every insert reads 0.8, so 1.0 is above unity (FL send/volume scale 0..16000 with 12800 = 0 dB = 0.8). Phase 1 had the seven drum inserts feeding the Drum Bus at 1.0 (hot); the Master route at 0 is confirmed off (level 0, no flag readable).
- Workaround: `send_to(dst, 0.8)` for unity; check the "sends:" line after every routing write. The docstring should say 0.8 = unity and mention the readback.
- Evidence: this session

### No Sampler channel-settings ops: time stretch, sample start/end, fades, reverse, stretch mode
- Status: open
- Seen: 2026-09-14, Parking Lot Moon phase 2 (Vox E8 loop is 112 BPM, brief asks for 100 BPM; roomtone is 70 s under an 8-bar intro)
- Call: catalog search of the 144 ops for stretch/tempo/sample start/fade/reverse; only `add_sample_channel`, `replace_channel_sample`, `set_channel_pitch` exist
- Expected / actual: a way to set the Sampler's time-stretch (mode + tempo/multiplier) or trim/fade/reverse the sample; actual nothing, and Edison is GUI-only.
- Workaround: audio edited offline in embedded Python with the `wave` module (no numpy in FL's Python 3.14): `Samples\Tex_Roomtone_Suburb-8bars.wav` (19.2 s, 1 s fade in / 2.5 s fade out) swapped in with `replace_channel_sample`; `Samples\Vox_E8_everywhere_112_Gbm-reversed.wav` (last 4.8 s reversed, faded). The vocal stays at 112 BPM: it only plays in the drum-less interlude, so the tempo mismatch is a texture, not a rhythm error; a pure-Python WSOLA was judged too slow. Phase 3/4 may set stretch in the GUI.
- Evidence: this session

### Fruity Delay 3 exposes three parameters named "Distortion" (18, 19, 20)
- Status: docs
- Seen: 2026-09-14, Parking Lot Moon phase 2
- Call: `fl.mixer[4].effects[3].parameters.page()`
- Expected / actual: unique names; actual indices 18-20 all read "^b^aDistortion", so `set_named`/`find` cannot address them.
- Workaround: indices. Calibration for the record: Time = 16v steps ("3:1" = dotted 1/8 at v 0.1875), Feedback = 125v %, Output wet/dry linear %, Feedback cutoff 0.55 -> 2437 Hz, 0.57 -> 2893 Hz, 0.6 -> 3577 Hz.
- Evidence: this session

### `set_verified` reports verified=False when the slot already holds the value
- Status: docs
- Seen: 2026-09-14, Parking Lot Moon phase 2
- Call: `parameters.set_verified(2, 1.0)` on Delay 3 "Tempo sync" (already On), "Output dry" (already 100%), Hyper Chorus "Modulation amount" (already 50%)
- Expected / actual: verified=True (value is in place); actual `verified=False, display_changed=False` because the raw value never changed, which reads like a failed write in a batch summary.
- Workaround: treat `normalized_after == value` as success; compare displays. A `already_set` flag on VerifiedWrite would remove the ambiguity.
- Evidence: this session

### No sidechain routing: Fruity Limiter has no sidechain-source parameter, Pro-C 3 "External" has no source
- Status: open
- Seen: 2026-09-14, Parking Lot Moon phase 2 (brief: Fruity Limiter COMP on the bass keyed from the Kick insert)
- Call: `fl.mixer[6].effects[2].parameters.page()` on Fruity Limiter (18 parameters: gain, sat, limiter, comp threshold/ratio/knee/attack/release/curve/RMS, noise gate; no sidechain input); `Pro-C 3` parameter 21 "Side Chain Input" accepts 0.25..0.34 -> "External" (0 Internal, 0.5 Host Sync, 1.0 MIDI); catalog has only `set_mixer_send(src, dst, level)` with no sidechain flag
- Expected / actual: a way to mark the 8 -> 6 route as a sidechain (FL right-click "Sidechain to this track") so the limiter/Pro-C 3 sees the kick; actual a plain send would sum the kick into the bass insert, and the wrapper's sidechain input stays silent, so Pro-C 3 External would never compress.
- Workaround: Fruity Limiter left in COMP mode on insert 6 with sidechain-ready settings (threshold -11.8 dB, 1:3.0, knee 40 %, attack 3.08 ms, release 163 ms) acting on the bass itself; Pro-C 3 removed; route 8 -> 6 created at level 0 as a placeholder. Phase 3 can emulate the duck with a mixer-volume automation clip on insert 6 keyed to the kick pattern, or the user can flip the route to sidechain in the GUI. An `set_mixer_send(..., sidechain=True)` op is the fix.
- Evidence: this session

### Mixer-volume dB curve is undocumented; gain staging done in plugin output gain instead
- Status: docs
- Seen: 2026-09-14, Parking Lot Moon phase 2
- Call: `fl.ops.get_mixer_volume` (all inserts 12800), `set_mixer_volume` docs "no dB conversion is defined"
- Expected / actual: a documented raw -> dB mapping (FL mixer: 12800/16000 = 0 dB, max +5.6 dB, curve unknown; Ember Tides 6800 -> 12800 measured +11.8 dB, which fits neither a linear nor the +5.6 dB power curve); actual none, and renders are forbidden in this phase.
- Workaround: leave every insert at 12800 (0 dB) and trim with dB-exact plugin gains verified by display: Pro-Q 4 "Output Level" (index 556, 0.45 -> -3.60 dB, so dB = 72 v - 36) and Parametric EQ 2 "Main level" (index 35, dB = 36 v - 18). Send levels use the 0.8 = unity scale.
- Evidence: this session

### Unlicensed Super VHS instance hung the next embedded request (60 s timeout), not the MCP
- Status: caller (plugin licensing), docs
- Seen: 2026-09-14, Parking Lot Moon phase 2
- Call: `fl.mixer[16].effects[4].load("Super VHS")` succeeded and its 9 parameters read (Heat/Wash/Drift/Magic/Mix/Output/Static/Shape/Bypass, all defaults); the NEXT request (`fl.mixer[16].effects[0].load("Fruity Parametric EQ 2")` ...) hung at its first native call and timed out ("FL operation timed out or was cancelled"), leaving insert 16 slot 0 empty.
- Expected / actual: parameter writes; actual the plugin had opened its cloud sign-in dialog (unauthenticated instance) and blocked FL's UI thread. After the user signed in and reloaded the plugin, a retry in the same session read and wrote every parameter by display (Heat 15 %, Wash 10 %, Drift 10 %, Static 5 %, Mix 30 %, Output 80 %). Cause: plugin licence state, not the bridge; the bridge only lacks a way to detect/dismiss a modal plugin dialog.
- Workaround: sign in / authorise cloud plugins in the GUI before scripting them; after a timeout, re-inspect `effects.list_text()` for empty slots and redo the request. A "modal dialog open" hint in the timeout error would save a round trip.
- Evidence: this session

### `AutomationPointSpec.tension` direction is undocumented (positive = fast start, slow finish)
- Status: docs
- Seen: 2026-09-14, Parking Lot Moon phase 3
- Call: `fl.automation[21].set_points([... P(96, 0.662), P(127.98, 0.809, 0.3) ...])` (pad Pro-Q 4 High Cut opening across bars 25-33), then `seek` to bar 29 and read the display
- Expected / actual: docs say only "tension -1..1"; a slow opening that accelerates into the downbeat was wanted. Actual: tension +0.3 on the segment's end point gave 5698 Hz at the half-way bar (89 % of the travel at 50 % of the time), i.e. positive tension bends the segment fast-early; -0.3 gave 2282 Hz at bar 29 and 3004 Hz at bar 31 (slow-early, accelerating). `pump()` uses +0.5 on its recovery segment, which therefore recovers fast then eases.
- Workaround: negative tension for swells that accelerate into a downbeat; positive for compressor-like recoveries. The docstring should state the sign convention and that the tension belongs to the segment ending at that point.
- Evidence: this session (bar-29 display 5698 Hz vs 2282 Hz for +0.3 / -0.3)

### No automation inventory: `fl.automation` has no list/describe, event ids only via `get_channel_plugin`
- Status: open
- Seen: 2026-09-14, Parking Lot Moon phase 3
- Call: `dir(fl.automation)` -> only `create`, `pump`, `__getitem__`; `fl.ops.query_channels()` marks automation channels like any other channel
- Expected / actual: a `describe()`/`list()` that returns every automation channel with its target, event id, point count and clip placements; actual the inventory had to be built by filtering `query_channels()` on the "PLM - " name prefix, calling `get_channel_plugin(channel=i)` (prints "automation clip -> event 0x71008030") and `automation[i].list()` per channel, and matching `query_clips()` rows with `source_kind == "channel"`.
- Workaround: the loop above; `AutomationTarget.from_event_id` decodes the printed id back to a target (checked: 0x71008030 = insert 4 slot 0 param 48, 0x76001fc0 = insert 24 volume, 0x70001fc0 = Master volume).
- Evidence: this session; `records/phase-3-state-v008.json`

### Automation clips cannot loop or offset: one 1-bar tile cannot follow the alternating kick pattern
- Status: docs / caller
- Seen: 2026-09-14, Parking Lot Moon phase 3 (bass sidechain emulation on `AutomationTarget.mixer_volume(6)`)
- Call: `AutomationCurve.add_clip(track, start_tick, length_tick)` re-places the same channel (curve restarts at each placement, no clip offset/loop flag); the kick pattern is a 2-bar cycle (odd bars 1 + 2.5, even bars 1 + 3; choruses 1 + 3 / 1 + 2.5 + 3; pre-chorus 1 only)
- Expected / actual: a tiled 1-bar duck matching every kick; actual impossible with one envelope, and 80 separate placements of a 2-bar tile would still miss the chorus/pre variants.
- Workaround: read the kick notes of patterns 29-33 through the playlist clips on track 7 (162 hits in verses/pre/choruses only), and write ONE full-song clip with a computed 488-point list (hold 0.8 one tick before each hit, 0.69 at the hit, back to 0.8 after 24 ticks = 150 ms with tension 0.5). `pump()` itself is only for regular grids (`beats_per_hit`).
- Evidence: this session (`PLM - Bass duck (insert 6)`, channel 36, event 0x71801fc0)

### No `AutomationTarget` for mixer send levels; automated the send insert's own volume instead
- Status: open
- Seen: 2026-09-14, Parking Lot Moon phase 3 (delay-send pulses at phrase ends)
- Call: `AutomationTarget` kinds are channel volume/pan/pitch, mixer volume/pan, plugin_parameter; `set_mixer_send(src, dst, level)` has no automation counterpart
- Expected / actual: automate Lead->24 and Vox->24 send levels; actual no such target, so the pulse is on `mixer_volume(24)` ("Delay Send" return insert), which moves all sources together and lowers the phase-2 baseline (0.66 in verses = below the 0.8 unity that phase 2 assumed; 0.8 at phrase ends and through the interlude, 0.72 in the outro).
- Workaround: the return-insert volume; alternatively the per-insert Delay 3 "Output wet" (index 23) on inserts 1/4/5.
- Evidence: this session (`PLM - Delay send level (insert 24)`, event 0x76001fc0; readback 10560 at bar 20, 12800 at bars 16 and 52)

### Fruity Reeverb 2 parameter scales are undocumented; first writes landed far off
- Status: docs
- Seen: 2026-09-14, Parking Lot Moon phase 3 (new Reeverb 2 on the Master, wet 3 %)
- Call: `set_plugin_param(channel_or_track=0, slot=0, param_index=i, value=v)` with guessed v (Low cut 0.25 -> 764 Hz, High cut 0.6 -> 13.5 kHz, Predelay 0.2 -> 200 ms, Decay 0.35 -> 7.1 s, Dry 1.0 -> 125 %)
- Expected / actual: a documented v -> display mapping; actual none, three calibration writes per knob (display lags within one request, so each read needed a 0.25 s pause).
- Workaround: measured: Low cut 0.05 = 168 Hz, 0.075 = 243, 0.1 = 317, 0.15 = 466; High cut and High damping 0.2 = 4.8 kHz, 0.21 = 5.0, 0.3 = 7.0, 0.4 = 9.1; Predelay ms = 1000 v (0.02 = 20 ms); Decay 0.1 = 2.1 s, 0.12 = 2.5, 0.15 = 3.1, 0.2 = 4.1; Dry, ER and Wet levels = 125 v %. Room size = 100 v.
- Evidence: this session

### `query_plugin_parameters` `raw_value` is a float bit pattern, not the normalized value
- Status: docs
- Seen: 2026-09-14, Parking Lot Moon phase 3
- Call: `fl.ops.query_plugin_parameters(channel_or_track=4, slot=0, offset=48, limit=1).items[0].raw_value`
- Expected / actual: the 0..1 value written by the automation clip; actual 1059075707 (= 0x3F203A7B, the IEEE-754 bits of 0.626) with no `normalized_value` field, so the display string is the only readable value and numeric verification needs `struct.unpack("f", struct.pack("I", raw))`.
- Workaround: decode the bits or compare displays.
- Evidence: this session

### Transport `seek` readback drifts 14-20 ticks; bar-level automation values read fine, 150 ms ducks do not
- Status: open
- Seen: 2026-09-14, Parking Lot Moon phase 3 (verifying clips by seeking and reading plugin displays / mixer volumes)
- Call: `fl.ops.seek(tick=3072)`; `time.sleep(0.12)`; `fl.transport.state_text()`; `fl.ops.get_mixer_volume(track=6)`
- Expected / actual: position 3072 and the duck's dip value (0.69 -> 11040); actual `pos=bar 9 beat 1 (tick 3086)` with playing=no, every seek landing 14-20 ticks late (about the wait time at 100 BPM), so the 24-tick dip is never sampled (12784-12800). Bar-scale curves verify well this way (Pad LP 1500/1800/7000/2500/8000/1000 Hz at bars 1/20/33/52/88/107, drum LP 2989 Hz at tick 12254 vs 20000 Hz at 12294, lead shelf -5.00/0.00 dB, mixer volumes 10560/12800/0).
- Workaround: verify short envelopes from the point list; seek for bar-level checks only. A stopped seek should not advance.
- Evidence: this session

### The named SDK venv has no numpy/soundfile; the `fruitylink.analysis` helpers are pure Python and slow on a full song
- Status: caller / docs
- Seen: 2026-09-14, Parking Lot Moon phase 4 (verifying the v009 full render, 259 s stereo float WAV)
- Call: `sdk\python\.venv\Scripts\python.exe -c "import numpy, soundfile"` -> `ModuleNotFoundError`; `fruitylink.analysis.loudness` imports only `array`/`math`
- Expected / actual: the phase brief promised numpy/soundfile in that venv; actual it only has the SDK, and `load_wav` returns an `AudioSource` without `summary()` (the docs' `audio.summary(loudness=True, true_peak=True)` is on `fl.analysis.wav(...)`, the `AudioAnalysis` wrapper). `scan_bars` + `describe_sections` on the full 259 s render took about 2.5 min of pure Python.
- Workaround: system Python 3.12 (numpy 2.5.1, soundfile) with `PYTHONPATH=sdk\python\src` for the SDK helpers; whole-file integrated LUFS / true peak from `ffmpeg -af ebur128=peak=true`; own numpy FFT band envelopes for the duck check. A `pip install -e .[analysis]` extra (numpy, soundfile) and a numpy fast path in `scan_bars` would remove this.
- Evidence: this session; `Parking-Lot-Moon/records/verification-v009.json`

### GMS authored by parameter renders silence; its displays are raw fractions; a `.gmsynth` preset loads through `load_channel_plugin_state`
- Status: open (docs)
- Seen: 2026-09-14, Parking Lot Moon phase 4 (first full render v009: intro bars 1-6 at -50 dBFS, pad harmonics at -70 dB; verse/chorus pad missing entirely)
- Call: phase 1 `set_plugin_param` on GMS channel 4 (osc mixes, unison 4, detune, amp ADSR, filter cutoff 0.60); phase 4 `fl.channels[4].parameters.page()` -> "CHN: Amp Attack = 0.50 %", "Filter Cutoff = 0.60 %" (no units, no oscillator waveform parameter at all); `fl.ops.load_channel_plugin_state(channel=4, path=r"...\GMS\Pads & Textures\Smooth & Warm TE.gmsynth")`
- Expected / actual: a pad; actual the parameter-authored GMS instance is inaudible (bars 1-5 150-400 Hz at -58 dB; shortening the attack to 0.10 changed nothing, so it is not an envelope problem: GMS oscillators are single-cycle "Synth Waves" .wav files chosen in the GUI, which the parameter list never exposes, and the fresh instance evidently has none loaded). The `.gmsynth` preset loaded in place through the dispatcher route (verification line: same instance, 226 differing bytes = 4.5 %, unison 10, cutoff 0.42, amp level +4.5 dB) and the pad became audible (150-400 Hz -58 -> -40 dB in the same intro render). The op docstring says proprietary preset files are "silently ignored" (true for .SerumPreset); for GMS it works.
- Workaround: load a factory `.gmsynth` first, then trim by parameter; verify a new synth voice with an isolated section render before building a mix on it. Docs should list which native plugins accept their own preset format through opcode 0x12 and warn that GMS/3x Osc waveform choices are not parameters.
- Evidence: `Parking-Lot-Moon/scratch/v010-intro-attack010.wav` vs `v010a-intro-gmspreset.wav`; `records/phase-4-master.md`

### `set_channel_volume` takes `value=`, `set_mixer_volume` / `fl_mixer_set` take `volume=`
- Status: docs
- Seen: 2026-09-14, Parking Lot Moon phase 4
- Call: `fl.ops.set_channel_volume(channel=15, volume=8000)`
- Expected / actual: same keyword as the mixer op; actual `TypeError: ... unexpected keyword argument 'volume'. Did you mean 'value'?` (one wasted request; `fl_python_api` shows the name but the two ops are inconsistent).
- Workaround: `value=`; query_channels().volume reads it back (raw 0..12800).
- Evidence: this session

### `fl_project_start` reports the template tempo (140) before the resumed project settles
- Status: open
- Seen: 2026-09-14, Parking Lot Moon phase 4 (`fl_project_start(projectPath=v011, sourceProjectPath=<render snapshot v010>)`)
- Call: the start result said `"tempo":140` for a 100 BPM project; `fl_status` and `fl.ops.get_tempo()` a few seconds later both said 100, and the following full render was 259.2 s (correct for 108 bars at 100 BPM).
- Expected / actual: the readiness result should carry the loaded project's tempo (it is documented as "Returns readiness, tempo and PPQ"); actual it can carry the template default (140) if FL has not finished applying the loaded project when the probe runs. Every other resume this session reported 100, so it is a race, not a corruption.
- Workaround: re-read with `fl_status` before using the tempo from the start result.
- Evidence: this session (v011 start result vs fl_status)

### Channel volume raw scale is a power curve: 5000 is about -18 dB, 3200 about -29 dB; quiet texture samples vanished
- Status: docs
- Seen: 2026-09-14, Parking Lot Moon phase 4 (textures and the vocal loop inaudible in v009/v010; isolated stem render `scratch/v011-stem-texvox-49-56.wav`)
- Call: phase 1 `set_channel_volume` 3200/2800/3600 (textures), 5000 (vox), 10000 (kick); `query_channels().volume` reads them back but no dB is documented for channels either
- Expected / actual: phase 1 treated 3200/12800 as roughly -12 dB; actual the mixer-style curve (0.8 = 0 dB, 1.0 = +5.6 dB, exponent about 2.9) gives 3200 -> about -29 dB and 5000 -> about -18 dB, and the Splice texture files are themselves very quiet (`Tex_TapeHiss.wav` RMS -53.6 dBFS, `Tex_Roomtone_Suburb-8bars.wav` -43.6, crackle -35.4; measured with ffmpeg astats), so the texture bed sat near -80 dBFS and the vocal loop at -43 dBFS RMS with everything else muted. Nothing in the SDK reports a sample's level at `add_sample_channel` time even though `fl.analysis.wav` could.
- Workaround: measure sample files before choosing channel volumes; use plugin gains for large boosts (PEQ 2 main level up to +18 dB, Pro-Q 4 output up to +36 dB) and keep channel volumes near 10000. Docstring for `set_channel_volume` should state the curve (or expose dB), and `add_sample_channel` could return peak/RMS.
- Evidence: this session; `records/phase-4-master.md`

### Full render died mid-way (FL render exit code 250477278), left a 94 s partial WAV with a drifting timeline; retry from the preserved snapshot succeeded
- Status: open
- Seen: 2026-09-14, Parking Lot Moon phase 4 (`fl_project_render(outputPath="Parking-Lot-Moon/Parking-Lot-Moon-v011.wav")`, first attempt)
- Call: same call that succeeded for v009 and v010 (108 bars, 259.2 s, 25 inserts of FabFilter/iZotope/Baby Audio/stock effects)
- Expected / actual: WAV or a clean error; actual "Render failed. Snapshot preserved at ...\5bce15ae...\Parking-Lot-Moon-v011.flp. FL render exited with code 250477278" after about 40 s, the session gone, and a 36 MB `Parking-Lot-Moon-v011.wav` (94.3 s) left at the output path. Its first 16 bars match the previous render but from about bar 16 the content runs ahead (chorus material at bar 24, 7 dB hotter) as if the renderer changed tempo or dropped buffers before dying. Because the partial file occupies the output path, an immediate retry would be refused ("must not exist"). `fl_project_start` from the preserved snapshot and the same render call then produced the full 99.5 MB file; the start result again showed `tempo 140` before `fl_status` read 100.
- Workaround: move the partial file aside (never delete evidence), resume from the preserved snapshot, render again. The tool should delete or rename a partial output on failure (or at least say it is there) and include FL's stderr / the last log lines in the error.
- Evidence: `Parking-Lot-Moon/scratch/v011-render-crash-partial.wav`; snapshots `5bce15ae2ca84133a2aa37cf6c223892` (failed) and `64d771eb4a9a48baa1ca1e90a9fe6ab6` (retry)

### Plugin shadow copies are never pruned: `%LocalAppData%\FruityLink\plugin-shadow` reached 81 GB
- Status: fixed-unverified (deployed 2026-09-14 from stage deploy-20260914-g, backup-installed-20260914-162830; the root was purged by hand (820 entries) before the deploy; live check = a "shadow: pruned N stale plugin copies" line in the plugin-host log after a later FL launch. Source: `ShadowCopyStore` now writes a `.owner` marker (pid + start time) into every copy and sweeps the root on host start, deleting copies whose owner process is gone; marker-less legacy copies are deleted only when none of their files is locked; 3 new tests in `ShadowCopyStoreTests`; needs a host redeploy)
- Seen: 2026-09-14, reported by the user during Parking Lot Moon phase 4
- Call: every `fl_project_start` / render / close cycle (the host shadow-copies `plugins\fl-agent` 281 MB, `fl-python-ide` 129 MB and `fl-mcp` 1 MB per FL launch)
- Expected / actual: copies removed when FL exits; actual 772 GUID folders (after the user had already deleted many) dated 2026-09-12..14, ~410 MB per launch. `ShadowCopyStore` only deleted a copy in `UnloadAlc`/`SafeUnload`, which never runs when FL is closed or killed by the MCP, and the constructor deliberately did not purge the root (multi-host safety), so nothing ever cleaned up.
- Workaround: with no FL process running, delete everything under `plugin-shadow` (they are caches; a live host recreates its own copies).
- Evidence: `ls plugin-shadow | wc -l` = 772; per-entry contents = the plugin folders; `du` of the installed plugin folders 281/129/1 MB

### Master-chain scales had to be probed: Pro-L 2 Gain = 30 v dB, Output Level = 30 v - 30 dBTP; Super VHS Output 80 % is about -8 dB
- Status: docs
- Seen: 2026-09-14, Parking Lot Moon phase 4 (mastering on insert 0)
- Call: `fl.mixer[0].effects[2].parameters.set_verified(0, 0.6)` on FabFilter Pro-L 2 -> "+18.00 dB" (0.5389 -> "+16.17 dB", so Gain is linear 0..+30 dB, not centred at 0.5); `set_verified(18, 0.9)` -> "-3.00 dBTP" (Output Level linear -30..0 dBTP); Super VHS (16/4) index 5 "Output" 80 % -> 100 % raised the whole texture bus by about 8 dB (reverse swell 250-4 kHz -29 -> -15 dBFS together with +6 dB of Pro-Q output), so 80 % is roughly -8 dB, not -2 dB
- Expected / actual: a documented mapping for the FabFilter wrapper parameters (the friction log's "FabFilter parameter map ... still needs the live set-then-read harvest" is still open); actual two probe writes per knob, one of which overshot the swell by 14 dB and cost a full render (v011). Pro-L 2 also exposes its meter settings as unnamed "param 19..31" and true-peak limiting / oversampling as "param 9/10" (On/Off) with no names, so they were left at defaults. Ozone 11 was not tried: its parameters are only reachable through the wrapper's names and, after the earlier unlicensed-plugin hang and today's renderer crash, a 2000-parameter plugin on the master of a chain that cannot be auditioned was judged not worth the risk (skipped, per the brief's "else skip").
- Workaround: calibrations above; Pro-Q 4 as in `scratch/p2helpers.py` (freq v = ln(Hz/10)/ln 3000, gain v = (dB+30)/60, Q v = ln(Q/0.025)/ln 1600, shape index/9, slope index/10, Output Level index 556 dB = 72 v - 36).
- Evidence: this session; `records/phase-4-master.md`

### Deleting a note shrinks every playlist clip of that pattern to the new pattern length
- Status: open
- Seen: 2026-09-14, Parking Lot Moon phase 5 (v014 revision: chopping the second vocal entry)
- Call: `fl.ops.delete_notes(pattern=38, targets=[{"channel": 18, "key": 60, "startTick": 1536}])` (and the same on pattern 39); the Vox Interlude / Vox Double clips on playlist tracks 10 / 12 were 3072 ticks long
- Expected / actual: only the note goes; actual `fl.clips.list()` afterwards shows both clips resized to 1536 ticks (the remaining note's extent), so the clip end moved from bar 57 to bar 53 without any clip call. Harmless here (the 8.57 s sample plays out inside 4 bars and the new chop clip starts at bar 53), but a clip that relied on the old length would silently lose its tail, and `delete_notes` does not report it.
- Workaround: re-list clips after any note delete on a pattern that is placed in the playlist and `fl.clips.resize` back if needed; the op docs should state that FL re-derives clip lengths from the pattern.
- Evidence: this session (clips 48 / 51 before 3072, after 1536)

### `AutomationTarget.plugin_parameter` takes `(track, parameter, *, slot=)`, not the record's `track/slot/param` order
- Status: docs
- Seen: 2026-09-14, Parking Lot Moon phase 5
- Call: `AutomationTarget.plugin_parameter(1, 0, 556)` written from the phase-3 record's "1/0/49" notation
- Expected / actual: a target for insert 1 slot 0 param 556; actual `TypeError: ... takes 3 positional arguments but 4 were given` (signature is `(channel_or_track, parameter, *, slot=-1)`, slot keyword-only). One wasted request; `fl.transport.seek` and `fl.playlist.tracks` were also guessed wrong in the same pass (the seek is `fl.ops.seek(tick=)` / `fl.transport.seek_ticks`, track names are `fl.ops.set_track_name`), none of which the docs index by task.
- Workaround: `AutomationTarget.plugin_parameter(1, 556, slot=0)`; `inspect.signature` before calling; records should quote the call, not a shorthand.
- Evidence: this session

### Parameter display read immediately after `seek` returns the previous position's automated value
- Status: docs / caller
- Seen: 2026-09-14, Parking Lot Moon phase 5 (verifying the new Pro-Q 4 Output Level automation on insert 1)
- Call: `fl.ops.seek(tick=...)` then `fl.mixer[1].effects[0].parameters.page(offset=556, limit=1)` in the same request, three seeks in a row (bars 5 / 8.5 / 20)
- Expected / actual: -12 / -8 / -4 dB; actual -4 / -4 / -8 dB, i.e. each read shows the value FL had applied for the *previous* seek (the automation is applied asynchronously after the playhead moves). With `time.sleep(0.3)` between the seek and the read every value was right (-12 / -8 / -4 / -4 / -12 at bars 5 / 8.5 / 20 / 40 / 3), and a second read 300 ms later agreed. Related to the phase-3 "seek readback drifts 14-20 ticks" entry but a different failure: the value is from the wrong position, not a nearby one.
- Workaround: sleep about 300 ms after `seek` before reading a display, and read twice; a `seek_and_read` helper (or an op that seeks, waits for the automation pass, then reads) would remove the guesswork.
- Evidence: this session (same request, with and without the sleep)

### A single long heredoc (about 40 lines, Markdown with apostrophes and backticks) fails to parse in the harness Bash tool
- Status: open (harness)
- Seen: 2026-09-14, Parking Lot Moon phase 5 (writing `records/phase-5-v014.md`)
- Call: `cat > records/phase-5-v014.md <<'EOF' ... EOF` (quoted delimiter, 60 lines of Markdown), once combined with a second python heredoc and once alone
- Expected / actual: the file; actual `/usr/bin/bash: -c: line 35: unexpected EOF while looking for matching ''` both times, although three shorter quoted heredocs with apostrophes appended to this log fine in the same session. Same family as "Multi-heredoc Bash command failed to parse in the harness" (Ember Tides), but it also hits a single heredoc once it is long enough.
- Workaround: write long files with the Write tool (or a Python script reading a file), keep Bash heredocs short.
- Evidence: this session (two failed attempts, Write tool succeeded)
