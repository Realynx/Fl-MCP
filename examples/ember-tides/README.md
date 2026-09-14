# Ember Tides

**Final master:** [Ember-Tides-final-master.mp3](Ember-Tides-final-master.mp3) (3:20, -14 LUFS integrated, -1 dBTP).
**Project file:** [Ember-Tides.flp](Ember-Tides.flp) (FL Studio 2026; Serum 2 and FabFilter plugins; the drum samples
are commercial and not included, so those channels load silent without them). Verification records for every
revision, the issue log and the live-validation record are in [records/](records/).

`Ember Tides` is an evolving original reference composition for exercising FL MCP against one continuing FL Studio project. It is in F minor at 140 BPM and currently spans 112 bars, or 192 seconds before effect tails. The goal is to revise the same musical work in response to listening and user feedback while using each revision as an engine persistence test.

This directory is a documentation baseline for a possible future GitHub example. The example is not published yet and currently contains no project file, raw samples, presets, binaries, or rendered audio.

## v005: centered melodic lead and drop balance

Inspection found Timeless 3's `Ping Pong R-L` mode on the lead's synced
quarter-note delay. FL's channel/mixer pan and Serum's oscillator pans read
centered. v005 disables ping-pong and sets that delay's wet mix to 0%. A later
isolated audition still measured a strong right-channel imbalance, so these
parameter changes do not establish that the rendered lead is centered.

Six lead patterns now contain 132 grid-aligned F-minor melody notes instead of 40
sparse answers. Chorus II uses three connected variations. Forty drop-snare hits
keep their timing and move from velocity 111 to 88; verse/build drums are unchanged.
The project now contains 2,480 notes, 127 patterns and 132 clips. Captured state
matched after save-close-reopen, and the 192-second render measured -16.0 LUFS,
-1.0 dB true peak and 10.4 LU loudness range. A local 27.43-second Chorus II preview
was rendered for listening; rendered audio remains outside this repository.

An optional `fruitylink-serum` package was also exercised inside FL's embedded
Python runtime. Its read-only index returned nine locally installed presets whose
metadata mentions future bass, and its deterministic audio-description path passed
synthetic centered-stereo and silence checks. The host still exposes no safe preset
filename loader, so this revision does not claim that metadata search auditioned or
loaded those presets. See the [v005 verification record](records/verification-v005.json).

### Installer 0.1.29 integration check

Serum support is now a default-selected, separately deselectable installer
component. A live installed MCP session imported `fruitylink_serum` directly from
the shared embedded Python extension directory, queried the local preset index,
rendered an isolated copy of the existing lead, and analyzed eight seconds of that
render. Reopening v005 preserved all 2,480 notes; the source FLP hash was unchanged.

The audition retained the lead mixer and master effects. Its left/right RMS levels
were approximately -179.8/-33.3 dBFS, despite centered pan readbacks. Its WAV also
lasted 178.3 seconds after the disposable Playlist was reduced to a 32-beat clip
(13.7 seconds at 140 BPM). The causes of the imbalance and extra render duration
remain unverified. These observations are engine/mix investigation targets, not a
new song revision or evidence of successful factory-preset loading.

## v004: sound design and thematic development

Feedback asked for richer bass/lead timbres and less repetition. The revision keeps the clean sub and adds a separately routed square/saw mid-bass layer, with Saturn 2 saturation and per-note filter automation. The lead now blends a confirmed square oscillator with a quieter detuned saw, revised envelopes, saturation, and evolving brightness. The pluck uses a confirmed triangle oscillator with a quieter saw component.

The melodic parts were reduced from 436 to 177 notes: verse fragments alternate with rests, the second chorus introduces a different phrase, the lead answers the pluck, and the outro recalls a shortened theme. One hundred mid-bass notes are new, bringing the project to 2,388 notes, 127 patterns and 132 clips. All 102 prior patterns outside the motif/answering lead retain their note data. Captured notes, channel/clip state, automation, Serum controls and Saturn controls matched after save-close-reopen.

This exposed a sound-design boundary: the host parameter API reaches oscillator shape, levels, envelopes, filter controls and exposed effects, but the SDK has no explicit preset/wavetable-loading operation or enum-choice metadata. Serum waveform choices in this pass were verified using returned display values. Host automation supplied filter motion; this is not a claim of access to Serum's internal modulation routing.

A Sol sub-agent added `Parameters.set_named(name, value)` to the shared Python SDK. It resolves an exact unique parameter name across pages and returns the written index; missing or ambiguous names cause no write. Resolution and write are separate operations, and readback is a separate query. It avoids copying numeric indices between plugins but does not solve enum discovery or preset loading.

