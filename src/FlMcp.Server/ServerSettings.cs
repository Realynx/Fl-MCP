using FlMcp.Protocol;

namespace FlMcp.Server;

public sealed record ServerSettings(string? Executable, string? Template, string Workspace)
{
    public string? PythonRuntimeDirectory { get; init; }
    public string? PythonPackagePath { get; init; }
    /// <summary>Inline budget for fl_execute_python responses in UTF-8 bytes; larger responses are saved under the workspace.</summary>
    public int PythonResponseLimitBytes { get; init; } = PythonResults.DefaultLimitBytes;
    public static ServerSettings FromEnvironment() => new(
        Environment.GetEnvironmentVariable("FL_MCP_FL_EXE"),
        Environment.GetEnvironmentVariable("FL_MCP_TEMPLATE"),
        Environment.GetEnvironmentVariable(PipeProtocol.WorkspaceVariable) ??
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FlMcp", "Projects"))
    {
        PythonRuntimeDirectory = Environment.GetEnvironmentVariable("FL_MCP_PYTHON_RUNTIME"),
        PythonPackagePath = Environment.GetEnvironmentVariable("FL_MCP_PYTHON_PATH"),
        PythonResponseLimitBytes = PythonResults.ParseLimit(Environment.GetEnvironmentVariable(PythonResults.LimitVariable))
    };

    public string ResolvePythonRuntime() => PythonRuntimeDirectory ??
        Path.Combine(Path.GetDirectoryName(Executable!)!, "FruityLink", "tools", "fl-mcp", "python", "runtime");

    public string ResolvePythonPackage() => PythonPackagePath ??
        Path.Combine(Path.GetDirectoryName(Executable!)!, "FruityLink", "tools", "fl-mcp", "python", "fruitylink_python-0.2.0-py3-none-any.whl");

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
