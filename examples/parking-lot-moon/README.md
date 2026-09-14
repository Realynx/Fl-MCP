# Parking Lot Moon

**Final master:** [Parking-Lot-Moon-final-master.mp3](Parking-Lot-Moon-final-master.mp3) (4:19, -13.9 LUFS integrated, -1.0 dBTP).
**Project file:** [Parking-Lot-Moon.flp](Parking-Lot-Moon.flp) (the v015 master session; FL Studio 2026; Serum 2, GMS, 3x Osc,
FabFilter Pro-Q 4 / Pro-R 2 / Pro-C 3 / Pro-DS / Pro-L 2 / Saturn 2, Super VHS and stock effects; the drum, texture and vocal
samples are commercial and not included, so those channels load silent without them). The phase records, verification
measurements and the bass-versus-kick masking report are in [records/](records/).

`Parking Lot Moon` is the second album track produced to exercise FL MCP. It is in F# minor at 100 BPM, 4/4, 108 bars
(4:19.2, with 2.7 s of silence before the end marker), and was composed, mixed, automated, mastered and revised entirely
through `fl_execute_python` and the `fruitylink` helpers in FL Studio 26.1.3. Nothing was played or clicked in the FL
interface. The work ran as five phases with a persistence check (capture, close, reopen, diff) at the end of each, then two
listening revisions from the producer. Every level quoted below was measured from a render.

## The brief

A friend's prompt asked for an original track with a dreamy, melancholic, nocturnal sound blending dream-pop, trip-hop,
dark synthpop, shoegaze atmosphere and vaporwave texture, with slightly degraded electronic production; the named songs
in the prompt were mood references only, not to be copied. It specified 96-104 BPM in a minor key with room for
bittersweet major-colour chords, roughly 3:30-4:30, extended voicings (minor 7ths, add9, major 7ths, suspensions,
inversions, one borrowed major chord and one gently unresolved tension note), a wide slow analog pad with an automated
low-pass that opens into the choruses, a 3-6 note motif introduced as fragments before the chorus reveals it, doubled by an
octave layer and a washed-out layer with a reversed swell before important notes, soft submerged drums programmed by hand
with humanised velocity and timing, a mono low-passed bass sidechained 2-4 dB to the kick without obvious pumping,
low-level texture layers (vinyl, hiss, room tone, reversed samples) band-limited so they sound like damaged memories,
a vocal treated as an atmospheric instrument with filtered moments, and heavy automation of pad filter, lead filter,
reverb, delay, textures, drum filtering and stereo width. It asked for headroom: peaks around -6 dB before final limiting
and no brickwalling. The orchestrator turned this into the form and harmony below; the producer's instruction on top was
to use the premium plugins and samples (Serum 2, FabFilter, Splice) rather than stock alone.

## Form

| Bars | Section |
|---|---|
| 1-8 | Intro: GMS pad (3 low voices), tape crackle / hiss / suburban room tone, lead fragment at bar 7 |
| 9-24 | Verse 1: pad, bass + sub, soft drums, lead fragments |
| 25-32 | Pre-chorus: drums filtered and dipped, pad opens, reversed vocal swell into 33 |
| 33-48 | Chorus 1: full motif, octave layer, washed layer, Pad Serum, full drums, borrowed D / A major lift |
| 49-56 | Interlude: drums out, vocal loop (Love Philter sweep), chopped second vocal entry, pads, sparse bass |
| 57-72 | Verse 2: as verse 1, motif fragments answered by the washed layer |
| 73-80 | Pre-chorus 2 (swell into 81) |
| 81-96 | Chorus 2: wider, extra top voice, metallic percussion; swell into the outro |
| 97-108 | Outro: chords descend to Dmaj7#11, lead fragment unresolved, textures and master fade; silence from bar 108 |

Harmony (8-bar loop, one chord per bar): F#m9 | Dmaj7 | A(add9)/C# | Esus4 -> E | F#m7 | Bm(add9) | Dmaj7#11 |
C#m7 -> C#7(b9). The G# against D in bar 7 is the unresolved tension the brief asked for; the outro ends on it.
Motif: C#5 A4 F#4 G#4 E5, varied by displacement, third-shift and retrograde in the choruses.

## Sources

- Serum 2 factory presets: lead `LD - Lush and Vintage`, octave layer `LD - Analog Glow`, washed layer
  `KY - Echoes Of The Past`, pad `PD - Lush Chorus` (filter opened to 1.39 kHz), bass `BA - Analog SQ`.
- GMS analog pad from the factory `.gmsynth` preset `Smooth & Warm TE`; 3x Osc sine sub.
- Samples from the Splice library (Capsun Lo-Fi Soul kick, open hat and iron perc; Linndrum From Mars snare; Escapism
  clap, tape crackle and hiss; Cassette Vibes closed hat; Essential Roomtones suburb room tone; a rim from
  "under my skin") and a Divine Vocal Emanations vocal loop ("everywhere", 112 BPM, Gbm), also reversed for the swells
  and sliced into eight pieces for the v014 / v015 chop.
