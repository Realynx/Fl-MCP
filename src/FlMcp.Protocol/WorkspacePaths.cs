namespace FlMcp.Protocol;

/// <summary>Restricts write targets to the configured workspace and rejects Windows path aliases.</summary>
public sealed class WorkspacePaths
{
    public string Root { get; }

    public WorkspacePaths(string root)
    {
        if (!Path.IsPathFullyQualified(root)) throw new ArgumentException("Workspace must be an absolute path.");
        Root = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        Directory.CreateDirectory(Root);
        RejectReparsePoints(Root);
    }

    public string Resolve(string path, string extension)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ValidateSegments(path[(Path.GetPathRoot(path)?.Length ?? 0)..]);
        var full = Path.GetFullPath(path, Root);
        if (!full.StartsWith(Root, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Path must stay inside FL_MCP_WORKSPACE.");
        if (!string.Equals(Path.GetExtension(full), extension, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException($"Expected a {extension} file.");
        ValidateSegments(Path.GetRelativePath(Root, full));
        RejectReparsePoints(full);
        return full;
    }

    public string NewFile(string path, string extension)
    {
        var full = Resolve(path, extension);
        if (File.Exists(full) || Directory.Exists(full)) throw new IOException("Output already exists; use a new filename.");
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        RejectReparsePoints(full);
        return full;
    }

    private static void ValidateSegments(string relative)
    {
        foreach (var segment in relative.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))
        {
            if (segment.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || segment.EndsWith('.') || segment.EndsWith(' '))
                throw new ArgumentException("Path contains an invalid or ambiguous Windows filename.");
            var stem = segment.Split('.')[0].ToUpperInvariant();
            string[] reserved = ["CON", "PRN", "AUX", "NUL", "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9", "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9"];
            if (reserved.Contains(stem)) throw new ArgumentException("Reserved Windows device name.");
        }
    }

    private static void RejectReparsePoints(string path)
    {
        for (var current = path; !string.IsNullOrEmpty(current); current = Path.GetDirectoryName(current.TrimEnd(Path.DirectorySeparatorChar)))
        {
            if ((File.Exists(current) || Directory.Exists(current)) && (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                throw new ArgumentException("Workspace paths cannot traverse symbolic links or junctions.");
        }
    }
}
