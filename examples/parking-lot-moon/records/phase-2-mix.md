# Parking Lot Moon - phase 2 (mix: FX chains, sends, sidechain, textures, vocal, gain staging)

Date: 2026-09-14. Session resumed on v005, closed to `Parking-Lot-Moon-v006.flp`, reopened as `Parking-Lot-Moon-v007.flp` (left open for phase 3).
Every value below was written with `Parameters.set_verified` (or read back in a later request) and is quoted as the plugin's own display string. Stock plugin parameter names carry a `^b^a` prefix in FL's list; indices are what phase 3 should use. Helpers: `scratch/p2helpers.py` (Pro-Q 4 / PEQ 2 calibrations, `apply`, `capture`).

## Mixer layout (25 tracks) and routing
Inserts 1-17 as phase 1. New: 18 Crackle (ch 15), 19 Hiss (ch 16), 20 Roomtone (ch 17), 21 Reverse (new ch 19 "Tex Reverse Vox"), 22 Vox Double (new ch 20 "Vox Double"), 23 Verb Long (send), 24 Delay Send (send). 18-21 feed 16 Textures at 0.8 (unity) with Master at 0; drum inserts 8-14 feed 15 Drum Bus at 0.8 (phase 1 had them at 1.0 = above unity) with Master at 0. Send readback works: `fl.mixer[t].effects.list_text()` prints a `sends:` line; **0.8 is unity** on this scale.
Mixer volumes all stay at 12800 (0 dB; the raw-to-dB curve is undocumented); gain staging is done in plugin output gains (dB-exact, see below).

