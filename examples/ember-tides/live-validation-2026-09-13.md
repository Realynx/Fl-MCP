# Live validation of the v006 fixes and Serum preset loading (2026-09-13)

Deployed build: SDK, MCP server, Python wheel and serum-support extension rebuilt from the
uncommitted trees and copied into `C:\Program Files\Image-Line\FL Studio 2026\FruityLink`
(backup: scratchpad `backup-installed-20260913-212608`). FL 26.1.3.5570. Disposable copies
of `Ember-Tides-v006.flp` only; the song file was not modified.

## SDK fixes

| Fix | Call | Result |
| --- | --- | --- |
| Signed mixer pan | `set_mixer_pan(track=4, value=-3200)` then `get_mixer_pan` | -3200 read back in the same and the next request, and after save/close/reopen. Audio proof for the left side was not rendered; the right side (6800) was proven earlier. |
| Endpoint automation edit | `fl.automation[13].set_point(18, 0.5)` | last point 0.32 → 0.5, 19 points before and after, persisted after reopen |
| Verified parameter write | `fl.channels[1].parameters.set_verified(199, 0.8)` | verified on first attempt; display read "Square" in the next request (display in the same request still said "Saw", so the display lag remains) |
| Marker list/delete | `fl.transport.markers()`, `delete_marker("Outro - afterglow")` | 9 → 8 markers, remaining names/ticks intact, persisted after reopen; no FL error. Play range stayed at 112 bars because clips still reach bar 113. |
| Session title | `fl_project_start` | reports `Title: session-v007-validate (file name)` |
| Catalog | `fl_python_api` | not re-checked in this pass |

## Serum 2 preset loading (channel 4, dispatcher route, `fl.channels[4].load_state(path)`)

| File | State changed? | Note |
| --- | --- | --- |
| project-derived `.vstpreset`, class id `58455356736673506572756D20320000` (memory order, extension default) | no | silently ignored |
| same state, class id `56534558667350736572756D20320000` (GUID string order) | **yes** | chord patch applied: A Level 75%, unison 7, Sub Saw |
| generated `.SerumPreset` (unison 3, detune 0.35) → `.vstpreset`, string-order id, no controller chunk | **yes** | read back unison 3, detune 0.35 |
| factory `LD - Analog Glow` converted as-is (string-order id) | no | |
| factory state filtered to processor sections only | no | |
| factory state filtered + `component=processor`, `product`, `productVersion=2.1.4`, `version=10.0` copied from a processor state | **yes** | A Level 87%, detune 0.01, blend 70, WT pos 57, sub off, Env1 15 ms / 2.46 s; persisted after save/close/reopen |
| synthesized `serum-ch1-state.fst` | no | |
| raw `.SerumPreset` via dispatcher | no | |
| any file via `use_channel_loader=True` | no | renamed the channel to the file's base name; FL transport was found playing afterwards |

The `load_state` verification line always reported "0/12 sampled parameter values changed";
that sample is not informative for Serum and is being fixed. Display readbacks after a load
are reliable in the following request, not always in the same one.

## Follow-ups handed to the SDK agent

- Default class id → string order; `class_id_from_guid` semantics; `load_preset` default.
- Factory-file normalisation step (section filter + processor identity fields) in the extension.
- Document/disable the channel-loader route side effects; fix the changed-values sample.

## Patch-builder batch (same day, third deploy)

Disposable copy of v006, channel 4. `SerumPatch(name="gate-square-lead").osc_a("square", unison=4,
detune=0.2, width=60).osc_b("saw", octave=1, level=0.3).sub("triangle", octave=-1, level=0.5)
.filter("lp24", cutoff_hz=500, resonance=20).amp_env(attack_ms=50, release_ms=400).macro(0, 40)
.fx.reverb(mix=25, type="hall", size=50).fx.delay(mix=10, time_ms=250, feedback=30)
.master_volume(db=-9).load(fl, 4)` produced 25 state changes. Next-request displays: A WT Pos 4,
unison 4, detune 0.20, width 60, B On +1 oct 30%, Sub Triangle -1 oct 50%, MG Low 24, 500 Hz, 20 %,
50 ms, 400 ms, Macro 1 40, Main 50% [-9.0 dB]. `read_state` showed FXRack0 = [type 6 Reverb, type 4 Delay].
`snapshot_preset` wrote a 1,631-byte .SerumPreset that reloaded. `load_preset(fl, 4, "LD - Analog Glow")`
(shipped normalisation) read back WT Pos 57, 87%, detune 0.01, sub off, Env1 15 ms. `Channel.get_state()`
returned 13,663 bytes. With all markers deleted and the pattern-42 clip moved to tick 0, the render was
13.714 s: `audition-v007-builder-square-lead.wav` L/R RMS -28.66/-28.82 dBFS, peak -15.6 dBFS,
centroid 2.5 kHz. Bug found: `read_channel_state` (snapshot reader) calls `fl.project.info` as a method.
