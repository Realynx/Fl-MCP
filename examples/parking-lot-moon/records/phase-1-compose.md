# Parking Lot Moon - phase 1 (sources, harmony, patterns, arrangement)

Date: 2026-09-14. Tempo 100 BPM, PPQ 96 (bar = 384 ticks), 4/4, F# minor. Song end marker at bar 109 (tick 41472); last clip ends at bar 107.
Snapshots: v002 (patterns/clips, parameter-authored synths), v003 (reopened from v002; Serum factory presets loaded), v004 (closed from v003), v005 (reopened from v004, left open for phase 2).

## Channel map (channel index -> mixer insert)

| Ch | Name | Source | Insert |
|---|---|---|---|
| 0 | (unused template) | empty Sampler from the template (no delete op) | 0 Master |
| 1 | Lead Serum | Serum 2, factory `LD - Lush and Vintage` (3 PWM oscillators, LadderEMS LP, mono) | 1 Lead |
| 2 | Lead Oct | Serum 2, factory `LD - Analog Glow` (Basic Mg saw + SawRounded -1 oct, unison 3/7) | 2 Lead Oct |
| 3 | Lead Wash | Serum 2, factory `KY - Echoes Of The Past` (sine + saw -1 oct + spectral, reverb 48 % + convolution 28 %); Filter 1 Freq lowered to 1505 Hz | 3 Lead Wash |
| 4 | Pad Analog | GMS: 3 oscillators (osc 2 +2 detune at 60 %, osc 3 -12 st at 35 %), unison 4, detune 0.30, stereo 0.85, amp attack 0.50, release 0.62, filter cutoff 0.60, res 0.12 | 4 Pad |
| 5 | Pad Serum | Serum 2, factory `PD - Lush Chorus` (OSCAR + Dying Saw + PWM Juno, all -1 oct, Hyper/Dimension 30 %, delay 40 %), Macro 1 = 35 | 5 Pad Serum |
| 6 | Bass Mid | Serum 2, factory `BA - Analog SQ`; A/B Octave set back from -2 to 0 oct, Mono on | 6 Bass |
| 7 | Sub | 3x Osc: osc 1 shape value 0 (sine per enum order; display is blank), osc 2/3 muted, volume envelope on (attack 12 %, release 30 %) | 7 Sub |
| 8 | Kick | Samples/Kick_SoftScoop.wav | 8 Kick -> 15 Drum Bus |
| 9 | Snare | Samples/Snare_Linn_LoMid.wav | 9 Snare -> Drum Bus |
| 10 | Clap Room | Samples/Clap_Lofi_Room.wav | 10 Clap -> Drum Bus |
| 11 | Hat Closed | Samples/Hat_Closed_Tape.wav | 11 Hat Closed -> Drum Bus |
| 12 | Hat Open | Samples/Hat_Open_RawRoom.wav | 12 Hat Open -> Drum Bus |
| 13 | Rim | Samples/Rim_Clean.wav | 13 Rim -> Drum Bus |
| 14 | Perc Metal | Samples/Perc_Metal_Iron.wav | 14 Perc -> Drum Bus |
| 15 | Tex Crackle | Samples/Tex_TapeCrackle.wav (12.3 s) | 16 Textures |
| 16 | Tex Hiss | Samples/Tex_TapeHiss.wav (6.2 s) | 16 Textures |
| 17 | Tex Roomtone | Samples/Tex_Roomtone_Suburb.wav (70 s) | 16 Textures |
| 18 | Vox E8 | Samples/Vox_E8_everywhere_112_Gbm.wav (8.6 s, 112 BPM, Gbm) | 17 Vox |

Mixer inserts: 1 Lead, 2 Lead Oct, 3 Lead Wash, 4 Pad, 5 Pad Serum, 6 Bass, 7 Sub, 8 Kick, 9 Snare, 10 Clap, 11 Hat Closed, 12 Hat Open, 13 Rim, 14 Perc, 15 Drum Bus, 16 Textures, 17 Vox (added with `fl.mixer.add`; the template only had 16). Inserts 8-14 got `send_to(15, 1.0)` and `send_to(0, 0.0)`; whether the Master route is really off cannot be read back (friction log).
Channel volumes (raw /12800): lead 9000, oct 7000, wash 6000, pad 8500, pad serum 7000, bass 8500, sub 9000, kick 10000, snare 9000, clap 6500, hats 7500/6500, rim 6000, perc 5200, crackle 3200, hiss 2800, roomtone 3600, vox 5000. No mixer effects yet (phase 2).

