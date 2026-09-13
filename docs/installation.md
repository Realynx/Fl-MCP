# Install FL MCP

The recommended setup is the **FruityLink framework installer with FLMCP selected**. It installs the framework, FL MCP plugin, companion server, and private Python runtime together.

**Installer availability:** the verified **0.1.22** bundle is locally prepared and has not been published. Obtain that matching bundle from the maintainer. Published installers belong on the [FruityLink releases page](https://github.com/Realynx/FL-Automation/releases). To build your own, use the [developer guide](building.md); do not assume SDK 0.2.0 packages are published on NuGet or PyPI.

## Before you begin

| Requirement | Why it matters |
| --- | --- |
| Windows x64 and a licensed FL Studio installation | The plugin runs inside FL Studio. FL and its licensed instruments/assets are not bundled. |
| A compatible exact FL build | Live verification currently covers **26.1.3.5570**; see [compatibility](capabilities.md#compatibility). |
| A logged-in desktop session | FL may display licensing, missing-asset, or export dialogs. Unattended operation still uses the desktop application. |
| An MCP client that supports local stdio servers | Your AI app launches the companion and sends tool calls over standard input/output. |
| Matching .NET runtimes | The companion targets .NET 10; the framework plugin targets .NET 9. See [developer deployment](building.md) for manual runtime setup. |

End users do not need system Python, pip, or an AI provider key for FL MCP itself. Your chosen AI app has its own account and model configuration.

## Use the installer

1. Save your work and close FL Studio. Extract the installer bundle, keeping its payload folder beside `FruityLink.Installer.exe`, and run it.
2. Under **FL STUDIO INSTALL FOLDER**, use **Detect** or **Browse…** to choose the directory containing `FL64.exe`, for example `C:\Program Files\Image-Line\FL Studio 2026`.
3. Under **PLUGINS TO INSTALL**, select **FLMCP — MCP server with Python included (PolyForm Noncommercial)**. Bundles containing it select it by default. Other plugins are optional.
4. Under **CONNECT YOUR AI APPS (OPTIONAL)**, select the apps you use. The installer enables FLMCP and configures only selected apps. You can leave **Project defaults (optional)** unchanged for initial attachment.
5. Leave **Dry run (preview only)** unchecked and click **Install**. Approve the Windows administrator prompt if shown, and wait for installation and client setup to finish.
6. Restart the configured AI apps. Open FL Studio and let the project finish loading. After future plugin/runtime updates, restart FL before using the plugin.

Continue with [your first connection](getting-started.md#read-an-open-project). A successful setup lets your client read `fl_python_docs` and discover your FL process with `fl_instances`.

## Configure an app manually

If you skipped client setup, enable **FL MCP** in FruityLink's **Plugins** menu. Start with the repository's [example MCP settings](https://github.com/Realynx/Fl-MCP/blob/master/examples/mcp-settings.json), replacing its executable, companion DLL, runtime, wheel, template, and workspace paths with your own.

The example uses an `mcpServers` object. Your app may use a different configuration format; carry across the command, arguments, and environment settings rather than pasting an unsupported wrapper. When using the example's `dotnet` command, the .NET 10 runtime must be available to the app.

The essential setting for attachment is `FL_MCP_FL_EXE`, pointing to the exact installed `FL64.exe`. `FL_MCP_WORKSPACE` chooses the directory for snapshots and samples. A saved template is needed when you start disposable projects. The [configuration reference](sessions.md#connect-an-mcp-client) lists every supported environment variable and runtime fallback path.

<details>
<summary>Do I need the separate Python Scripting plugin?</summary>

No. FL MCP hosts the shared scripting dispatcher itself. The SDK's **Python Scripting** plugin serves standalone Python connections. MCP scripts use the installed private runtime inside the FL process.

</details>

<details>
<summary>Can I run FL MCP as a service or in a container?</summary>

The current workflow requires an interactive Windows desktop session. “Headless” means there is no FLMCP plugin window and FL can run unattended where its GUI application permits it. It does not mean FL can run as a Windows service or container workload.

</details>

## Prepare disposable projects

To let an agent author and render a separate project, save an FL template as a real `.flp` file and point `FL_MCP_TEMPLATE` to its absolute path. Choose a workspace for snapshots and audio, and close all other FL processes before calling `fl_project_start`.

The template provides actual tempo, PPQ, export settings, and initial content. The agent must query the loaded project instead of assuming a particular blank template. Follow the [disposable-project walkthrough](getting-started.md#create-and-render-a-disposable-project).