Installer 0.1.28 was installed and the named setter was verified through the installed MCP against Saturn's `Band 1 Drive`: index 11, normalized 0.42, separate display readback 42.0%. A missing name was refused without changing that control. All 2,388 notes survived the installed-version reopen. The [v004 verification record](records/verification-v004.json) records the 192-second render at -15.5 LUFS integrated and -1.0 dB true peak. The Python gate passed 246 tests and the installer Release build completed without warnings or errors.

## v003: groove revision from listener feedback

The listener reported irritating build drums and parts that did not groove together. Inspection found authoring choices behind those complaints: bass offsets of roughly a sixteenth note, staggered chord attacks, free-running 350 ms delays, repeated 0.857-second snare samples, and 20 exact doubled hi-hat hits where fills overlapped the regular pattern. The saved positions matched the original authoring; this pass does not establish a clock or MIDI-persistence fault.

The v003 edit reduces each eight-bar build from 108 snare triggers to 11, lowers build accents, aligns kick/bass/chord accents, simplifies melodic offsets, and sets both Timeless instances to quarter-note sync with a 100% time offset. Delay mix and feedback are reduced. The edits retain all 122 pattern IDs and 125 clip placements, with 2,547 intentionally retained notes.

The revised notes and captured channel/clip/automation/delay/envelope state passed save-close-reopen comparison. The full 192-second render completed without recovery warnings, measuring -15.5 LUFS integrated, -1.0 dB true peak, and 10.8 LU loudness range. The [v003 verification record](records/verification-v003.json) separates intentional note reductions from persistence checks. These measurements do not replace a listener's judgment of the revised groove.

Duplicate addressing exposed a workflow limitation: `NoteRef` / `NoteEdit` identify notes by channel, key, and start tick, so a target can match multiple notes. An edit-count assertion caught that case. Recovery inspected the partial result, consolidated the explicitly identified hi-hat duplicates, and verified every retained note against the revision plan. It did not rerun initial composition or recreate the patterns. Duplicate-aware inspection and unambiguous individual-note editing are follow-up SDK improvements; they are not implemented by this musical revision.

## Verified v002 baseline

The current live project contains 2,979 authored notes across 122 patterns and 125 Playlist clips, including three automation clips. It has 16 channels, one of which is the unused default Sampler. Its instrument palette includes five custom Serum 2 voices: velvet chords, pluck, sine sub, answering lead, and pad. Seven Splice drum sources provide the drum palette. The mixer uses FabFilter Pro-R 2, Timeless 3, Pro-C 3, and Pro-L 2.

A save-and-close followed by a restart from that exact snapshot preserved all 2,979 notes, channels, clips, automation, mixer state, and the inspected Serum parameters. Normalized JSON captured before close and after restart was byte-for-byte equal, and the lifecycle operations returned no warnings.

The final v002 project rendered successfully as 192 seconds of 48 kHz stereo floating-point WAV audio (73,728,950 bytes). FFmpeg EBU R128 analysis measured -14.8 LUFS integrated loudness, -1.0 dBFS true peak, and 11.3 LU loudness range. The outro reaches approximately -90 dBFS, consistent with the intended fade.

| Revision | Integrated loudness | Peak | Change under test |
| --- | ---: | ---: | --- |
| v001 | -41.9 LUFS | -27.3 dBFS | Initial mix using the incorrectly documented raw mixer-volume assumption |
| Controlled Master-only render | -30.1 LUFS | -15.5 dBFS | Changed only the Master raw volume for scale comparison |
| v002 | -14.8 LUFS | -1.0 dBFS true peak | Revised mixer levels and added the outro fade automation |

The revisions changed levels and added one fade automation clip; they did not rebuild or alter the 2,979 authored notes or 122 patterns. The final v002 reopen also matched the inspected FabFilter parameters. The [verification record](records/verification-v002.json) records the measured baseline and its limits. Three-second verse and chorus measurements ran through the embedded Python analysis API; no over-full-scale samples occurred in those measured sections.

During that calibration, one embedded Python request set the Master mixer volume to `12800` and immediately read back the preceding value, `6800`; a separate following request read `12800`. This is one observed delayed readback, not evidence that every setter behaves this way. Until the settling semantics are characterized, verify critical mixer writes in a later request instead of treating same-request readback as authoritative.

## Revision workflow

Treat each pass as another revision of the same FLP lineage:

1. Start a new working path from the latest exact snapshot with `fl_project_start`, setting `sourceProjectPath` to that snapshot and `background=true` when a private-desktop session is appropriate.
2. Inspect status, channels, patterns, notes, Playlist clips, mixer state, automation, and the parameters relevant to the planned change before editing.
3. Apply only the requested musical revision, then inspect the affected state. User feedback drives the composition; the before-and-after inspection also exercises the engine contract.
4. Save the revision to a fresh snapshot path with `fl_project_save`, or use `fl_project_close` with a fresh snapshot path when ending the disposable session.
5. Restart from the saved snapshot into another fresh working path and compare the same semantic state before continuing.
6. Render only to a fresh output path, and record render verification separately from project-state verification.

