# Ember Tides v006 — MCP / SDK findings

Session date 2026-09-13. Installed components: FL MCP server with fruitylink_python 0.2.0
embedded wheel, installer 0.1.29, Serum 2. Each item lists the call, expected vs actual,
reproduction and evidence, and is classified as caller mistake, SDK limitation or confirmed bug.

## 1. Mixer pan scale documented incorrectly (confirmed bug, root cause of the right-sided lead)

- Call: `fl.ops.set_mixer_pan(track=4, value=...)` / `fl.ops.get_mixer_pan(track=4)`.
- Expected (per docstring and API catalog): "Mixer track pan 0..12800 (6400 = center)".
- Actual: untouched mixer tracks read 0; the lead track read 6800. Isolated audio with
  6800 was hard right (L -179.8 dBFS / R -33.3 dBFS). After writing 0 the same isolated
  audition measured L -36.64 / R -36.68 dBFS. Channel-rack pan (`get_channel_pan`) does
  use 0..12800 with 6400 centre, which is where the confusion came from.
- Probe: writes of -6400 and -7000 read back 0; 6400 and 7000 read back unchanged.
  Negative values are clamped, so the setter cannot pan a mixer track left at all.
- Repro: read `get_mixer_pan` on a fresh track (0), write 6400, render, measure L/R.
- Evidence: `Ember-Tides/audition-v006-lead-centered.wav`, verification-v006.json.
- Impact on v005: three more tracks (pluck 5900, closed hat 7300, open hat 5400) were
  authored against the wrong scale and were all sitting right of centre. v006 resets those
  mixer pans to 0 and puts the intended small offsets on channel pan instead.
- Fix needed: document the real mixer scale, accept the native left range, and make the
  channel/mixer pan scales consistent or clearly distinct in the catalog.

## 2. Audition render length: time markers extend the song (caller assumption + SDK gap)

- Observation: playlist reduced to one 32-beat clip still renders 178.286 s.
- Evidence: `fl.transport.state_text()` returned `songLength=48 bars playRange=[0..39935]`
  with only the bar 41–48 clip present. Tick 39936 is the "Outro - afterglow" marker at
  bar 105; 104 bars at 140 BPM is 178.286 s. All five automation clips span 112 bars, so
  they were not the cause. Full-song render is not a renderer bug.
- SDK gap: `list_markers` and `add_marker` exist; there is no `delete_marker`, so a script
  cannot shorten a marker-extended audition. Recommend adding marker deletion (and/or a
  render range option) so isolated auditions cost 14 s instead of 3 minutes.

## 3. API catalog and installed Python package disagree (SDK limitation)

- `fl_python_api` lists `query_mixer_tracks`; `fl.ops.query_mixer_tracks()` raises
  `AttributeError` in the installed fruitylink_python 0.2.0 (`list_mixer_tracks` exists).
- The repository source has `automation_records.AutomationPointSpec` and
  `AutomationCurve.set_points`; the installed wheel has only `add_beats`/`add_ticks`/`delete`.
- The catalog prints camelCase argument names (`channelOrTrack`, `paramIndex`); Python
  requires snake_case. `get_channel_name` takes `index=` while every other channel getter
  takes `channel=`.
- `PluginParameterInfo` fields are `display_value`/`raw_value`, not the catalog's
  `displayValue`/`rawValue`.
- Consequence: an agent that reads the catalog and codes against it fails several times
  before discovering the installed surface. Ship the same generated names in both places
  or have `fl_python_docs` state the installed wheel version and naming rules up front.

## 4. Automation endpoints are protected (SDK limitation, undocumented)

- `fl.automation[13].delete(18)` (last point) raised
  `operation_failed: Automation operation refused: invalid-points-or-protected-endpoint`.
- Workaround used: delete/re-add interior points from the end backwards, leave points 0
  and n-1 untouched, verify with `list()`. Both curves matched their targets.
- Recommend a set-value-at-index operation so endpoints can be edited without deletion.

