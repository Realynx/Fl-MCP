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
- Evidence: examples/ember-tides/issues-v006.md #1, live-validation-2026-09-13.md

### No way to delete time markers; markers extend renders
- Status: fixed (delete_marker live-verified; with markers deleted and one 8-bar clip at tick 0 the render was 13.71 s instead of 178.3 s)
- Evidence: issues-v006.md #2

### Installed wheel / API catalog / Python names disagree
- Status: fixed-unverified for naming annotations; wheel redeployed. `select_channel` and
  `set_channel_solo` still take `index=` while other channel ops take `channel=`.
- Evidence: issues-v006.md #3

### Automation endpoints protected from deletion
- Status: fixed (set_point live-verified)

### Parameter display readback lags rapid writes in one request
- Status: open (no host fix found); `Parameters.set_verified` added as mitigation.
  Still observed after preset loads: the display is reliable in the next request only.
- Evidence: issues-v006.md #5, live-validation-2026-09-13.md

### `query_plugin_parameters` filter appeared to ignore spaces
- Status: caller (limit bounds the scanned slots from offset). Docs updated; `Parameters.read(index)` added.

### `fl_project_start` schema lacked `background`; title "(untitled)"
- Status: fixed-unverified for background (server rebuilt; the tool schema shown to the client
  did not include it until the client reconnected). Title: fixed (live).

### Permission classifier denied read-only `fl_python_api(filter="marker")`
- Status: open (harness). Workaround: read the SDK source.

### Oversized Python results and exceptions discarding partial results
- Status: open (docs only). Write large dumps to disk inside FL; wrap steps in try/except.

### Serum preset loading: default class id ignored silently
- Status: fixed (deployed; shipped load_preset on "LD - Analog Glow" read back WT Pos 57 / 87% / detune 0.01). Working id is the GUID string order
  `56534558667350736572756D20320000`; memory order is ignored without error.
- Evidence: live-validation-2026-09-13.md

### Serum factory presets need processor-state normalisation
- Status: fixed (deployed and live-verified through load_preset; minimal identity subset still not isolated). Drop UI-only sections, add
  component/product/productVersion/version.

### Synthesized `.fst` did not load; raw `.SerumPreset` ignored
- Status: wontfix for synthesized .fst (documented unsupported; FL-saved .fst untested); raw .SerumPreset is converted automatically by load_preset.

### `use_channel_loader=True` renames the channel and can start playback
- Status: fixed-unverified (deployed; refusal path not exercised live). Dispatcher route is the default.