## Samples (copied from Splice into `Samples/`, never committed)
Kick_SoftScoop (Capsun Lo-Fi Soul), Snare_Linn_LoMid (Linndrum From Mars), Clap_Lofi_Room (Escapism), Hat_Closed_Tape (Cassette Vibes), Hat_Open_RawRoom (Capsun), Rim_Clean (under my skin - soulful rnb), Perc_Metal_Iron (Capsun ClosedHH Iron, used as metallic perc), Tex_TapeCrackle + Tex_TapeHiss (Escapism SFX), Tex_Roomtone_Suburb (Essential Roomtones), Vox_E8_everywhere_112_Gbm (Divine Vocal Emanations). The catalog has no 12-bit/ATD2 kick (those packs only hold one hat / one snare).

## Harmony (scientific pitch, C4 = 60), 8-bar loop, one chord per bar
1 F#m9 [42 49 52 57 68] root | 2 Dmaj7, 2nd inversion (A bass) [45 54 61 62 66] | 3 A(add9)/C# [49 52 57 59 64] | 4 Esus4 -> E [47 52 57 59 64] -> [47 52 56 59 64] |
5 F#m7, 1st inversion [45 52 54 61 69] | 6 Bm(add9), 1st inversion [50 54 59 61 66] | 7 Dmaj7#11 [50 57 61 66 68] (G# against D) | 8 C#m7, 2nd inversion [44 49 52 59 64] -> C#7(b9) [44 49 53 59 62].
Chorus voicings add a sixth voice on top (73/74/71/...) for the lift on the D and A bars; Chorus 2 adds a further octave top voice. Intro uses the lowest 3 voices, verse/interlude/outro 4 voices, pre/chorus all voices. Pad velocities 58-92 +/-4, starts +/-3 ticks, lengths bar + 8 ticks (legato).
Bass roots: F#2 D2 C#2 E2 F#2 B1 D2 C#2 (mid) / F#1 D2 C#2 E2 F#1 B1 D2 C#2 (sub); passing tones C#2 (bar 6) and E# (bar 8; chorus and pre only). Interlude bass only on bars 1/3/5/7, sub on 1/5.
Motif: C#5 A4 F#4 G#4 | E5 (73 69 66 68 76): dotted quarter, eighth, dotted quarter, eighth pickup, dotted half. Variants: displaced by one beat (chorus 1 bar 5), third-shift 76 73 69 71 79 (bar 9 of each chorus), retrograde (chorus 2 bar 5), F#5 -> C#5 ending (chorus 2 bar 14). Fragments (2-4 notes) in intro bar 7, verse bars 4/8/10/14, outro bars 100/104; G#4 alone over the final Dmaj7#11.

