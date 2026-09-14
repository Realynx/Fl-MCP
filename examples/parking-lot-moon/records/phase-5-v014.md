# Parking Lot Moon - phase 5 (producer revisions v014 / v015)

Date: 2026-09-14. Started from `Parking-Lot-Moon-v013-master.flp` (session `Parking-Lot-Moon-v014.flp`, tempo read back 100 at start, no tempo race this time). Master chain (Reeverb 2 / Pro-Q 4 / Pro-L 2 on insert 0) untouched. Nothing else musical was changed.

Producer notes on v013: (1) out-of-key note at a lead apex in the chorus; (2) two intro lead notes far too loud; (3) the second vocal entry could be chopped rhythmically. After v014: "octave-down shift is cool, use more sparsely; the original seems to keep playing like on delay; too much in the MIDI pattern" -> v015.

## A. Out-of-key apex note (found and fixed)

All chorus lead patterns were read with note names: Lead Chorus 1 / 2 (patterns 20 / 21, channel 1), Lead Oct Chorus 1 / 2 (24 / 25, channel 2), Lead Wash Chorus 1 / 2 (26 / 27, channel 3). Every other note is inside F# natural minor. The offender is the apex of the phase-1 "third-shift" variant (E5 C#5 A4 B4 **G5**): key 79 = **G natural**, not in the key, landing on the **Dmaj7** bar (D F# A C#) - pattern bar 10 = **song bar 42** (chorus 1, tick 3458 in pattern 20) and **song bar 90** (chorus 2, tick 3553 in pattern 21), a 288-tick dotted half at velocity 103-104, i.e. the loudest and highest note of the chorus. Doubled as **G6** (91) in Lead Oct (patterns 24 / 25, ticks 3458 / 3553) and **G5** in Lead Wash (26 / 27, ticks 3482 / 3577, +24 ticks late by design).

Fix: moved each to the nearest chord-friendly scale tone that keeps the shape (B4 leaping up to the apex): **F#5 (78)** / **F#6 (90)**, the major third of Dmaj7 and the tonic of the key. Six notes changed (`edit_notes`, one per pattern), nothing else; re-read confirmed no key 79 / 91 remains in those patterns and the note counts are unchanged (22 / 23 / 16 / 18 / 22 / 23).

Noted, not changed: chorus 1 bar 48 holds E5 (from bar 47.25) into the C#7(b9) half bar, against the pad's E#. It is the phrase's sustained tail, not an apex, and reads as the intended tension; flagged here for the producer.

## B. Intro lead fragment (bar 7)

Only the Lead clip (pattern 17 "Lead Intro", channel 1) plays in bars 1-8; Lead Oct / Lead Wash have no clip before bar 33. Fragment = C#5 (tick 2302, bar 7.0) and A4 (tick 2449, bar 7.4).

- Velocities C#5 82 -> **60**, A4 73 -> **52** (verse fragments are 79-92).
- New automation clip, channel 45 "PLM - Lead output trim (insert 1)" on playlist track 29 "Auto Lead Trim", target insert 1 slot 0 (Pro-Q 4) **Output Level** (index 556, dB = 72 v - 36; static value was -4 dB): **-12 dB (v 0.3333) from bar 1 to bar 8, linear to -4 dB (v 0.4444) at bar 9**, held to the end. Read back after seeking (300 ms wait): -12.00 dB at bars 3 / 5, -8.00 dB at bar 8.5, -4.00 dB at bars 20 / 40.
- Measured (master, mono RMS per bar): bar 7 v013 -19.2 dBFS (+9.2 dB above bar 6) -> v014 -25.9 / v015 -25.4 (**-6.2 to -6.7 dB**, now +2.5 dB above the pad bed). Bars 1-6 and 8-12 unchanged within 0.1 dB. The Serum patch's velocity response is evidently shallow: the velocity cut contributed little, the gain automation did the work.

## C. Chopped second vocal entry

Vocal placement: one Vox clip (pattern 38 "Vox Interlude", channel 18 -> insert 17) at bars 49-56 with two triggers of the 8.57 s loop, bar 49 and **bar 53** (the second entry; the Love Philter sweep opens 49-53 so the second entry is the unfiltered one); Vox Double (pattern 39, channel 20 -> insert 22) identical.

