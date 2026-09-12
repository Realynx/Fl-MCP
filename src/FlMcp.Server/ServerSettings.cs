using FlMcp.Protocol;

namespace FlMcp.Server;

public sealed record ServerSettings(string? Executable, string? Template, string Workspace)
{
    public static ServerSettings FromEnvironment() => new(
        Environment.GetEnvironmentVariable("FL_MCP_FL_EXE"),
        Environment.GetEnvironmentVariable("FL_MCP_TEMPLATE"),
        Environment.GetEnvironmentVariable(PipeProtocol.WorkspaceVariable) ??
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FlMcp", "Projects"));

    public void ValidateLaunch()
    {
        RequireFile(Executable, ".exe", "FL_MCP_FL_EXE");
        RequireFile(Template, ".flp", "FL_MCP_TEMPLATE");
    }

    private static void RequireFile(string? value, string extension, string setting)
    {
        if (string.IsNullOrWhiteSpace(value) || !Path.IsPathFullyQualified(value) ||
            !string.Equals(Path.GetExtension(value), extension, StringComparison.OrdinalIgnoreCase) || !File.Exists(value))
            throw new InvalidOperationException($"Set {setting} to an existing absolute {extension} path.");
    }
}