## Patterns (index: name, notes, length in bars)
1 Pad Intro 30 (8) | 2 Pad Verse 40 (8) | 3 Pad Pre 50 (8) | 4 Pad Chorus 60 (8) | 5 Pad Chorus 2 70 (8) | 6 Pad Interlude 40 (8) | 7 Pad Outro A 40 (8) | 8 Pad Outro B 6 (2, Dmaj7#11 held) |
9 Pad Serum Chorus 30 (8) | 10 Pad Serum Interlude 8 (8) | 11 Bass Verse 19 | 12 Bass Chorus 27 | 13 Bass Pre 17 | 14 Bass Interlude 6 | 15 Bass Outro A 12 | 16 Bass Outro B 2 |
17 Lead Intro 2 (7) | 18 Lead Verse A 5 | 19 Lead Verse B 7 | 20 Lead Chorus 1 22 (16) | 21 Lead Chorus 2 23 (16) | 22 Lead Outro 7 | 23 Lead Outro B 1 (2) |
24 Lead Oct Chorus 1 16 (16) | 25 Lead Oct Chorus 2 18 (16) | 26 Lead Wash Chorus 1 22 (16, +24 ticks late) | 27 Lead Wash Chorus 2 23 (16) | 28 Lead Wash Verse 2 8 (16; answers at bars 6, 10, 14) |
29 Drums Verse A 112 | 30 Drums Verse B 113 (fill in bar 8) | 31 Drums Pre 57 (kick on 1, rim on 2+4, quarter hats, snare on beat 4 of bar 8) | 32 Drums Chorus A 116 | 33 Drums Chorus B 117 | 34 Drums Outro 46 (6) | 35 Perc Chorus 2 14 |
36 Tex Bed 6 (crackle at bars 1 and 6, hiss every 2.5 bars) | 37 Tex Roomtone 1 | 38 Vox Interlude 2.
Total notes: 1195 (verified identical after v002 -> v003 and v004 -> v005 reopen). Drums: kick on 1 plus and-of-2 (odd bars) or 3 (even bars), snare + clap (clap +3 ticks, -34 velocity) on 2 and 4, 1/8 closed hats with +3-tick off-beat swing and dropped hits in bars 3/6/7, open hat at phrase ends, rim ghosts in bars 2/6, fills only in the B variants (bar 16 of a section), no rolls. Every drum and pad note is humanised (velocity +/-2..6, timing +/-1..3 ticks).

## Arrangement (playlist tracks: 1 Lead, 2 Lead Oct, 3 Lead Wash, 4 Pad, 5 Pad Serum, 6 Bass, 7 Drums, 8 Perc, 9 Textures, 10 Vox) - 75 clips
- 1-8 Intro: Pad Intro, Lead Intro (7 bars), Tex Bed, Tex Roomtone
- 9-24 Verse 1: Pad Verse x2, Bass Verse x2, Drums Verse A then B, Lead Verse A then B, Tex Bed x2
- 25-32 Pre: Pad Pre, Bass Pre, Drums Pre, Tex Bed
- 33-48 Chorus 1: Pad Chorus x2, Pad Serum Chorus x2, Bass Chorus x2, Drums Chorus A then B, Lead Chorus 1, Lead Oct Chorus 1, Lead Wash Chorus 1, Tex Bed x2
- 49-56 Interlude: Pad Interlude, Pad Serum Interlude, Bass Interlude, Vox Interlude, Tex Roomtone, Tex Bed (no drums)
- 57-72 Verse 2: as verse 1 plus Lead Wash Verse 2
- 73-80 Pre 2: as pre
- 81-96 Chorus 2: Pad Chorus 2 x2, Pad Serum Chorus x2, Bass Chorus x2, Drums Chorus A then B, Perc Chorus 2 x2, Lead Chorus 2, Lead Oct Chorus 2, Lead Wash Chorus 2, Tex Bed x2
- 97-108 Outro: Pad Outro A (97), Pad Outro B (105-106), Bass Outro A (97) / B (105), Lead Outro (fragments at 100 and 104), Lead Outro B (G#4 at 105), Drums Outro (97-102), Tex Bed (97; last crackle ends about bar 107.1)
- End marker bar 109: about 4.5 s after the last trigger (synth release tails inside that).
Clip lengths had to be forced with `fl.clips.resize` because `add_patterns` used each pattern's own length (see friction log).

## Persistence
- v002 -> v003: channels, mixer names/routes, 38 patterns, 1195 notes, 75 clips, End marker, all Serum/GMS/3x Osc parameter displays identical (`records/phase-1-state-v002.json` vs `-v003.json`, empty diff).
- v004 -> v005: see the "v005 check" section appended below.

## Open questions for the orchestrator / phase 2-3
- Serum presets were picked by name and description (auditioning needs a render, which closes the session). Phase 2 should listen: `LD - Lush and Vintage` is mono with a 180 ms release; `BA - Analog SQ` came with both oscillators at -2 oct (reset to 0); `KY - Echoes Of The Past` carries its own reverb and convolution; `PD - Lush Chorus` has a dark base cutoff (146 Hz plus envelope/macro), Macro 1 set to 35.
- The lead's chorus must come from the mixer chain (Hyper Chorus / Vintage Chorus): Serum's FX rack is not addressable by effect name through the wrapper.
- GMS filter cutoff (parameter 32, now 0.60) is the phase-3 automation target for the pad opening into the choruses; its Hz mapping is unknown.
- 3x Osc osc-1 shape display is blank; value 0 was assumed to be sine (enum order sine/tri/square/saw).
- The reversed swell into bars 33/81, sidechain, vocal treatment and texture band-limiting are phase 2. The vocal loop is 112 BPM in Gbm and needs stretching to 100 BPM (or use it as texture as is).
- Verify in the mixer that inserts 8-14 no longer feed Master directly.
- Tex Roomtone (70 s) at bars 1 and 49 runs under the following sections; phase 3 should fade textures out by bar 107.
- Channel 0 "(unused template)" cannot be deleted through the SDK.
- A first-session bug blocked all plugin-state reads/loads until the project was closed and reopened (friction log: "FL event 254 is truncated"); if it recurs, close to a fresh vNNN and resume.

## v005 check (session left open on Parking-Lot-Moon-v005.flp, reopened from v004)
38 patterns, 1195 notes, 75 clips, End marker at 41472 (bar 109), insert 17 = Vox, channel routes [0,1,2,3,4,5,6,7,8,9,10,11,12,13,14,16,16,16,17].
Serum: ch1 Lush and Vintage (OSCAR frame 1/4, Main Vol 68 %, mono, Filter 1 284 Hz), ch2 Analog Glow (Basic Mg saw, 42 %, 53 Hz), ch3 Echoes Of The Past (sine, Filter 1 1505 Hz), ch5 Lush Chorus (Macro 1 = 35, 146 Hz base), ch6 Analog SQ (mono, A/B 0 oct, 31 %). GMS cutoff 0.60 / unison 4 / osc 3 -12; 3x Osc volume envelope on, attack 12 %. All match the values written in v003.