## 5. Parameter display readback lags rapid same-request writes (confirmed, intermittent)

- Six consecutive writes to chords `Sub Shape` (index 199) with an immediate
  `query_plugin_parameters(offset=199, limit=1)` after each returned
  Sine, Sine, Sine, Saw, Saw, Saw, and a final write of 0.0 read back "Pulse" in the same
  request. A read at the start of the next request returned "Sine" (correct). A single
  write per parameter followed by a read in the same request was consistent every time.
- Repro: write one enum parameter several times in one `fl_execute_python` call, reading
  between writes.
- Guidance that held: write once, verify in the next request.

## 6. `query_plugin_parameters` filter with a space returns nothing (CORRECTED: caller mistake)

- `filter="Sub Shape"` returned an empty page; `filter="Uni"`, `"Pan"`, `"Level"` work.
  Reading by `offset=<index>, limit=1` is the reliable way to target one parameter.
- Correction after reading the query implementation: the filter is a plain case-insensitive
  substring match and handles spaces. `limit` bounds the raw slots scanned from `offset`, and
  the failing call used `limit=8` while `Sub Shape` is slot 199; the earlier "Osc"/"Env"/"Fil"
  misses used `limit=64` for slots 202+. The documented contract already says so. The SDK now
  adds `Parameters.read(index)` and restates the scan-window rule in docs/python/api.md.

## 7. Parameter enum mappings discovered (reference, not bugs)

- Serum unison count: normalized = (n-1)/15 (3 → 0.1333, 5 → 0.2667, 7 → 0.4).
- Serum unison detune: display ≈ normalized² (0.224 → "0.05", 0.316 → "0.10").
- Serum Sub Shape: 0.0 Sine, 0.4 Triangle, 0.6 Saw, 0.8 Square, 1.0 Pulse (0.2 also read Sine).
- Serum Sub Octave: 1/3 → "-1 oct". Env sustain 0.72 → "-5.7 dB", 0.85 → "-2.8 dB".
- Filter cutoff normalized v ≈ 8 Hz × 2756^v (0.42 → 226 Hz matched the readback).

## 8. Oversized Python results (caller-side, worth a note)

- A result over the MCP display budget is written to a tool-results file rather than
  returned; large dumps should be written to disk inside FL and summarised in `result`.
- A raised exception discards the whole `result` dict, including readings gathered before
  the failure. Wrap steps in try/except and record errors in the result.

## 9. `fl_project_start` schema lacks `background` (documentation drift)

- The project README instructs `background=true`; the loaded tool schema has no such
  parameter. Starts still worked. The reopened session also reported `Title: (untitled)`
  while `projectPath` was set, which is cosmetic but confusing.

## 10. Permission classifier blocked a read-only lookup (harness)

- `fl_python_api(filter="marker")` was denied as "Irreversible Local Destruction". The
  operation only lists documentation. The SDK source was used instead.

## Server fixes applied (Fl-MCP, 2026-09-13)

- `fl_python_api` now annotates the SDK catalog: `pythonConventions` at the top, `pythonSignature`
  per operation, and `pythonName` on every argument and result-schema property (item 3, naming half).
- `fl_python_docs` states up front that embedded Python is the primary route, names the installed
  wheel, lists the helper classes and `fruitylink_serum`, and documents snake_case arguments, string
  dict keys, exception-discards-result, disk dumps, readback lag, single-token filters (items 3, 5, 6, 8).
- Managed sessions whose FL title is empty now report the project file name in `projectTitle` and the
  `Title:` line (item 9, cosmetic half). `background` was already exposed in the uncommitted source;
  the installed server predates it.
- `fl_mixer_set` uses the SDK's corrected signed pan contract (-6400..6400, default 0) and rejects the
  old 0..12800 range (item 1). New `fl_markers_list` / `fl_marker_delete` tools forward to the SDK's
  `list_markers` / `delete_marker` operations (item 2). Live behaviour of `delete_marker` depends on
  the rebuilt SDK bridge and installer; it has not been exercised against FL in this session.
