# Parking Lot Moon - phase 4 (verification, fixes, master, deliverables)

Date: 2026-09-14. Started from the phase-3 session on `Parking-Lot-Moon-v009.flp`. Every level below is measured from a render (ffmpeg `ebur128` for LUFS / true peak, numpy band RMS on the mono sum, `fruitylink.analysis.scan_bars` / `describe_sections` for the JSON files). Nothing was heard; all judgements are by numbers against BRIEF.md and the taste rules.

Full renders (48 kHz float, 259.2 s = 108 bars): `Parking-Lot-Moon-v009.wav`, `-v010.wav`, `-v011.wav`, `-v012-master.wav`, `-v013-master.wav` (final). Section / stem renders are in `scratch/`. Verification JSON: `records/verification-v009.json`, `records/verification-v013-master.json` (each has an `authoritative` block with the ffmpeg numbers; the helper's own `true_peak_dbtp` is a crude estimate).

Workspace `vNNN.flp` files are the state at the *start* of each session (written by `fl_project_start`); the state at the end of each render is the snapshot the render returned (listed per revision). The final master session is also copied to `Parking-Lot-Moon-v013-master.flp` in the project folder.

## Measurements per revision

| Revision | Session / snapshot | Integrated | True peak | LRA | Chorus RMS | Verse RMS | Interlude RMS | Intro RMS | Notes |
|---|---|---|---|---|---|---|---|---|---|
| v009 (phase-3 mix, unheard) | v009 / `snapshots/b47814b98558493c9ffc3049372ba02d/Parking-Lot-Moon-v009.flp` | -22.8 LUFS | -5.1 dBTP | 9.3 LU | -22.5 dBFS | -24.5 | -31.5 | -36.7 (bars 1-6 at -50) | pad inaudible, textures at about -80 dBFS, vocal loop at -43 dBFS, centroid 295 Hz in the chorus, chorus corr 0.76 |
| v010 (mix fix 1) | v010c / `snapshots/1fa839226c424b06a94e405ce7491ba4/Parking-Lot-Moon-v010.flp` | -21.0 LUFS | -4.1 dBTP | 8.5 LU | -22.2 | -24.9 | -29.5 | -32.6 | pads present (chorus 250-1 k -32 -> -26 dB), chorus corr 0.40, 2-4 kHz only +1.2 dB |
| v011 (mix fix 2) | v011b / `snapshots/64d771eb4a9a48baa1ca1e90a9fe6ab6/Parking-Lot-Moon-v011.flp` | -20.2 LUFS | -0.5 dBTP (swells) | 11.1 LU | -22.7 (chorus 1 peak -5.1 dBFS) | -25.4 | -29.5 | -33.1 | reverse swells overshot to -17 dBFS RMS (bars 31-32 / 79-80 / 95-96); vocal still masked; first render attempt crashed (see friction log) |
| v012-master (master pass 1) | v012 / `snapshots/0b8fdabd35764156ad512abdf36855f9/Parking-Lot-Moon-v012-master.flp` | -15.5 LUFS | -1.0 dBTP | 8.4 LU | -17.0 / -16.6 | -19.7 | -18.4 | -27.5 | limiter +7.0 dB: 1.5 LU short; vocal +10 dB made the interlude louder than the verse |
| **v013-master (final)** | v013 / `snapshots/2919e3ad82cc4d1ba9d37d635fafae93/Parking-Lot-Moon-v013-master.flp` | **-13.9 LUFS** | **-0.99 dBTP** (4x soxr; ebur128 -1.0) | 8.3 LU | -15.3 / -14.8 | -17.9 | -20.1 | -25.7 | sample peak -1.00 dBFS, 563 of 12.4 M samples at the ceiling; max short-term -10.2 (chorus 1), -9.6 (chorus 2); max momentary -9.0 / -8.0 |

Pre-limiter peaks: v011 chorus 1 peak -5.1 dBFS, verse -5.8, pre -1.8 (swell); after the v012 swell trim (-12 dB) the pre-limiter peaks sit about -5 dBFS in the choruses (no separate unmastered render of v012/v013 was made).

## What the v009 numbers showed and what was changed

1. **The GMS pad was silent.** Bars 1-6 (pad + textures only) sat at -50 dBFS with the pad's harmonics at -70 dB; shortening the amp attack (0.50 -> 0.10) changed nothing (`scratch/v010-intro-attack010.wav`). GMS's oscillator waveforms are GUI-only "Synth Waves" files that the parameter list never exposes, so the parameter-authored instance had nothing to play. Fix: `load_channel_plugin_state(channel=4, path=".../GMS/Pads & Textures/Smooth & Warm TE.gmsynth")` (state changed 226 bytes; unison 10, amp level +4.5 dB), then Filter Cutoff 0.42 -> 0.55; insert 4 Pro-Q 4 Output -8 -> 0 -> -3 -> **-4 dB** (final). Intro 150-400 Hz band -58 -> -40 dB (`scratch/v010a-intro-gmspreset.wav`).
2. **Pad Serum was practically inaudible** (Filter 1 MG Low 12 at 146 Hz with every oscillator -1 oct, under a 150 Hz 24 dB/oct high-pass). Fix: Filter 1 Freq 146 -> **1390 Hz** (the automated Pro-Q high cut now sets its brightness), Main Vol 49 % (-9.4 dB) -> **70 % (-3.2 dB)**, insert 5 Pro-Q Output -8 -> **-7 dB**.
3. **Textures were about -80 dBFS.** The Splice files are very quiet (hiss RMS -53.6 dBFS, roomtone -43.6, crackle -35.4) and FL's channel-volume scale is a power curve (raw 3200 is about -29 dB, not -12). Fix: channel volumes crackle 3200 -> **6400**, hiss 2800 -> **12800**, roomtone 3600 -> **10000**; insert 19 (hiss) PEQ 2 main **+13 dB**, insert 20 (roomtone) **+8 dB**; Textures bus PEQ 2 main -12 -> **-1 dB**; Super VHS Output 80 -> **100 %** (worth about +8 dB). Intro 2-8 kHz band -75 -> -50 dB in the master: the hiss/crackle bed is now subtle but present; no fizz above 8 kHz (>8 k band -52 dB in the intro, the bus low-pass at 8 kHz holds).
4. **Reversed swells** (channel 19 -> insert 21 -> Textures): Pro-Q Output -8 -> 0 -> +6 (v011, far too loud) -> **-6 dB** (final). Final 250-4 kHz half-bar levels: bars 29-30 about -25 dBFS, bar 31.5-32.5 -19.6 / -18.8, bar 33 downbeat -17.9 (same shape at 79-81, and 95-96.5 into the outro drop). Clearly audible, no longer a peak hazard.
5. **Vocal loop** (channel 18 -> insert 17): channel volume 5000 (about -18 dB) -> **10000**, Pro-Q Output -8 -> -2 -> +8 (too loud, interlude -18.4 dBFS > verse) -> **+2 dB**, Pro-C 3 threshold -20 -> **-14 dB**. Vox Double: channel 3600 -> **6500**, Pro-Q -12 -> **-8 dB**. Final interlude: -20.1 dBFS RMS (2.2 dB under the verse), 1-2 kHz rises from -38 dB (bars 49-52, Love Philter closed and opening) to -31 dB (bars 53-55, open): the loop is audible and the filter sweep reads. It still runs at 112 BPM against the 100 BPM grid (no stretch op); it is a drum-less section so it reads as texture, harmonic clash was not assessed by ear.
6. **Balance / headroom**: sub Pro-Q Output -6 -> **-9 dB**, bass -4 -> **-6 dB**, lead -6 -> **-4 dB**, kick 0 -> **-3 dB**. Chorus bands (v011): <90 -31.9, 90-250 -26.5, 250-1 k -27.0, 1-2 k -35.7, 2-4 k -40.1, 4-8 k -42.7 dB (v009: -27.8 / -26.1 / -31.6 / -41 / -41 / -42): pads and lead carry the chorus now instead of bass + kick.
7. Nothing was muted or removed; no notes, patterns, clips or automation were changed. Bass duck, drum filter, pad LP, delay-send and master-fade clips all measured as intended (see checks).

## Checks against the brief / taste rules (final master unless noted)

- Integrated -13.9 LUFS (target -14 +/- 0.5, ceiling -13.5): pass. True peak -0.99 dBTP (ceiling -1): pass. LRA 8.3 LU.
- Peaks before limiting about -5 dBFS in the choruses (v011): pass. Limiting is gentle: 563 samples touch the ceiling (chorus downbeats), gain reduction about 4 dB on the loudest kicks; nothing brickwalled (chorus crest about 14 dB in the master).
- Short-term LUFS: choruses reach -10.2 / -9.6 (brief allowed "may reach -11"); this is what -14 integrated costs with an 8 LU range (verses -12.9, interlude -11.7 peak at the philter opening, intro -15.8). Backing the limiter off to keep choruses at -11 would land the integrated at about -15.
- Bar-to-bar RMS steps > 6 dB (master): bar 7 +9.2 (lead fragment enters over the pad bed), bar 9 +11.8 (verse downbeat: kick, bass and snare enter after a pad-only intro), bar 97 -6.8 (outro drop), bar 107/108 (fade to silence). The chorus downbeats (33, 81) are +5.7 / +4.9. Bars 7 and 9 are arrangement entries, not level bugs, but they are steep; see open questions.
- 2-4 kHz: verse -35.8, pre -41.9, chorus -32.4 / -31.8 dB while chorus RMS is 2.6 dB above the verse; the band rises 3.4 dB, i.e. under 1 dB relative to the whole mix. No presence spike.
- Bass duck: the 40-200 Hz envelope (10 ms) shows no periodic dip between kick hits in verses or choruses; the only dips are the kick's own decay. Not pumping. (A with/without-duck A/B was not rendered; the curve is -3 dB for 150 ms per hit.)
- Width: verse L/R correlation 0.78 (side/mid -9.1 dB), pre 0.74, chorus 0.42 (side/mid -3.8 dB), interlude 0.57, outro 0.44. Chorus wider than verse: pass. Leads centred (the lead inserts have no width processing; the wash and pad-serum enhancers carry the width).
- Interlude not empty: pads at -22 dB (250-1 k), vocal loop audible (above). Pass.
- Outro: last sample above -60 dBFS at 256.53 s, 2.67 s of silence before the 259.2 s end (master fade + texture fade + End marker at bar 109). Pass.
- Reversed swells audible before 33 / 81 (and 97): pass (numbers in item 4).
- Super VHS on the texture bus: intro >8 kHz -52 dB, 4-8 kHz -48 dB; hiss is band-limited 2-9 kHz and the bus low-passes at 8 kHz; no fizz.
- Masking bass vs kick: see the stem section at the end.

## Master chain (insert 0, in order)

0. Fruity Reeverb 2 (phase 3, atmospheric, wet 3 % / 5 % automated) - kept first so the EQ and limiter see the reverb tail; a limiter before a reverb would let the tail overshoot the ceiling.
1. FabFilter Pro-Q 4: Band 1 Low Cut 25.0 Hz 12 dB/oct; Band 2 Bell 380 Hz -0.70 dB Q 1.0 (the pads made 250-1 k the loudest band; a gentle mud trim, within the +/-1 dB tone limit); Output 0 dB. Verified by display.
2. FabFilter Pro-L 2: Gain **+8.80 dB** (v = dB/30), Output Level **-1.00 dBTP** (v = 1 + dBTP/30), Style Modern, lookahead/attack/release defaults (0.18 ms / 275 ms / 400 ms), true-peak and oversampling switches left at their unnamed defaults ("param 9/10").
3. Ozone 11: skipped. Its parameters are reachable only through the wrapper's names, and after the phase-2 unlicensed-plugin hang and today's renderer crash a 2000-parameter plugin on a chain that cannot be auditioned was not worth the risk (friction log entry "Master-chain scales had to be probed").

Limiter passes: +7.0 dB -> -15.5 LUFS (v012); +8.8 dB -> -13.9 LUFS (v013, accepted).

## Deliverables (all from `Parking-Lot-Moon-v013-master.wav`, confirmed with ffprobe)

- `Parking-Lot-Moon-final-master.mp3` - MP3 320 kbps CBR, 48 kHz, 259.2 s (10.37 MB)
- `Parking-Lot-Moon-final-master.flac` - FLAC 24-bit, 48 kHz, 259.2 s (50.1 MB)
- `Parking-Lot-Moon-final-master-44k16.wav` - PCM 16-bit 44.1 kHz, soxr resample with triangular high-pass dither, 259.2 s (45.7 MB)
- `Parking-Lot-Moon-chorus1-preview.mp3` - bars 33-48 (76.8-115.2 s), 38.4 s, 10 ms fade-in / 300 ms fade-out, 320 kbps
- `Parking-Lot-Moon-interlude-preview.mp3` - bars 49-56 (115.2-134.4 s), 19.2 s, same fades, 320 kbps

## Sessions and snapshots this phase

v010 (reopened from the v009 render snapshot) -> section render -> v010a -> section render -> v010b -> section render -> v010c -> full render v010 -> v011 -> stem render -> v011a -> full render (crashed, partial in `scratch/v011-render-crash-partial.wav`) -> v011b -> full render v011 -> v012 -> master render -> v013 -> master render (final) -> v013a/v013b (bass and kick stems for the masking report only, no other change).

Final session state: closed by the v013 master render to `C:\Users\poofi\AppData\Local\FlMcp\Projects\snapshots\2919e3ad82cc4d1ba9d37d635fafae93\Parking-Lot-Moon-v013-master.flp` (copied to `Parking-Lot-Moon\Parking-Lot-Moon-v013-master.flp`). Resume with `fl_project_start(projectPath="Parking-Lot-Moon/Parking-Lot-Moon-v014.flp", sourceProjectPath="Parking-Lot-Moon/Parking-Lot-Moon-v013-master.flp")`.

## Open questions for the orchestrator / user

- Nothing has been heard. The GMS preset ("Smooth & Warm TE", unison 10) and the opened Pad Serum filter were chosen by name and numbers; one listening pass on the chorus and interlude previews decides whether the pads are the right colour.
- The intro is quiet (-25.7 dBFS RMS in the master) and the lead fragment at bar 7 sits 9 dB above the bed; the verse entry at bar 9 is +11.8 dB. If that is too abrupt, add an automation clip on insert 4's Pro-Q Output (index 556) to lift the pad +3 dB in bars 1-8, or lower channel 1's volume for the intro pattern only.
- Chorus short-term loudness (-10.2 / -9.6) exceeds the -11 guideline because the integrated target was prioritised.
- The vocal loop still runs at 112 BPM (no time-stretch op); harmonic fit against the interlude chords was not checked by ear.
- Ozone 11 was skipped (above). The mastering EQ tone move is one -0.7 dB bell; no air was added on purpose ("wide but not bright").
- Super VHS Output now 100 %; its Mix stays 30 %. The friction log has the calibration.

## Bass vs kick masking (stems of bars 33-40, master chain in line, other channels muted)

Stems: `scratch/v013-stem-bass-33-40.wav` (channels 6 Bass Mid + 7 Sub) and `scratch/v013-stem-kick-33-40.wav` (channel 8), rendered from working copies v013a / v013b of the final snapshot; JSON in `records/masking-bass-kick-v013.json` (`fruitylink.analysis.masking_report`, clash threshold 6 dB, floor -60 dB).

- bass: peak -10.6 dBFS, rms -22.2, dominant 92.5 Hz | 20-45:-66.3 45-90:-31.7 90-150:-23.0 150-250:-35.6 250-500:-43.0 500-2000:-47.6
- kick: peak -4.0 dBFS, rms -23.9, dominant 51.2 Hz | 20-45:-39.0 45-90:-25.2 90-150:-32.9 150-250:-34.9 250-500:-41.5 500-2000:-45.5
- masking_report: ["45-90", "150-250", "250-500", "500-2000"]
- bands: {"<45": {"a_db": -45.58, "b_db": -31.85, "louder": "b", "overlap_db": -13.73}, "45-90": {"a_db": -26.81, "b_db": -26.06, "louder": "b", "overlap_db": -0.75}, "90-150": {"a_db": -25.09, "b_db": -32.59, "louder": "a", "overlap_db": -7.5}, "150-250": {"a_db": -30.94, "b_db": -34.79, "louder": "a", "overlap_db": -3.85}, "250-500": {"a_db": -41.0, "b_db": -40.41, "louder": "b", "overlap_db": -0.59}, "500-2000": {"a_db": -47.26, "b_db": -45.4, "louder": "b", "overlap_db": -1.86}}

The last session was closed by the kick stem render to `snapshots/a20ce615994343c29927ac5e0da45fc9/v013-stem-kick-33-40-full.flp` (all channels except the kick muted: do not continue from it). The final master state is `Parking-Lot-Moon-v013-master.flp` / `snapshots/2919e3ad82cc4d1ba9d37d635fafae93/Parking-Lot-Moon-v013-master.flp`.

Reading: the kick's body sits at 51 Hz (<45 Hz band 14 dB clear of the bass), the bass fundamental at 92.5 Hz (F#2) leads the kick by 7.5 dB in 90-150 Hz, and the only real shared zone is 45-90 Hz (bass -26.8 / kick -26.1 dB, overlap -0.75 dB), where the sub's F#1 (46 Hz) meets the kick's 51 Hz body. The -3 dB / 150 ms bass duck at every kick is what keeps that zone readable; the 150-250, 250-500 and 500-2000 flags are both sources 8-20 dB below their low bands (kick click vs bass harmonics under the 1.2 kHz low-pass) and are not audible masking. If the low end feels blurred on speakers, the next move is a Pro-Q 4 dynamic bell on insert 6 around 55 Hz (-2 dB) rather than more duck depth.