## Chains (insert: slot plugin - verified settings)
- **1 Lead**: 0 Pro-Q 4 (Low Cut 120 Hz 12 dB/oct; Bell 3000 Hz -1.50 dB Q 1.2; High Shelf 6000 Hz -2.00 dB; Output Level -6.00 dB) | 1 Hyper Chorus (Delay 40 %, Feedback 30 %, Mod rate 40 %, Mod amount 50 %, Movement 30 %/30 %, 4 lines, LP 5979.8 Hz, HP 72.9 Hz, Mix 40 % wet) | 2 Saturn 2 (Band 1 Clean Tape, Drive 15.0 %, Mix 35.0 %, Output 0 dB) | 3 Pro-R 2 (Space 2.850 s, Brightness -30 %, Width 72 %, Mix 18.0 %, Predelay 15 ms) | 4 Fruity Delay 3 (sync, Time 3:1 = dotted 1/8, Feedback 20 %, LP 2436.9 Hz, Out wet 15 %, dry 100 %).
- **2 Lead Oct**: 0 Pro-Q 4 (Low Cut 150 Hz; High Shelf 5000 Hz -3.00 dB; Output -12.00 dB = 6 dB under the lead) | 1 Vintage Chorus (Speed 0.90 Hz, Mix 40 % wet, HP 35.6 Hz) | 2 Pro-R 2 (2.500 s, -30 %, 72 %, Mix 15 %, 15 ms) | 3 Delay 3 (3:1, 20 %, LP 2436.9 Hz, wet 12 %).
- **3 Lead Wash**: 0 Pro-Q 4 (Low Cut 200 Hz; High Cut 2500 Hz 12 dB/oct; Output -10.00 dB) | 1 Fruity Chorus (Delay 15.025 ms, Depth 2.0 ms, Stereo 90 deg, LFO 1 0.3125 Hz) | 2 Pro-R 2 (Space 5.200 s, Brightness -40 %, Width 108 %, Mix 45 %, Predelay 22.5 ms) | 3 Delay 3 (4:1 = 1/4, Feedback 30 %, LP 1299.6 Hz, wet 25 %) | 4 Fruity Stereo Enhancer (Stereo separation "40% separated").
- **4 Pad / 5 Pad Serum** (identical except delay wet): 0 Pro-Q 4 (Low Cut 150 Hz 24 dB/oct; Bell 350 Hz -2.50 dB Q 1; **High Cut 6000 Hz 12 dB/oct = automatable low-pass**; Output -8.00 dB) | 1 Fruity Chorus (Delay 12.04 ms, Depth 0.75 ms, Stereo 90 deg, LFO 0.3125/0.2/0.45 Hz) | 2 Fruity Reeverb 2 (Low cut 168 Hz, High cut 5.0 kHz, Predelay 31 ms, Room 80, Decay 4.5 s, Damping 3.5 kHz, Dry 100 %, ER 25 %, Wet 25 %) | 3 Delay 3 (3:1, Feedback 20 %, LP 2892.8 Hz, wet 20 % on 4 / 12 % on 5, dry 100 %).
- **6 Bass**: 0 Pro-Q 4 (Low Cut 30 Hz; High Cut 1200 Hz 24 dB/oct; Output -4.00 dB) | 1 Fruity Stereo Shaper mono (L, R, L-to-R, R-to-L all -6.4 dB normal = (L+R)/2 on both sides) | 2 Fruity Limiter COMP (threshold -11.8 dB, ratio 1:3.0, knee 40 %, attack 3.08 ms, release 162.71 ms).
- **7 Sub**: 0 Pro-Q 4 (Low Cut 25 Hz; High Cut 120 Hz 24 dB/oct; Output -6.00 dB). No compressor (3x Osc sine is already mono and steady).
- **8 Kick**: Pro-Q 4 High Shelf 4000 Hz -3.00 dB; pan 0. **9 Snare**: Pro-Q 4 High Cut 9000 Hz; pan 0. **10 Clap**: Pro-Q 4 High Cut 8000 Hz + Reeverb 2 short room (Low cut 198 Hz, High cut 5.9 kHz, Predelay 10 ms, Room 31, Decay 0.9 s, ER 30 %, Wet 20 %). **11 Hat Closed** pan -900, **12 Hat Open** pan +700, **13 Rim** pan +1800, **14 Perc** pan -1800 (mixer scale, 0 = centre; rim/perc keep their quiet phase-1 channel volumes 6000/5200).
- **15 Drum Bus**: 0 Fruity Parametric EQ 2 (Band 1 Low shelf 60 Hz +1.0 dB; Band 3 Peaking 400 Hz -1.5 dB; **Band 6 Low pass 20000 Hz order 2 = automatable drum filter**; Band 7 High shelf 8000 Hz -2.0 dB; Main level -5.0 dB) | 1 Fruity Compressor (threshold -18.0 dB, ratio 1.4:1, attack 10.0 ms, release 151 ms, Soft/R, gain 0) | 2 Fruity Soft Clipper (threshold -0.9 dB, post gain 100 %).
- **16 Textures bus**: 0 PEQ 2 (High pass 120 Hz, Low pass 8000 Hz, Main level -12.0 dB) | 1 Saturn 2 (Subtle Tape, Drive 8 %, Mix 25 %) | 2 Reeverb 2 (Low cut 198 Hz, High cut 5.0 kHz, Predelay 20 ms, Room 70, Decay 3.0 s, Damping 3.5 kHz, ER 20 %, Wet 20 %) | 3 Delay 3 (4:1, Feedback 25 %, LP 1299.6 Hz, wet 12 %) | 4 Super VHS (Heat 15 %, Wash 10 %, Drift 10 %, Static 5 %, Mix 30 %, Output 80 %).
- **18 Crackle** PEQ 2 HP 300 Hz / LP 5000 Hz; **19 Hiss** HP 2000 / LP 9000; **20 Roomtone** HP 100 / LP 3000.
- **21 Reverse**: Pro-Q 4 (Low Cut 200 Hz; High Cut 4000 Hz; Output -8.00 dB) | Pro-R 2 (4.000 s, -40 %, 108 %, Mix 50 %, 5 ms).
- **17 Vox**: 0 Pro-Q 4 (Low Cut 150 Hz; High Cut 9000 Hz; Bell 3000 Hz -1.00 dB; Output -8.00 dB) | 1 Pro-C 3 (Clean, threshold -20.00 dB, ratio 2.00:1, attack 10.72 ms, release 101.3 ms, auto gain on) | 2 Pro-DS (Single Vocal, threshold -36 dB, range 6 dB - defaults, light) | 3 Saturn 2 (Subtle Tape, Drive 10 %, Mix 30 %) | 4 Reeverb 2 short room (Low cut 228 Hz, High cut 5.9 kHz, Predelay 15 ms, Room 36, Decay 1.1 s, Wet 18 %) | 5 Fruity Love Philter neutral (Filter 1 cutoff 100 %, resonance 0 %; there is no slot-bypass op, so it is left in line fully open).
- **22 Vox Double**: channel 20 = same WAV, channel pitch raw 5 (cents assumed), channel pan 4400 (left of the 6400 centre), channel volume 3600; Pro-Q 4 (Low Cut 200 Hz; High Cut 3000 Hz 24 dB/oct; Output -12.00 dB) | Pro-R 2 (5.200 s, -40 %, 108 %, Mix 60 %, 15 ms).
- **23 Verb Long**: Pro-R 2 (Space 7.000 s, Distance 70 %, Brightness -50 %, Width 102 %, **Mix 100 %**, Predelay 22.5 ms).
- **24 Delay Send**: Fruity Delay 3 (Input wet 100 %, sync, Time 3:1 dotted 1/8, Feedback 37.5 %, LP 1753 Hz, Output wet 100 %, dry 0 %).
- **0 Master**: empty (phase 4).