Paths supplied to lifecycle tools are workspace-relative and must be new. See [Getting started](../../docs/getting-started.md), [Sessions, configuration and recovery](../../docs/sessions.md), and the [MCP tool reference](../../docs/tools.md).

Never rerun the authoring script to repair an unexpectedly low live query count. Note and clip additions append, so doing so can duplicate music. First finish pagination, compare per-pattern and per-channel counts, verify that the intended snapshot was resumed, and inspect or render the preserved project before deciding that project data is missing.

## Sample discovery boundary

The live installed-sample query returned at most 150 results and did not include the user's `Documents/Splice` collection. That location was therefore confirmed unavailable through the current SDK sample query. A missing search result must not be treated as evidence that a sample is absent from the machine; sample staging or selection outside that bounded query remains a separate workflow.

## Distribution status

Licenses for the project dependencies and assets have not been reviewed. Sample and preset distribution must be handled separately before any publication. This repository baseline makes no statement about what may be redistributed.

## v006: centred lead, lead presence, richer chords

The listener reported that the square lead entering near 1:20 sat in the right channel
and did not read as a melody, and that the chords sounded merely acceptable. Inspection
found the lead's mixer track pan stored as 6800 on a scale whose centre reads 0, past
hard right; the setter's "6400 = center" note applies to channel-rack pan only. Setting
the mixer pan to 0 and re-rendering the isolated lead measured left/right RMS within
0.04 dB, versus a silent left channel before. Three other tracks had the same authoring
error and were corrected the same way.

The lead gained level, unison and sustain, and its filter automation was raised. Chords
gained wider unison, a saw sub-oscillator one octave down, a brighter cutoff curve and a
seventh in every chorus stab that lacked one (60 new notes). The project now holds
2,540 notes, 127 patterns and 132 clips; captured state was byte-equal after
save-close-reopen. The 192-second render measured -15.1 LUFS, -1.0 dBTP and 7.1 LU.

The 178-second audition was explained: FL extends the song to the last time marker
(bar 105), and the SDK cannot delete markers. See the
[v006 verification record](records/verification-v006.json) and [issues-v006.md](records/issues-v006.md).

## v007: preset selection and an authored lead

The first revision to use preset loading and the patch builder. Eight candidate voices were
auditioned in a single 64-bar render by adding one Serum channel per candidate, loading each
preset by name or from SerumPatch, copying the chorus chord or lead notes to new patterns and
placing them back to back over the chorus rhythm section. Measurements per block (stereo width,
spectral centroid) plus listening guided the choice.

The choruses gained a second chord layer playing the factory "Clean Future Bass Chords" patch,
while the original velvet chords stay everywhere so the filtered intro survives; the lead is an
authored square patch. A rejected draft showed why the layer approach was needed: that factory
patch routes its oscillators through Serum's FX buses, so FL automation on the voice-filter cutoff
no longer shaped it. Captured state was byte-equal after reopen (decoded Serum states included).
Render: -15.0 LUFS, -1.0 dBTP, 7.3 LU. See [verification-v007.json](records/verification-v007.json).

## v008: drop arps, drum variation, cadence outro, lead audibility

Listener requests: fast arpeggiated plucks that come in louder on the drops, drums that change in
places, a lead that was hard to hear near two minutes, an outro that ends with a proper cadence
rather than a fade, and less literal repetition of motifs. All were done through embedded Python:
a factory pluck preset loaded by name onto a new channel after a three-voice audition render,
sixteenth-note arpeggios generated from each chorus's chord tones with a different figure per
chorus, five small drum-variation patterns placed at phrase ends and through specific sections,
velocity edits plus a preset-level and mixer lift for the Verse II lead, diatonic mirror /
displacement / retrograde / third-shift transforms of the duplicated motif patterns, and a rewritten
outro (iv7 - Vsus4 - V7 - i) with a sub line and shortened fades.

State was byte-equal after reopen including decoded Serum states. Render -13.7 LUFS, -1.0 dBTP,
9.1 LU, 197 s. See [verification-v008.json](records/verification-v008.json).

## v009: softer drop arps

The v008 arps were reported as harsh. Band measurements of Chorus I against v007 showed +7.4 dB
in the 2-4 kHz band and +4.8 dB above 5 kHz. The arp voice was replaced by an authored triangle
pluck with a 2.4 kHz low-pass, the arps were regenerated an octave lower with gentler velocities,
and the channel moved to its own insert with only a light reverb. Both bands returned to within
about 1 dB of v007 while keeping the arps. State byte-equal after reopen; render -14.8 LUFS,
-1.0 dBTP, 7.4 LU. See [verification-v009.json](records/verification-v009.json).

