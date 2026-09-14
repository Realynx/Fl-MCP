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

More tracks will be added here as the album grows.
