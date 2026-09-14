# Album: music made through FL MCP

Every track here was composed, revised, mixed and mastered by an AI agent working only through
FL MCP and the embedded Python API. No human touched the FL Studio interface during production.
The tracks exist to exercise the MCP against real production work: each listener request became
a small revision, each revision was verified for persistence and measured after rendering, and
the friction those revisions surfaced became the next batch of SDK fixes.

## 1. Ember Tides

Future bass · F minor · 140 BPM · 3:20 · finished 2026-09-14

<audio controls preload="none" style="width: 100%; max-width: 640px;">
  <source src="https://github.com/Realynx/Fl-MCP/raw/master/examples/ember-tides/Ember-Tides-final-master.mp3" type="audio/mpeg">
  Your browser does not support the audio element.
</audio>

- [Download the MP3](https://github.com/Realynx/Fl-MCP/raw/master/examples/ember-tides/Ember-Tides-final-master.mp3) (320 kbps, -14 LUFS integrated, -1 dBTP)
- [Project file](../examples/ember-tides/Ember-Tides.flp) (FL Studio 2026; Serum 2 and FabFilter plugins; commercial drum samples not included)
- [Revision history](../examples/ember-tides/README.md) · [verification records](../examples/ember-tides/records)

### How it was made

The song started as a generated sketch of 2,979 notes across 122 patterns and grew through
eighteen revisions, each one answering a specific listening note from the producer. A few of
the turns show what working through an MCP actually looks like:

- **The right-sided lead (v006).** Three revisions had failed to centre a lead that every
  parameter readback claimed was centred. An isolated render proved the left channel was
  silent, and a signed-versus-unsigned mismatch in the mixer pan scale turned out to be the
  cause. The fix shipped as an SDK change the same day, with the render as evidence.
- **Choosing sounds by ear and by number (v007).** Eight candidate Serum voices were
  auditioned in a single 64-bar render, one channel per candidate over the same chorus, then
  measured for width and brightness before the producer picked. A factory preset loaded by
  name became the chorus chord stack; the lead was authored from a description with the
  Serum patch builder.
- **Arps that hurt (v008 to v009).** The first arpeggio voice added 7 dB in the 2-4 kHz band.
  The follow-up measured that band against the previous revision, swapped in a warm authored
  pluck an octave lower on its own reverb insert, and brought the band back to within a
  decibel of where it had been.
- **Not repeating ourselves (v008).** Four identical chorus motifs and three identical verse
  motifs were varied with diatonic mirror, displacement, retrograde and third-shift transforms.
  Drums got fills only at phrase ends. The fade-out ending became a real cadence, later
  foreshadowed in the break so the ending returns to something already heard.
- **A tape-stop that was verified (v011).** The final held note bends down two octaves as the
  mix fades. A pitch track of an isolated render confirmed the two-octave glide over 3.4 seconds
  before the change was documented.
- **Scan before you master (v016 to v018).** A per-bar scan of full, high-band and sub-band
  level found every problem the producer reported and two he had not: a sub patch that ignored
  velocity, a riser opening into white noise, fill bars spiking 6 dB. The master was retargeted
  to -14 LUFS for streaming, and a chord stutter that read as an out-of-place triplet was
  removed at the drop.

### What the MCP had to do

Reading and writing notes, clips, channels, mixer routing and automation; loading factory
Serum presets by name; generating presets from parameters and reading the live plugin state
back; auditioning candidates in disposable projects; rendering; and measuring the audio
afterwards. Everything ran through `fl_execute_python` with the FruityLink helpers, which let
one request make dozens of coordinated edits.

### What it taught the SDK

Twenty-plus entries in the [friction log](../MCP-FRICTION-LOG.md) came from this track,
and most were fixed in batches between revisions: the mixer pan scale, marker deletion,
editable automation endpoints, verified parameter writes, Serum preset loading and the patch
builder, a native read-state operation, and the naming and documentation gaps an agent hits
when it codes against a catalog instead of the installed package. The [verification
records](../examples/ember-tides/records) hold the measurements behind every claim above.

## 2. Parking Lot Moon

Dream-pop / trip-hop / dark synthpop · F# minor · 100 BPM · 4:19 · finished 2026-09-14

<audio controls preload="none" style="width: 100%; max-width: 640px;">
  <source src="https://github.com/Realynx/Fl-MCP/raw/master/examples/parking-lot-moon/Parking-Lot-Moon-final-master.mp3" type="audio/mpeg">
  Your browser does not support the audio element.
</audio>

- [Download the MP3](https://github.com/Realynx/Fl-MCP/raw/master/examples/parking-lot-moon/Parking-Lot-Moon-final-master.mp3) (320 kbps, -13.9 LUFS integrated, -1 dBTP)
- [Project file](../examples/parking-lot-moon/Parking-Lot-Moon.flp) (FL Studio 2026; Serum 2, GMS and FabFilter plugins; commercial samples and vocal loop not included)
- [Revision history](../examples/parking-lot-moon/README.md) · [phase and verification records](../examples/parking-lot-moon/records)

### How it was made

The song was built to a friend's written brief (dreamy, melancholic, nocturnal; extended
voicings with one unresolved tension note; a motif hidden in fragments until the chorus; soft
submerged drums; textures that sound like damaged memories) in five phases, each closed with a
capture-close-reopen persistence check, followed by two listening revisions from the producer.
Nothing was heard until the first full render at the end of phase 3, so most of the decisions
were made by measurement:

- **The silent pad (v009 to v010).** The first full render came in at -22.8 LUFS with the intro
  bed at -50 dBFS. A per-bar band scan showed the GMS pad's harmonics at -70 dB: an instance
  authored purely through parameters had no oscillator waveform to play, because GMS keeps its
  waves in GUI-only files. Loading the factory `.gmsynth` preset through the native state
  operation and reopening the Pro-Q high cut brought the 150-400 Hz band up 18 dB.
- **Textures 50 dB too quiet (v010).** Hiss, crackle and room tone measured about -80 dBFS in
  the mix. The Splice files are quiet to begin with, and FL's channel-volume scale turned out to
  be a power curve: raw 3200 is about -29 dB, not the -12 dB assumed. Channel volumes, per-insert
  EQ gains and the texture bus were re-staged from measured numbers, with the bus low-passed at
  8 kHz so the tape bed stayed subtle.
- **Sidechain without a sidechain.** No API flags a mixer route as a sidechain, and neither
  Fruity Limiter nor Pro-C 3 exposes a source, so the bass duck is a computed automation clip:
  162 kick hits were read from the drum patterns through the playlist and turned into a
  488-point curve on the bass insert's volume (-3 dB at each hit, 150 ms recovery). The stem
  masking report in the records shows why it matters: bass and kick share only the 45-90 Hz zone.
- **The out-of-key apex (v014).** The producer heard a wrong note at the chorus climax. Every
  chorus lead pattern was read back with note names and checked against the chord per bar; the
  offender was a G natural at the apex of the third-shift motif variant, landing on the Dmaj7
  bar at bars 42 and 90 and doubled in the octave and wash layers. Six notes moved to F#, and a
  re-read confirmed no G remained. The same pass cut the intro fragment by about 6.5 dB with a
  new Pro-Q output automation, since the Serum patch barely responded to velocity.
- **The vocal chop (v014 to v015).** The second vocal entry was sliced into eight pieces in
  Python on the loop's beat grid, loaded as eight sampler channels on the vocal insert and
  re-sequenced. The first version was too dense and 6-9 dB too loud, and the producer heard the
  original "still playing like a delay"; a muted-chop render proved it was the overlapping slices
  themselves. v015 thins it to 17 hits with five sparse octave-down hits, and bars 53-56 sit
  within 2-7 dB of the original entry.
- **An 81 GB cache the producer found.** Midway through mastering the producer noticed
  `plugin-shadow` had grown to 772 copies of the plugin folders. The host shadow-copies about
  410 MB per FL launch and only cleaned up on a code path that never runs when the MCP closes
  FL. It became a friction entry and then an SDK fix (owner markers and a sweep on host start).

### What the MCP had to do

Building 25 premium and stock effect chains with every value written by `set_verified` and read
back as the plugin's own display string; loading Serum factory presets by name and a GMS preset
as native plugin state; slicing and reversing samples in pure Python inside FL's embedded
runtime and swapping them into channels; 17 automation clips on FabFilter, stock-effect and mixer
targets, including calibrating undocumented scales (Pro-Q frequencies, Reeverb 2, Pro-L 2 gain
and ceiling) before writing; section, stem and range renders to check single changes cheaply;
and offline measurement of every render (per-bar and per-band levels, loudness, true peak,
stereo correlation, masking) before anything was called done.

### What it taught the SDK

The [friction log](../MCP-FRICTION-LOG.md) section for this track holds 37 entries, most of them
logged and worked around rather than fixed mid-song. Their themes: plugin state that could not be
read until the project was closed and reopened; no sidechain routing, no send-level automation
target, no sampler stretch, trim or reverse operations, no channel delete and no automation
inventory; undocumented scales (channel volume, mixer volume, Reeverb 2, Pro-L 2, automation
tension direction) and parameter names with a `^b^a` prefix; readback timing after seeks and a
tempo that reads as the template's for a moment after a resume; a full render that died mid-way
and left a partial file in the output path; GMS rendering silence when authored by parameter;
and the unpruned plugin shadow copies. The [records](../examples/parking-lot-moon/records) hold
the measurements behind every claim above.

More tracks will be added here as the album grows.
