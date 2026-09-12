# Third-party software

FL MCP is licensed under PolyForm Noncommercial 1.0.0. Dependencies keep their own licenses; this project's license does not replace them.

Full license and notice texts for bundled runtime dependencies are provided in `licenses/` and copied into the distribution.

- [FruityLink SDK](https://github.com/Realynx/FL-Automation), MIT. The plugin references the public `FruityLink.Plugins.Abstractions` 0.1.0 package. FruityLink runtime assemblies are supplied by the user's host installation, not the plugin distribution.
- [Model Context Protocol C# SDK](https://github.com/modelcontextprotocol/csharp-sdk), Apache-2.0. Version 2.2.0.
- [Microsoft .NET extensions](https://github.com/dotnet/runtime), MIT. Used by the stdio companion. See dependency metadata and `packages.lock.json` for the resolved graph.
- xUnit and Microsoft test SDK packages are development dependencies; they are not included in the distribution.

FL Studio, its factory content, third-party audio plugins, and Blender are not bundled. Their owners' terms continue to apply. FL MCP is not affiliated with or endorsed by Image-Line.