## Send matrix (level on FL's 0.8 = unity scale, read back from the `sends:` line)
| src | to 23 Verb Long | to 24 Delay Send |
|---|---|---|
| 1 Lead | 0.2 | 0.15 |
| 2 Lead Oct | 0.15 | - |
| 3 Lead Wash | 0.25 | - |
| 4 Pad / 5 Pad Serum | 0.2 | 0.1 |
| 16 Textures | 0.12 | 0.08 |
| 17 Vox | 0.3 | 0.25 |
| 22 Vox Double | 0.3 | - |

Kick 8 to Bass 6 exists at level 0 (sidechain placeholder, see below).

## Sidechain result
No API exists to flag a mixer route as a sidechain; Fruity Limiter exposes no sidechain-source parameter (18 params) and Pro-C 3's "Side Chain Input" can be set to External (v 0.25-0.34) but receives nothing. Outcome: Fruity Limiter COMP stays on insert 6 with the sidechain-ready settings above (it currently compresses the bass by itself, gently); Pro-C 3 removed; route 8 to 6 left at level 0. Options for phase 3: automation clip on insert 6 volume (`AutomationTarget.mixer_volume(6)`) dipping about 3 dB on each kick, or the user flips the route to sidechain in the GUI (one right-click), after which the limiter works as written.

## Textures / vocal audio edits (Python `wave`, 24-bit, no numpy in FL's Python)
- `Samples/Tex_Roomtone_Suburb-8bars.wav`: first 19.2 s (8 bars) of the 70 s file, 1 s fade-in, 2.5 s fade-out; swapped into channel 17 with `replace_channel_sample`. The two Tex Roomtone clips (bars 1 and 49) now end exactly at bars 9 and 57.
- `Samples/Vox_E8_everywhere_112_Gbm-reversed.wav`: last 4.8 s (2 bars) of the vocal loop reversed, 0.4 s fade-in, 0.15 s fade-out. Channel 19 "Tex Reverse Vox" to insert 21 to Textures. Pattern 40 "Tex Reverse" (1 note, 2 bars). Clips on new playlist track 11 "Tex Reverse" at bars 31-32, 79-80, 95-96 (ticks 11520, 29952, 36096; 768 long) = swells into both chorus downbeats and the outro.
- Vox stretch: no Sampler time-stretch op; the loop stays at 112 BPM (only heard in the drum-less interlude bars 49-56). Logged.
- Pattern 39 "Vox Double" (channel 20, same two notes as Vox Interlude) on new track 12 "Vox Double", clip at tick 18432 length 3072 (same as the Vox clip).

Totals now: 21 channels, 40 patterns, 1198 notes, 79 clips (75 + 3 reverse + 1 double). Arrangement untouched otherwise.