- Slices: `Samples/Vox_E8_everywhere_112_Gbm-slice-01..08.wav` (24-bit, 44.1 k), cut on the 112 BPM beat grid / detected onsets (0.000-0.536, 0.536-1.071, 2.190-2.680, 2.680-3.214, 4.004-4.554, 4.554-5.089, 5.508-6.128, 6.128-6.696 s), boundaries snapped to zero crossings within 10 ms, 5 ms fades, 0.49-0.62 s each, RMS -12 to -19 dBFS.
- Channels 37-44 "Vox Chop 1..8" (Sampler, routed to insert 17 Vox so the vocal chain, Verb Long and Delay Send apply), volume 10000 (v014) -> **9000** (v015).
- Pattern 41 "Vox Chop" (4 bars) on track 10 at bar 53 (tick 19968, 1536). Pitch by note key: 60 unison, **48 = -12**, 65 = +5.
- Removed: the bar-53 note (tick 1536) from pattern 38 and from pattern 39 (Vox Double) - the only original-sample triggers of the second entry. FL then shortened the two first-entry clips (playlist clips 48 / 51, tracks 12 / 10) from 3072 to 1536 ticks by itself (bars 49-53; the 8.57 s sample plays out at bar 52.6 either way). No chopped double was added (it would need eight more channels on insert 22; the Vox Double keeps the first entry).
- v014 figure: 27 hits (7 / 6 / 7 / 7 per bar; dotted-eighth / eighth mix, one -12 and one +5 per 2 bars, velocities 70-106). Too dense and 6-9 dB louder than the old entry (bars 53-56 -12.2 / -13.0 / -12.1 / -13.8 dBFS vs v013 -19.1 / -18.7 / -19.9 / -22.9).
- Verification that nothing original remains under the chop: `scratch/v015-interlude-chopmuted-49-56.wav` (bars 49-56 + 8 beats tail, chop channels muted, everything else live): bars 53-56 at -21.7 / -22.1 / -24.5 / -24.5 dBFS with 1-4 kHz at -37 to -41 dB, i.e. the pad / bass bed only (v013 with the long vocal: 1-4 kHz -31 dB). The playlist holds no vocal clip in bars 53-56 other than the chop (tracks 10 / 11 / 12 listed). The "original playing over like a delay" was the v014 slices themselves (0.55 s each, retriggered every eighth, so two always overlapped) plus their delay send.
- v015 figure (17 hits, 4 / 4 / 4 / 5 per bar, rests on every off-beat, 2-bar figure A-B repeated, bar 4 varied): A: S1 (beat 1) S2 (1.75) **S3 -12** (3) S4 (4); B: S5 (1) S6 (2) S7 (3) **S8 -12** (3.75); A again; D: **S5 -12** (1) S6 (1.75) S7 +5 (2.25) **S8 -12** (3) S1 (4). Five of 17 hits are octave-down, on beats 1 / 3 or phrase ends, never adjacent. Velocities 62-92. Slice order 1-8 kept so the phrase stays recognisable.
- v015 measured: bars 53-56 -16.7 / -17.1 / -16.5 / -16.0 dBFS (+2.4 / +1.6 / +3.4 / +6.9 dB vs v013; bar 56 carries two octave-down hits and was the old entry's quiet tail). 1-4 kHz -31.7 / -31.1 / -31.3 / -24.5 dB (v013: -30.9 / -31.5 / -32.7 / -40.4). Bar 57 (verse 2 downbeat) -17.2 vs -17.3.

## D. Renders, measurements, deliverables

| Render | Integrated | True peak | LRA | Bars differing from v013 by >= 0.5 dB |
|---|---|---|---|---|
| v013-master (reference) | -13.9 LUFS | -0.99 dBTP | 8.3 LU | - |
| `Parking-Lot-Moon-v014-master.wav` | -13.6 LUFS | -1.0 dBTP | 9.0 LU | 7 (-6.7), 53-56 (+5.7..+9.1), 57 (+1.9), 106 (-0.6) |
| **`Parking-Lot-Moon-v015-master.wav`** (final) | **-13.9 LUFS** | **-1.0 dBTP** | 8.8 LU | 7 (-6.2), 53-56 (+1.6..+6.9); all other bars within 0.48 dB (bar 90 -0.44 = the apex change) |

All 48 kHz float, 259.2 s, 2.68 s of silence after the last -60 dBFS sample. Per-bar numbers: `records/verification-v014-master.json`, `records/verification-v015-master.json` (mono-RMS per bar plus ffmpeg ebur128; the v013 reference re-measured the same way is in `scratch/v013-measure.json`).

Deliverables (320 kbps CBR MP3, cut by time at 2.4 s/bar, 10 ms in / 300 ms out fades on previews; v013 files untouched):
- `Parking-Lot-Moon-v015-final-master.mp3` (259.2 s) - the current final; `Parking-Lot-Moon-v014-final-master.mp3` kept for A/B.
- `Parking-Lot-Moon-v015-chorus1-preview.mp3` (bars 33-48, 76.8-115.2 s), `-v015-intro-preview.mp3` (bars 1-12, 0-28.8 s), `-v015-vocal-chop-preview.mp3` (bars 49-57, 115.2-136.8 s: first entry, chop, verse 2 downbeat); the same three exist for v014.

Sessions and snapshots: v014 (from v013-master) -> full render -> `snapshots/893f95ca08184b849d1bf62a637984bd/Parking-Lot-Moon-v014-master.flp` (copied to `Parking-Lot-Moon-v014-master.flp`) -> v015 (chops muted for the check) -> section render -> `snapshots/afa4db12c1ae476f999dd39a5b9219ef/v015-interlude-chopmuted-49-56-full.flp` -> v015a (unmuted, pattern 41 rewritten, chop volume 9000) -> full render -> **`C:\Users\poofi\AppData\Local\FlMcp\Projects\snapshots\834c9bb2c0784c6f8ec7210fed8ecf20\Parking-Lot-Moon-v015-master.flp`** (final state; copied to `Parking-Lot-Moon\Parking-Lot-Moon-v015-master.flp`). Session closed. Resume with `fl_project_start(projectPath="Parking-Lot-Moon/Parking-Lot-Moon-v016.flp", sourceProjectPath="Parking-Lot-Moon/Parking-Lot-Moon-v015-master.flp")`.

Totals after v015: 46 channels (21 instruments / samples + 8 chop samplers + 17 automation), 41 patterns, 1213 notes, 97 clips (v013: 37 / 40 / 1198 / 95).

## Open for the producer
- Bar 56 (last chop bar) is 6.9 dB above the old tail because of its two octave-down hits; if it is too much, drop the beat-1 octave hit (channel 41, key 48, tick 1152 in pattern 41).
- The chop still feeds Verb Long (0.3) and Delay Send (0.25) like the original vocal; the Delay Send return is automated to unity through the interlude, so each hit gets filtered repeats.
- Chorus 1 bar 48: E5 over the C#7(b9) half bar (see A).

Friction logged this phase (4 entries under "2026-09-14 - Parking Lot Moon"): note delete shrinks clips; `AutomationTarget.plugin_parameter` argument order; seek readback lags one seek without a wait; a long single-heredoc Bash write failed to parse in the harness (record written with the Write tool instead).
