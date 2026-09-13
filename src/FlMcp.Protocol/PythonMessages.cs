using System.Text.Json;

namespace FlMcp.Protocol;

public sealed record PythonCall(string Method, JsonElement Parameters);
public sealed record PythonReply(JsonElement? Result, JsonElement? Error = null);
public sealed record PythonExecute(string Code, int TimeoutSeconds, string ExpectedProjectPath)
{
    public ProjectIdentity? AttachedProject { get; init; }
}