## Gain staging (estimates; nothing measured, renders are forbidden in this phase)
Trims in dB-exact plugin gains: Lead -6, Lead Oct -12, Lead Wash -10, Pads -8, Bass -4, Sub -6, Drum Bus -5 (PEQ 2 main), Textures -12 (PEQ 2 main), Vox -8, Vox Double -12, Reverse -8. Intended peaks: drums about -8, pads -12, lead -10, bass -8, textures -20, vox -14 dBFS, mix about -6. Phase 4 must measure and correct; no limiter anywhere.

## Automatable parameters for phase 3 (track/slot/index, all verified writable)
- Pad LP cutoff: insert 4 slot 0 index 48 "Band 3 Frequency" (and insert 5 slot 0 index 48); Pro-Q 4 mapping v = ln(Hz/10)/ln(3000) (0.6 = 462 Hz, 0.7 = 2.7 kHz, 0.8 = 6.0 kHz, 0.85 = 9.1 kHz). GMS generator cutoff (channel 4 param 32) is the alternative.
- Lead filter: insert 1 slot 0 index 48 "Band 3 Frequency" is a shelf; for a sweep use Hyper Chorus LP index 7 on 1/1 (0.5 = 3963 Hz, 0.7 = 7867 Hz) or add a Pro-Q 4 High Cut band 4 (index 69 used / 71 freq / 74 shape).
- Reverb wet: Pro-R 2 "Mix" index 9 on 1/3, 2/2, 3/2, 21/1, 22/1 (linear %); Reeverb 2 "Wet level" index 12 on 4/2, 5/2, 16/2, 17/4 (125 v %).
- Delay send: mixer send levels have no automation target; use Delay 3 "Output wet" index 23 on 1/4, 4/3, 5/3 (linear %) or the Delay Send insert volume (`AutomationTarget.mixer_volume(24)`).
- Texture volume: `AutomationTarget.mixer_volume(16)`, or PEQ 2 "Main level" 16/0 index 35 (dB = 36 v - 18).
- Drum bus filter: 15/0 index 12 "Band 6 freq" (Low pass; v = ln(Hz/20)/ln(1000): 0.8 = 5 kHz, 0.6 = 1.26 kHz, 1.0 = 20 kHz).
- Love Philter cutoff: 17/5 index 9 "Filter 1 - Cutoff frequency" (100 % = open), resonance index 10.
- Width: Pro-R 2 "Stereo Width" index 7 on 3/2 (120 v %); Fruity Stereo Enhancer 3/4 index 2 (0.5 original, 0.3 = 40 % separated); Hyper Chorus mix 1/1 index 9.
- Sidechain emulation: `AutomationTarget.mixer_volume(6)`.

Automation link event ids were not read this phase (no clips created); phase 3 gets them from `fl.automation.create(...)`.

## Persistence
`records/phase-2-state-v006.json` (captured before close) vs `phase-2-state-v007.json` (after `fl_project_start(v007, source v006)`): empty diff over 25 insert effect lists + sends, 53 parameter groups (every setting above), mixer pans, 21 channels (route/volume/pan/pitch), 40 patterns / 1198 notes, 79 clips. Session left open on v007.

## Friction logged this phase (8 entries under "2026-09-14 - Parking Lot Moon")
`^b^a` parameter-name prefix; send readback exists and unity is 0.8; no Sampler stretch/trim/fade ops; Delay 3 duplicate "Distortion" names; `set_verified` false when already at value; no sidechain routing (Fruity Limiter / Pro-C 3); mixer-volume dB curve undocumented; unlicensed Super VHS hung a request (retry after sign-in worked).

## Open questions
- Nothing has been heard: the chorus/reverb amounts, the -12 dB texture trim and the vocal pitch offset (raw 5, units unverified) need one listening pass in phase 3 or 4.
- Phase-1 taste risks remain (Serum `LD - Lush and Vintage` mono, `KY - Echoes Of The Past` has its own reverb under Pro-R 2).
- Sidechain: choose automation emulation or GUI route flag.
- The vocal loop is at 112 BPM; a GUI stretch to 100 BPM (Sampler > Time > tempo) would tighten the interlude if wanted.
- Love Philter sits in line (open); if phase 3 does not automate it, remove it to keep the chain clean.
