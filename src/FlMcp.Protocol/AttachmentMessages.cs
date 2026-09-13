namespace FlMcp.Protocol;

public sealed record AttachRequest(string InstanceId, string Workspace, string? PythonRuntimeDirectory = null, string? PythonPackagePath = null);
public sealed record AttachReply(string InstanceId, string LeaseToken, string Workspace, SessionStatus Status);

/// <summary>Best-effort identity only: FL can replace an untitled project without changing these fields.</summary>
public sealed record ProjectIdentity(string Title, string Path, bool Untitled)
{
    public static ProjectIdentity FromStatus(SessionStatus status)
    {
        var path = status.ProjectPath ?? Line(status.Project, "Path: ");
        return new(status.ProjectTitle ?? Line(status.Project, "Title: "), path,
            status.Untitled ?? (string.IsNullOrWhiteSpace(path) || string.Equals(System.IO.Path.GetFileName(path), "untitled.flp", StringComparison.OrdinalIgnoreCase)));
    }

    public bool Matches(ProjectIdentity other) =>
        string.Equals(Title, other.Title, StringComparison.Ordinal) &&
        string.Equals(Path, other.Path, StringComparison.OrdinalIgnoreCase) && Untitled == other.Untitled;

    private static string Line(string text, string prefix) =>
        text.Split('\n').LastOrDefault(line => line.StartsWith(prefix, StringComparison.Ordinal))?[prefix.Length..].TrimEnd('\r') ?? "";
}