## v010: cadence foreshadowed, outro filled out

The listener liked the v008 cadence but felt it appeared from nowhere and that the ending was
thin. The suspended interlude now states the same iv7 - Vsus4 - V7 - i progression as a break,
with a sub line and the closing motif, so the ending returns to a known idea. The outro gained
the chorus stack, mid bass, the lead doubling the closing line, soft arps running into the tonic,
kicks on the two arrivals and a fuller pad. Presence bands of the outro stay about 4 dB below the
accepted chorus. State byte-equal after reopen; render -14.7 LUFS, -1.0 dBTP, 5.8 LU.
See [verification-v010.json](records/verification-v010.json).

## v011: ending polish, richer lead, wider field

Requests: drop the reverse cymbal at the end, make the fading final note bend down, make the lead
waveform more interesting, and widen the stereo field. The bend was done with a new automation
clip on Serum's pitch-bend parameter after baking a 24-semitone range into the lead preset; an
isolated render's fundamental track shows a two-octave descent over 3.4 s. The lead was rebuilt
with seven-voice square, pulse and saw layers plus Serum hyper and chorus, and a factory pad was
layered at low level as sustained upper chord tones, lifting chorus side energy by about 2 dB
without changing presence bands. State byte-equal after reopen; render -14.2 LUFS, -1.0 dBTP.
See [verification-v011.json](records/verification-v011.json).

## v012: the wide pad sits under the lead

The new wide pad clashed with the lead. The pad patterns in the lead sections were moved down an
octave and a full-song channel-volume automation clip ducks the pad by about a third during the
lead phrases with one-beat ramps, leaving the break untouched. The overlapping 500-1600 Hz band
dropped about 1.5 dB in those sections and the interlude is unchanged. State byte-equal after
reopen; render -14.4 LUFS. See [verification-v012.json](records/verification-v012.json).

## v013: interest in the sparse sections

The listener wanted more happening where the lead is absent, while keeping negative space. Six
short figures were added only inside gaps: pluck answers at phrase ends in the intro and Verse I,
a soft sub swell and a faint high shimmer in the second half of the intro, a low rising arp in the
last four bars of each build, and a lead pickup in the bar before the lead's first entrance. The
intro rose about 2 dB; the verse and build levels are unchanged because the additions live in
rests. State byte-equal after reopen; render -14.3 LUFS. See [verification-v013.json](records/verification-v013.json).

## v014-v015: stylized details, mix check and final master

Final pass before mastering: a filtered-noise riser with its own cutoff automation into each drop,
a 32nd-note chord stutter on the last beat before each drop, a sidechain-style pump on the chord
bus through the choruses (per-beat volume automation), a delayed pluck sparkle opening the break,
and an end marker that leaves two seconds of silence after the final tail. The mix needed no track
changes: low-end energy measured within 2 dB of the whole. Mastering is limiter gain staging only
(Pro-L 2, +7 dB, -1 dBTP ceiling), giving -10.1 LUFS integrated, 4.7 LU, -1.0 dBTP. Deliverables
are a 320 kbps MP3, a dithered 44.1 kHz/16-bit WAV and a lossless FLAC, kept outside this
repository. Both project states were byte-equal after reopen.
See [verification-v015-master.json](records/verification-v015-master.json).

This completes the first album track of the MCP test series; each track follows the same pattern
of small listenable revisions with persistence and render verification.

## v016-v017: scan-driven fixes and the -14 LUFS final master (supersedes v015)

The listener heard noise around the first drop and a sub that entered far too loud in the intro,
and asked for a Spotify-style -14 LUFS target. A per-bar scan of the master (full, >4 kHz and
<90 Hz band RMS per bar, flagging steps over 6 dB) found all of it: a velocity-insensitive sub
patch at full level from bar 5, the noise riser peaking at -31 dBFS above 4 kHz into the drop, and
6 dB high-band spikes on every fill bar. The sub swell was removed, the riser rebuilt darker and
quieter with a lower sweep ceiling, the stutter and fills tamed, and the limiter set to +2.4 dB.
The re-scan shows no full-band jumps; the master measures -14.0 LUFS, 6.0 LU, -1.0 dBTP.
See [verification-v017-master.json](records/verification-v017-master.json).

## v018: smoother drop entries (final master)

The chord stutter before each drop read as an out-of-place triplet burst and was removed; the
chord-bus pump now lets each drop's downbeat through at full level before it starts dipping.
The last beat before each drop is about 6 dB quieter and each downbeat about 1 dB louder than
v017, with loudness unchanged at -14.0 LUFS, 6.0 LU, -1.0 dBTP. This is the final Ember Tides
master for the album example. See [verification-v018-master.json](records/verification-v018-master.json).
