using System.Text;

namespace FlMcp.Tests;

internal sealed class TestFiles : IDisposable
{
    public string Root { get; } = Path.Combine(Path.GetTempPath(), "fl-mcp-tests", Guid.NewGuid().ToString("N"));

    public TestFiles() => Directory.CreateDirectory(Root);
    public string PathFor(string name) => Path.Combine(Root, name);

    public string Project(string name = "template.flp")
    {
        var path = PathFor(name);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        WriteProject(path);
        return path;
    }

    public static void WriteProject(string path)
    {
        // Synthetic envelope for orchestration tests only; no claim that FL can load this project.
        using var writer = new BinaryWriter(File.Create(path), Encoding.ASCII);
        writer.Write(Encoding.ASCII.GetBytes("FLhd"));
        writer.Write(6);
        writer.Write((short)0);
        writer.Write((short)1);
        writer.Write((short)96);
        writer.Write(Encoding.ASCII.GetBytes("FLdt"));
        writer.Write(2);
        writer.Write((byte)0);
        writer.Write((byte)1);
    }

    public static void WriteWave(string path)
    {
        using var writer = new BinaryWriter(File.Create(path), Encoding.ASCII);
        writer.Write(Encoding.ASCII.GetBytes("RIFF"));
        writer.Write(40);
        writer.Write(Encoding.ASCII.GetBytes("WAVEfmt "));
        writer.Write(16);
        writer.Write((short)1);
        writer.Write((short)1);
        writer.Write(44100);
        writer.Write(88200);
        writer.Write((short)2);
        writer.Write((short)16);
        writer.Write(Encoding.ASCII.GetBytes("data"));
        writer.Write(4);
        writer.Write(123);
    }

    public void Dispose()
    {
        var root = Path.GetFullPath(Root);
        var parent = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "fl-mcp-tests")) + Path.DirectorySeparatorChar;
        if (!root.StartsWith(parent, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Invalid test cleanup root.");
        Directory.Delete(root, recursive: true);
    }
}
