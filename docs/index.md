# FL MCP documentation

FL MCP lets an AI app work with FL Studio through the Model Context Protocol (MCP). Your app chooses tools; FL MCP connects those requests to the project you selected. You can inspect a session, create notes and arrangements, adjust instruments and effects, and render a disposable project to audio.

Start with one small goal: **connect to an open project and read its channels**. Once that works, the same connection can make edits or execute Python using the FruityLink SDK.

## Choose your starting point

| Your goal | Guide |
| --- | --- |
| Set up FL MCP and an AI app | [Installation](installation.md) |
| Make your first successful connection | [Getting started](getting-started.md) |
| Understand the components and music objects | [How it works](concepts.md) |
| See what works today | [Capabilities and compatibility](capabilities.md) |
| Find a tool's inputs and behavior | [MCP tool reference](tools.md) |
| Write a script or use more of the SDK | [Python in FL MCP](python.md) |
| Configure sessions, snapshots, and rendering | [Sessions and configuration](sessions.md) |
| Fix a connection, script, or render problem | [Troubleshooting](troubleshooting.md) |

## The project family

**FruityLink** is the framework installed inside FL Studio and the reusable C# and Python SDK. **FL MCP** is this repository's adapter: a plugin inside FL and a companion process launched by your AI app. It handles selecting a project, saving workspace snapshots, and managing disposable rendering sessions.

For a standalone Python application or a C# plugin, start with the [FruityLink SDK documentation](https://github.com/Realynx/FL-Automation/blob/master/docs/index.md). For AI-assisted work through MCP, stay here. You do not need to learn C# or install Python to use FL MCP through your app.

## Current availability

The verified installer **0.1.22** is currently local and has not been published. Obtain the matching bundle from the maintainer or follow the [source build guide](building.md). The [framework releases page](https://github.com/Realynx/FL-Automation/releases) is the destination for published installers; SDK 0.2.0 packages are not assumed published to NuGet or PyPI.

Live verification covers **FL Studio 26.1.3.5570 on Windows x64**. Capabilities depend on exact native builds. Read the [compatibility summary](capabilities.md#compatibility) before choosing an installation, and the [verification record](live-verification.md) for measured results and unresolved limits.

## For contributors

Read [building and developer deployment](building.md) for the .NET quality gate, matching SDK checkout, packaging, and CI. [Live verification](live-verification.md) explains how to check a new exact FL build. [Documentation maintenance](documentation.md) explains the Markdown site and wiki workflow.

FL MCP is source-available under [PolyForm Noncommercial 1.0.0](https://github.com/Realynx/Fl-MCP/blob/master/LICENSE). See [third-party notices](https://github.com/Realynx/Fl-MCP/blob/master/THIRD-PARTY-NOTICES.md) for dependency terms.
