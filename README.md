<div align="center">
<img src="assets/logo.svg" width="80" alt="FL Automate logo" />
<h1>FL MCP</h1>
<p><strong>Connect AI agents to FL Studio.</strong><br/>An MCP adapter built on the <a href="https://github.com/Realynx/FL-Automation">FruityLink SDK</a>.</p>
<p>
<a href="https://github.com/Realynx/Fl-MCP/actions/workflows/ci.yml"><img src="https://github.com/Realynx/Fl-MCP/actions/workflows/ci.yml/badge.svg" alt="Build and test status" /></a>
<img src="https://img.shields.io/badge/platform-Windows%20x64-d946ef?style=flat-square" alt="Windows x64" />
<a href="LICENSE"><img src="https://img.shields.io/badge/license-PolyForm%20Noncommercial-8b5cf6?style=flat-square" alt="PolyForm Noncommercial license" /></a>
</p>
<p><a href="https://realynx.github.io/Fl-MCP/"><strong>Documentation site</strong></a> · <a href="https://realynx.github.io/Fl-MCP/getting-started/">Getting started</a> · <a href="https://realynx.github.io/Fl-MCP/tools/">Tool reference</a></p>
</div>

Work on an open FL Studio project, or let an agent start a disposable project, author music, save snapshots, and render a WAV. **27 MCP tools** cover discovery, authoring, project management, and embedded Python. The shared FruityLink SDK adds typed access to notes, playlist clips, mixer routing/effects, automation, and offline audio analysis.

## Example album

Original tracks composed, revised, mixed and mastered entirely through FL MCP and the embedded
Python API, in small listener-driven revisions with persistence and render verification at every
step. The album exists to exercise the MCP against real production work; each track's friction
became the next batch of SDK fixes. **Listen in the browser** on the
[album page of the documentation site](https://realynx.github.io/Fl-MCP/album/) (GitHub shows
repository audio as a raw file; the site embeds a player).

**1. Ember Tides** (future bass, F minor, 140 BPM, eighteen revisions)

- **MP3:** [Ember-Tides-final-master.mp3](examples/ember-tides/Ember-Tides-final-master.mp3) (3:20, -14 LUFS, -1 dBTP)
- **Project file:** [Ember-Tides.flp](examples/ember-tides/Ember-Tides.flp) (FL Studio 2026, Serum 2, FabFilter; drum samples not included)
- **Revision history:** [examples/ember-tides](examples/ember-tides/README.md) · verification records in [examples/ember-tides/records](examples/ember-tides/records)

**2. Parking Lot Moon** (dream-pop / trip-hop / dark synthpop, F# minor, 100 BPM, fifteen revisions from a friend's brief)

- **MP3:** [Parking-Lot-Moon-final-master.mp3](examples/parking-lot-moon/Parking-Lot-Moon-final-master.mp3) (4:19, -13.9 LUFS, -1 dBTP)
- **Project file:** [Parking-Lot-Moon.flp](examples/parking-lot-moon/Parking-Lot-Moon.flp) (FL Studio 2026, Serum 2, GMS, FabFilter; commercial samples and vocal loop not included)
- **Revision history:** [examples/parking-lot-moon](examples/parking-lot-moon/README.md) · phase and verification records in [examples/parking-lot-moon/records](examples/parking-lot-moon/records)

**[Open the documentation site →](https://realynx.github.io/Fl-MCP/)**

[Browse the documentation's Markdown source](docs/index.md).

| I want to… | Start here |
| --- | --- |
| Connect my AI app | [Install FL MCP](https://realynx.github.io/Fl-MCP/installation/) |
| Try it on my first project | [Getting started](https://realynx.github.io/Fl-MCP/getting-started/) |
| Understand sessions and timing | [How it works](https://realynx.github.io/Fl-MCP/concepts/) |
| Find a tool or write Python | [Tool reference](https://realynx.github.io/Fl-MCP/tools/) · [Python guide](https://realynx.github.io/Fl-MCP/python/) |
| Build plugins or use the C# SDK | [FruityLink C# SDK](https://realynx.github.io/FL-Automation/csharp/) |
| Build FL MCP from source | [Developer guide](https://realynx.github.io/Fl-MCP/building/) |

The recommended setup is the FruityLink framework installer with **FLMCP** selected. You need Windows x64, a licensed compatible FL Studio installation, and an interactive desktop session. The installer bundles private Python; FL MCP does not need system Python, pip, or its own AI provider key.

**Serum support** is also selected by default in current framework bundles. It adds
local preset searches and audition-audio descriptions through `fruitylink_serum`,
using the same embedded Python runtime. You can deselect it in the installer.
See [Python extensions](docs/python.md#serum-support).

**Availability:** the verified installer **0.1.26** is locally prepared and has not been published; obtain a matching bundle from the maintainer or use the source build guide. Published installers belong on the [framework releases page](https://github.com/Realynx/FL-Automation/releases). SDK 0.2.0 packages are not assumed available on NuGet or PyPI.

**Compatibility:** live verification covers FL Studio **26.1.3.5570**. Other exact builds need their own validation; FL 2025 has automated/binary coverage but no live run in this cycle. See [capabilities and limits](docs/capabilities.md) and [verification evidence](docs/live-verification.md).

FL MCP is source-available under [PolyForm Noncommercial 1.0.0](LICENSE). Dependencies have their own terms; see [third-party notices](THIRD-PARTY-NOTICES.md).
