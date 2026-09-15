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
- Status: fixed-unverified (live PARTIAL 2026-09-14: check 11 -- the describe output is right (Sub Shape index 199 display "Sine" -> `meaning.value` "sine"; A WT Pos frame 1 of `S2 Tables/Analog/DM - OSCAR.wav`; `fx` lists Delay then Reverb) but `explain_parameters` returns `meaning.values` keyed by floats and the embedded worker refuses to serialise it: `TypeError: JSON object keys must be strings.` from `fruitylink/values.py` line 42; fix in progress for stage i; deployed 2026-09-14 from stage deploy-20260914-h, backup-installed-20260914-174218; partial: `fruitylink_serum.describe.explain_parameter(name, normalized, state=)`, `explain_parameters(fl, channel, filter=, state=)` and `fx_slot_names(state)` name Sub Shape values (0.25 -> roundrect), resolve A/B/C WT Pos to table + frame and list each rack's loaded units; the "FX Main Param n" proxy slots stay opaque because every preset stores `FXRack{n}/proxyParams = null`, documented in serum-support.md with the `SerumPatch.fx.*` -> `load_preset` route; the calibrations recorded here are in `data/parameter-scales.json`)
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
- Status: fixed-unverified (live PARTIAL 2026-09-14: check 21 -- the control ids read and write consistently on channel 8 (id 14 is stretch time: 1000 -> 1500 -> 1000, id 13 unmoved, `sample_offset` 0) but the checklist snippet's int-keyed result is refused by the worker (`TypeError: JSON object keys must be strings.`) and the GUI knob movement was not eyeballed; fix in progress for stage i; deployed 2026-09-14 from stage deploy-20260914-h, backup-installed-20260914-174218; partial: generic `get_channel_control(channel, control)` / `set_channel_control(channel, control, value)` on the REC_Chan command bus (same protocol as the verified volume 0 / pan 1 / pitch 4 / mute 7 / route 8; range 0..0x1FFF), Python `Channel.control(i)`, `set_control(i, v)`, `ChannelControl` enum, `Channel.stretch_time` (REC_Chan_StretchTime = 14) and `Channel.sample_offset` (REC_Chan_SmpOffset = 13) on FL-SDK ids that are NOT live-verified (raw FL ints); reverse, fades, trim/sample end and stretch mode are not REC events (no bridge path, Edison GUI-only): api.md "Sampler channel settings" keeps the `wave`-module trim/reverse + `replace_sample` recipe)
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
- Status: fixed-unverified (live FAIL 2026-09-14: check 15 -- `set_verified("Tempo sync", 1.0)` on a Fruity Delay 3 slot already On returned `{verified: false, unchanged: false, attempts: 6, display: "On"}`; the row reads `rawValue 1, normalized null`, so `normalized_from_raw` decodes the native integer as a float32 denormal and the unchanged path never fires; fix in progress for stage i; deployed 2026-09-14 from stage deploy-20260914-h, backup-installed-20260914-174218; `VerifiedWrite.unchanged: bool` (default False); a slot already at the value returns `verified=True, unchanged=True, attempts=1, display_changed=False` after one readback instead of burning every attempt)
- Seen: 2026-09-14, Parking Lot Moon phase 2
- Call: `parameters.set_verified(2, 1.0)` on Delay 3 "Tempo sync" (already On), "Output dry" (already 100%), Hyper Chorus "Modulation amount" (already 50%)
- Expected / actual: verified=True (value is in place); actual `verified=False, display_changed=False` because the raw value never changed, which reads like a failed write in a batch summary.
- Workaround: treat `normalized_after == value` as success; compare displays. A `already_set` flag on VerifiedWrite would remove the ambiguity.
- Evidence: this session

### No sidechain routing: Fruity Limiter has no sidechain-source parameter, Pro-C 3 "External" has no source
- Status: wontfix (FL's "Sidechain to this track" is a route flag that is not in the verified `FlMixerLayout` (send records hold only level int32 @0 and active byte @4; the RE notes a per-track table at +0x12A4 and an FX sub-table at +0x158, neither profiled), `FLmx_SetRouteActiveCore` has no sidechain argument and can raise a "Disable routing?" dialog, so a send always sums audio; documented in the `set_mixer_send` docstring and api.md "Verify sends and bus routing"; workaround: `fl.automation.pump(AutomationTarget.mixer_volume(insert), ...)` or `fl.automation.duck(...)` keyed to `fl.playlist.onsets(kick, ...)`, or flip the route in the GUI; RE lead: resolve the +0x12A4 table for 26.1.3.5570)
- Seen: 2026-09-14, Parking Lot Moon phase 2 (brief: Fruity Limiter COMP on the bass keyed from the Kick insert)
- Call: `fl.mixer[6].effects[2].parameters.page()` on Fruity Limiter (18 parameters: gain, sat, limiter, comp threshold/ratio/knee/attack/release/curve/RMS, noise gate; no sidechain input); `Pro-C 3` parameter 21 "Side Chain Input" accepts 0.25..0.34 -> "External" (0 Internal, 0.5 Host Sync, 1.0 MIDI); catalog has only `set_mixer_send(src, dst, level)` with no sidechain flag
- Expected / actual: a way to mark the 8 -> 6 route as a sidechain (FL right-click "Sidechain to this track") so the limiter/Pro-C 3 sees the kick; actual a plain send would sum the kick into the bass insert, and the wrapper's sidechain input stays silent, so Pro-C 3 External would never compress.
- Workaround: Fruity Limiter left in COMP mode on insert 6 with sidechain-ready settings (threshold -11.8 dB, 1:3.0, knee 40 %, attack 3.08 ms, release 163 ms) acting on the bass itself; Pro-C 3 removed; route 8 -> 6 created at level 0 as a placeholder. Phase 3 can emulate the duck with a mixer-volume automation clip on insert 6 keyed to the kick pattern, or the user can flip the route to sidechain in the GUI. An `set_mixer_send(..., sidechain=True)` op is the fix.
- Evidence: this session

### Mixer-volume dB curve is undocumented; gain staging done in plugin output gain instead
- Status: fixed-unverified (live FAIL 2026-09-14: check 45 -- isolated renders measure a 12.70 dB drop for mixer volume 12800 -> 6400 and 14.21 dB for 12800 -> 5769, against the model's 17.4 / 20.0 dB; solved exponent about 2.05-2.11, not `FADER_EXPONENT` 2.89; fix in progress for stage i; deployed 2026-09-14 from stage deploy-20260914-h, backup-installed-20260914-174218; new `fruitylink.levels` (`mixer_volume_to_db`/`mixer_volume_from_db`, `fader_db`/`fader_position`, `volume_table`, constants `MIXER_VOLUME_MAX=16000`, `MIXER_VOLUME_UNITY=12800`, `SEND_UNITY=0.8`, `FADER_EXPONENT` about 2.889; model dB = 20 * 2.889 * log10(position / 0.8) from FL's 0.8 = 0 dB / 1.0 = +5.6 dB anchors, every helper takes `exponent=`), `MixerTrack.volume_db` and `set_volume(value=None, *, db=None)`, api.md "Volume in decibels" with the raw/dB table; bridge `SetMixerVolumeAsync` now clamps at 16000 instead of 12800 (the +5.6 dB headroom was unreachable); the exponent needs the render calibration (Ember Tides 6800 -> 12800 measured +11.8 LU, model 15.9, square law 11.0); `set_master_volume` left on 0..12800)
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
- Status: fixed-unverified (live FAIL 2026-09-14: check 45 -- isolated renders measure a 12.54 dB drop for channel volume 12800 -> 6400 against the model's 17.4 dB; solved exponent about 2.08, not `FADER_EXPONENT` 2.89 (the readback half of check 17 passed: `volume_db = 0.0` -> raw 10240); fix in progress for stage i; deployed 2026-09-14 from stage deploy-20260914-h, backup-installed-20260914-174218; `fruitylink.levels.channel_volume_to_db`/`channel_volume_from_db` on the same fader model (`CHANNEL_VOLUME_MAX=12800`, `CHANNEL_VOLUME_UNITY=10240`, `CHANNEL_VOLUME_DEFAULT=10000`), `Channel.volume_db` and `set_volume(value=None, *, db=None)`, every volume op docstring carries the scale and a table; `fl.samples.describe(channel)` / `fruitylink.analysis.describe_samples(paths)` report a sample's peak/RMS before a channel volume is chosen; the exponent shares the mixer calibration caveat)
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

FL prerequisites (must be set by hand before any capture; the SDK now names both in the zero-file error):

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

### A solo armed mixer insert records nothing until a second arm-state change
- Status: fixed-unverified (SDK: `fruitylink.capture.Audio._arm_refresh` arms then disarms one non-requested insert after the requested set is armed, verifies both readbacks (`CaptureError` if FL refuses) and reports an `"Arm-refresh workaround applied: insert N (name) ..."` warning; `arm_refresh=False` opts out; a `"skipped"` warning says so when no candidate insert exists; modelled by the fake FL in `python/tests/test_capture.py`)
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