### `load_state` verification line always says "0/12 sampled values changed"
- Status: open (deployed, but the line still reported no change after loads that did change state; the sampled indices miss Serum's live parameters; consider comparing the plugin state record instead).

### No render range / selection render; audition of one clip costs a full-length render
- Status: open. Markers can now be deleted, but clips and automation still set the length;
  a typed render range or stem/selection render would make auditions cheap.

### Plugin waveform/wavetable identity not readable through parameters
- Status: open. `A WT Pos` reads a frame number only; preset loading now gives a path to
  known sounds, but reading which table is loaded is still not possible.

### Enum/scale mappings undiscoverable (unison count, detune, sub shape, filter cutoff)
- Status: open (recorded mappings in issues-v006.md #7; a probe helper or catalog metadata
  would avoid rediscovery).

### `PresetFile` has no `name` attribute; `vars()` fails on slotted records
- Status: open (minor ergonomics: expose `name` and a `to_dict()` on extension records).

### Duplicate-note addressing (channel/key/tick) can hit several notes
- Status: open since v003 (issues history in examples/ember-tides/README.md).

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
- Status: in-progress (data files in extensions/serum-support/data; live harvest pending)
- Uncertain: kParamTablePos frame rule (one live point: 88.29 on a 166-frame table showed WT Pos 57);
  filter kParamType display names beyond MG Low 12 are pattern guesses; osc A/B/C kParamOctave FL
  mapping; synced LFO rate units; kRoundRect FL position; Default Shapes frame 5 may be a wide pulse.
- Fix path: with read-state now available, run a set-then-read harvest over FL's 685 named Serum
  params on a disposable project and stamp confidence=verified on every row it resolves.

### No native plugin "get state" opcode known; read-state goes through a temp project copy
- Status: fixed (live: Channel.get_state returned the 13,663-byte record; fruitylink_serum.read_state and snapshot_preset round-tripped).
  Slower than a native getChunk and depends on note validation passing; acceptable for now.

### `read_channel_state` (snapshot reader) crashes: 'ProjectInfo' object is not callable
- Status: open (extension bug, found live 2026-09-13). `fl.project.info` is a property, the reader calls it.
  Native `fl.channels[n].get_state()` and `fruitylink_serum.read_state` worked (13,663-byte record).
- Fix path: use `fl.project.info` without parentheses; add a test with a stub Studio whose info is a property.

### Builder round trip verified live (square lead + reverb + delay)
- Status: fixed (live). `SerumPatch(...).load(fl, 4)` then readbacks: WT Pos 4 (square frame), unison 4,
  detune 0.20, width 60, B +1 oct 30%, sub Triangle -1 oct 50%, MG Low 24 at 500 Hz / 20%, Env 50 ms / 400 ms,
  Macro 1 = 40, main -9 dB; plugin state shows FXRack0 = [Reverb, Delay]. Audition rendered: 13.71 s, L/R RMS -28.66/-28.82 dBFS, peak -15.6 dBFS (Ember-Tides/audition-v007-builder-square-lead.wav/.mp3).

## 2026-09-13 - Ember Tides v007 (first revision using preset tools)

### Loaded presets can silently bypass existing FL automation targets
- Status: open (needs a guard or a warning)
- Seen: v007 draft; "KY - Smart Future" on channel 1
- Expected / actual: the song automates Serum Filter 1 Freq on that channel; the preset's oscillators have direct level 0 and feed FX buses, so the voice filter is out of the path and the automation did nothing audible (intro centroid 7.9 kHz vs 2.6 kHz).
- Workaround: keep the original patch where automation matters and add the new preset as a separate channel/layer.
- Fix path: load_preset could list FL automation links on the target channel and report which state keys they drive; the schema could flag presets whose osc direct levels are 0 or whose bus routing is active.

### Raw plugin-state hash is not a persistence oracle
- Status: caller/docs. Channel.get_state() bytes differ after reopen on untouched channels; decoded parameter diffs show only float noise. Compare decoded states (as done in state-before/reopened-v007.json).

### Multi-candidate audition in one render works but needed manual scaffolding
- Status: open (worth a helper). Pattern: add one Serum channel per candidate, route to the target mixer track, copy the source pattern's notes to a new pattern, place blocks back to back, delete markers, render once. A fruitylink_serum.audition_candidates(fl, source_pattern, mixer_track, candidates, accompaniment) helper would make it one call.

### Playlist track numbering collision
- Status: caller. Placed stack clips on track 15, which already held an automation clip; moved them to track 20 and named it. A "first free track" helper would avoid this.

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
- Status: open (same helper request as v007): channel per candidate, notes copied, blocks placed, markers deleted, one render.

### Per-section measurement is manual ffmpeg scripting
- Status: open (worth a helper). A `describe_sections(wav, markers_or_bars)` returning LUFS/RMS/centroid/width per named section would replace ad-hoc shell loops used in every revision.

## 2026-09-13 - Ember Tides v009

### add_mixer_effect plugin name must match the plugin database file name
- Status: docs. "FabFilter Pro-R 2" failed, "Pro-R 2" loaded. list_available_plugins gives the exact names; load_preset-style fuzzy resolution would help.

### No band-energy check before committing a new voice
- Status: open (helper). The harsh arps would have been caught by comparing 2-4 kHz and >5 kHz band RMS of the chorus against the previous revision before rendering the full song. Fold band energy into the per-section measurement helper requested in v008.

## 2026-09-14 - Ember Tides v011

### Preset load resets global plugin parameters written just before it
- Status: docs/guard. set_plugin_param on Serum "Bend Down" was undone by a load_preset in the same script; the value had to be baked into the preset (kParamBendRangeDn). load_preset should warn that it replaces the whole state, and the builder could expose bend_range().

### Automation clip point times are relative to the clip start
- Status: docs. fl.automation.create(...) at bar 105 then set_points with beats 0..40 mapped correctly to bars 105-115; earlier song-start clips made this look absolute.

### Pitch bend via Serum's Pitch Bend parameter works; channel pitch range is not exposed
- Status: docs. Automating plugin parameter 7 gave a clean two-octave glide (verified by f0 tracking). FL channel pitch automation would need the channel's pitch range knob, which the SDK does not expose.

### Verifying a time-varying effect needs an isolated render plus a pitch track
- Status: open (helper). Centroid on the full mix could not show the bend; a stripped project and a pure-Python autocorrelation did. A describe_pitch_track(wav, start, end) helper in the analysis package would make this one call.

## 2026-09-14 - Ember Tides v012

### Channel-volume automation scale
- Status: docs. AutomationTarget.channel_volume values 0..1 map to the 0..12800 channel scale (0.55 -> 7040 read back). Document alongside the mixer/channel pan scales.

### No masking/clash check between two instruments
- Status: open (helper). Deciding how much to duck the pad under the lead was done by ear-proxy (band RMS in 500-1600 Hz before/after). A per-instrument band-overlap report from two isolated renders would make this measurable in one call.

## 2026-09-14 - Ember Tides v013

### Finding the empty spaces is manual
- Status: open (helper). Choosing where to add answering figures meant reading each pattern's note ticks by hand. A `gaps(pattern_or_section, channel=None, min_beats=1)` helper returning rest regions per channel would make "compose into the negative space" a query.

## 2026-09-14 - Ember Tides v014/v015 (details, mix, master)

### Limiter gain parameter scale had to be probed
- Status: docs. Pro-L 2 "Gain" normalized 0..1 = 0..30 dB (0.5 -> +15, 0.6 -> +18, 0.1 -> +3). Same-request readback after a write showed the old value; the next request was correct. A per-plugin scale table (like the Serum parameter map) would remove this probing for FabFilter plugins.

### Mastering EQ skipped for lack of a cheap parameter map
- Status: open. Pro-Q 4 is installed but its band parameters would need probing; a FabFilter parameter-map generator (same set-then-read loop as Serum) is the fix.

### End-of-song silence via a marker
- Status: docs (works). An "End" marker past the last note extends the render; useful, and worth a `set_song_end(bar)` helper so it does not rely on marker behaviour.

### Sidechain pump needed 194 automation points by hand
- Status: open (helper). A `pump(target, bars, depth, recovery_beats)` helper generating the per-beat curve would make this one call.

## 2026-09-14 - Ember Tides v016/v017

### Velocity does not scale some Serum patches
- Status: docs. The sine-sub patch played at full level regardless of note velocity (velocity-to-amp modulation absent), so a "soft" velocity-60 swell was as loud as the drop sub. The builder should expose velocity sensitivity (or the schema flag its absence) so level intent is not silently ignored.

### Per-bar anomaly scan should be a helper
- Status: open (helper, high value). A pure-Python scan (per-bar RMS in full, >4 kHz and <90 Hz bands with >6 dB step flags) located every reported problem in one pass. Add it to the analysis package as `scan_bars(wav, bpm, ...)` and run it before every master.

### Loudness target belongs in the mastering step's inputs
- Status: docs. The first master aimed at -10 LUFS by genre habit; the user wanted -14 (Spotify). Record the target in the project README and make the limiter helper take `target_lufs`.

## 2026-09-14 - Ember Tides v018

### Transition effects need an A/B transition measurement
- Status: docs. Comparing the last beat before the drop and the first beat of the drop between two renders (0.43 s windows) confirmed the fix numerically; fold a `transition(wav_a, wav_b, bar)` comparison into the analysis helpers alongside the per-bar scan.