- Effects: FabFilter Pro-Q 4, Pro-R 2, Pro-C 3, Pro-DS, Pro-L 2 and Saturn 2; Baby Audio Super VHS; Hyper Chorus and
  Vintage Chorus; Fruity Parametric EQ 2, Chorus, Reeverb 2, Delay 3, Limiter (COMP), Compressor, Soft Clipper,
  Stereo Shaper, Stereo Enhancer and Love Philter. Every setting is listed in
  [records/phase-2-mix.md](records/phase-2-mix.md); the 17 automation clips in
  [records/phase-3-automation.md](records/phase-3-automation.md).
- Totals at v015: 46 channels (21 instruments and samples, 8 chop samplers, 17 automation), 41 patterns, 1,213 notes,
  97 playlist clips, 25 mixer inserts.

### Samples are not included

The Splice samples and the vocal loop are commercial and are never redistributed. The FLP references copies that lived
in a local `Samples\` folder next to the project (for example `Samples\Kick_SoftScoop.wav` and the eight
`Vox_E8_everywhere_112_Gbm-slice-0N.wav` files), so those 19 sampler channels load silent and FL reports the files as
missing. The Serum, GMS and 3x Osc channels, every effect chain and all automation open as saved. To hear the arrangement
with drums, drop equivalent one-shots into the same paths or replace the channel samples through the SDK
(`replace_channel_sample`).

## Revision history

Workspace `vNNN.flp` files were session start states written by `fl_project_start`; each phase ends with a close to a
fresh snapshot and a reopen from it, so the odd numbers are mostly reopen checks. The measured end state of each render
is what the records quote. Full detail per phase: [phase-1-compose.md](records/phase-1-compose.md),
[phase-2-mix.md](records/phase-2-mix.md), [phase-3-automation.md](records/phase-3-automation.md),
[phase-4-master.md](records/phase-4-master.md), [phase-5-v014.md](records/phase-5-v014.md).

### v001: template

The template project (FL build 4726) with the tempo set to 100 BPM. The first session opened from this copy could not
read or load any plugin state ("FL event 254 is truncated"); closing to v002 and reopening cleared it, so preset loading
happened one snapshot later than planned.

### v002 to v005: phase 1, sources, harmony, patterns, arrangement

Eighteen channels (five Serum 2 layers, a parameter-authored GMS pad, a 3x Osc sub, seven drum samples, three textures
and the vocal loop) were routed to seventeen named inserts, the drums through a bus. The 8-bar progression was voiced
over C#2-E5 with no chord in root position twice in a row, the motif written with four variants and its fragments
scattered through the intro, verses and outro, and the drums programmed by hand with humanised velocity and timing,
dropped hits and fills only at phrase ends. 38 patterns, 1,195 notes and 75 clips were placed over the 108-bar form
with an end marker at bar 109. Serum factory presets were loaded by name after the reopen (v003). State captured before
the close matched the reopened project exactly (v002 to v003, v004 to v005). Clip lengths had to be forced after
placement because `add_patterns` used each pattern's own length.

### v006 to v007: phase 2, mix

Every insert got its chain, each value written with `set_verified` and quoted from the plugin display: Pro-Q 4 on
nearly everything, Hyper Chorus and Vintage Chorus on the leads, Pro-R 2 and Reeverb 2 reverbs, Delay 3, Saturn 2,
Super VHS on the texture bus, Pro-C 3 and Pro-DS on the vocal, a Love Philter for the interlude sweep, plus long-reverb
and delay send inserts (25 inserts in all). The sidechain the brief asked for turned out to be unreachable: no route can
be flagged as a sidechain and neither Fruity Limiter nor Pro-C 3 exposes a source, so the Limiter stayed as gentle bass
compression and ducking was deferred to automation. Two audio edits were done in pure Python inside the FL runtime: an
8-bar trimmed and faded room tone, and the last two bars of the vocal loop reversed as the swell into each chorus.
Gain staging was done in plugin output gains because the mixer-volume dB curve is undocumented. 40 patterns, 1,198
notes, 79 clips; the 53 captured parameter groups were identical after the reopen. Nothing had been heard yet.

### v008 to v009: phase 3, automation

Sixteen automation clips, one per playlist track, from bar 1 to the end marker: the pad low-pass opening into each
chorus on both pad inserts, the lead high shelf, lead and pad reverb amounts, delay-send pulses at phrase ends,
texture volume, the drum-bus low-pass and level dip through the pre-choruses, master reverb, wash and pad widths, the
vocal filter sweep, the master fade, and the bass duck: 162 kick hits read from the drum patterns through the playlist
and converted into a 488-point volume curve on the bass insert (-3 dB per hit, 150 ms recovery), because automation
clips cannot loop or offset. Scales for Reeverb 2, Pro-Q frequencies and the automation tension sign were calibrated
first. All 16 clips, event ids and points survived the reopen byte-for-byte. Session left open on v009.

### v009 render and v010 to v011: phase 4, first listen by numbers

The first full render measured -22.8 LUFS with the pad inaudible and the textures near -80 dBFS. Per-bar band scans
found the causes: the parameter-authored GMS instance had no oscillator waveform (GMS keeps them in GUI-only files),
the Pad Serum filter sat at 146 Hz under a 150 Hz high-pass, and the quiet Splice samples sat on a channel-volume scale
that is a power curve (raw 3200 is about -29 dB). v010 loaded the factory `Smooth & Warm TE` GMS preset as native plugin
state, opened Pad Serum to 1.39 kHz and re-staged the textures and vocal from measured levels (-21.0 LUFS). v011 trimmed
the swells and low end and raised the vocal; its first render attempt died mid-way and left a partial WAV, and the retry
from the preserved snapshot succeeded (-20.2 LUFS, chorus peaks about -5 dBFS before limiting).

### v012 to v013: master

The master chain on insert 0 is the phase-3 Reeverb 2, a Pro-Q 4 (25 Hz low cut, -0.7 dB at 380 Hz) and Pro-L 2 with
its gain and ceiling scales probed first. +7 dB landed at -15.5 LUFS and made the interlude louder than the verse; v013
took the vocal down 6 dB and the limiter to +8.8 dB for -13.9 LUFS, -0.99 dBTP, 8.3 LU, with 563 samples touching the
ceiling and no full-band bar-to-bar jumps except the arrangement entries at bars 7 and 9. Chorus L/R correlation 0.42
against 0.78 in the verse, 2-4 kHz within a decibel of the verse relative to the mix, 2.67 s of silence at the end.
Bass and kick stems (v013a / v013b, not for continuing) fed the masking report: the only shared zone is 45-90 Hz, which
the duck keeps readable. See [verification-v013-master.json](records/verification-v013-master.json) and
[masking-bass-kick-v013.json](records/masking-bass-kick-v013.json).

### v014: three listening notes from the producer

First listen. An out-of-key note at the chorus apex: every chorus lead pattern was read back with note names against
the chord of each bar, and the G natural at the top of the third-shift variant (bars 42 and 90, over Dmaj7, doubled an
octave up and in the wash layer) became F#, six notes in all. Two intro lead notes far too loud: velocities dropped to
60 / 52 and a new Pro-Q output automation on the lead insert holds -12 dB through bar 8, which cut bar 7 by 6.7 dB
(the velocity change alone did little). A rhythmic chop of the second vocal entry: the loop was sliced into eight
pieces in Python, loaded as eight sampler channels on the vocal insert and sequenced as 27 hits with octave-down and
+5 pitches, replacing the bar-53 trigger. -13.6 LUFS, -1.0 dBTP. See
[verification-v014-master.json](records/verification-v014-master.json).

### v015: final

The producer liked the octave-down shift but wanted it sparser, and heard the original vocal still playing underneath
as if on a delay. A render with the chop channels muted proved nothing of the original remained under bars 53-56; the
ghost was the 0.55 s slices retriggered every eighth. The figure was rewritten as 17 hits (four or five per bar, rests
on every off-beat, five octave-down hits on strong beats or phrase ends, never adjacent) at a lower channel volume.
Bars 53-56 now sit 1.6-6.9 dB above the v013 entry instead of 6-9 dB; every other bar is within 0.5 dB of v013. Final
master -13.9 LUFS, -1.0 dBTP, 8.8 LU. See [verification-v015-master.json](records/verification-v015-master.json).

## How to continue from v015

1. Start a new working path from this file with `fl_project_start`, passing `Parking-Lot-Moon.flp` as
   `sourceProjectPath` and a fresh `projectPath` such as `Parking-Lot-Moon/Parking-Lot-Moon-v016.flp`. Never reuse an
   existing vNNN name.
2. Work through `fl_execute_python` with the `fl` global. Stock plugin parameter names carry a `^b^a` prefix, FabFilter
   names are clean; write with `parameters.set_verified(index, value)` and read displays back in a later request.
3. To hear one change cheaply, render a range: `fl_project_render(outputPath, startBar, endBar, cutClips=true,
   tailBeats=8)`. It closes the session and returns a `-full.flp` snapshot to resume from.
4. The master chain is on insert 0: slot 0 Reeverb 2 (a mix element, leave it), slot 1 Pro-Q 4, slot 2 Pro-L 2 (Gain
   index 0 = dB / 30, Output Level index 18 = 1 + dBTP / 30). Remove or bypass slots 1-2 to work on the mix again.
5. Automation channel 36 is the bass duck; if a real sidechain is ever wired in the GUI, mute it to avoid double
   ducking. Automation channel 45 is the intro lead trim.
6. Measure before committing: loudness and true peak with `ffmpeg -af ebur128=peak=true`, per-bar band scans with
   `fruitylink.analysis`; keep -14 LUFS, -1 dBTP and about -6 dBFS peaks before the limiter.
