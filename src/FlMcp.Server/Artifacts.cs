using System.Text;

namespace FlMcp.Server;

public static class Artifacts
{
    public static long VerifyProject(string path) => VerifyProject(path, FileShare.None);

    internal static long VerifyProjectSource(string path) => VerifyProject(path, FileShare.Read);

    private static long VerifyProject(string path, FileShare share)
    {
        using var stream = File.Open(path, FileMode.Open, FileAccess.Read, share);
        using var reader = new BinaryReader(stream, Encoding.ASCII, leaveOpen: true);
        if (stream.Length <= 22 || Encoding.ASCII.GetString(reader.ReadBytes(4)) != "FLhd" || reader.ReadUInt32() != 6)
            throw new InvalidDataException("Project has no complete FLhd header.");
        var format = reader.ReadUInt16();
        reader.ReadUInt16(); // Legacy channel count; modern FL stores its channel list in events.
        var ppq = reader.ReadUInt16();
        if (format != 0 || ppq == 0 || Encoding.ASCII.GetString(reader.ReadBytes(4)) != "FLdt")
            throw new InvalidDataException("Project header fields or FLdt data chunk are invalid.");
        var length = reader.ReadUInt32();
        if (length == 0 || length != stream.Length - stream.Position)
            throw new InvalidDataException("Project event data is empty, truncated, or has unexpected trailing bytes.");
        return stream.Length;
    }

    public static long VerifyWave(string path) => ReadWave(path).Bytes;

    /// <summary>Validates the WAV envelope and reads its duration from the format and data chunks.</summary>
    public static WaveInfo ReadWave(string path)
    {
        using var stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.None);
        var header = new byte[12];
        stream.ReadExactly(header);
        var format = Encoding.ASCII.GetString(header.AsSpan(0, 4));
        if (stream.Length <= 44 || format != "RIFF" || Encoding.ASCII.GetString(header.AsSpan(8, 4)) != "WAVE")
            throw new InvalidDataException("Render exited without a valid nonempty WAV file.");
        return ReadWaveChunks(stream);
    }

    private static WaveInfo ReadWaveChunks(Stream stream)
    {
        using var reader = new BinaryReader(stream, Encoding.ASCII, leaveOpen: true);
        var hasFormat = false;
        var hasAudio = false;
        var byteRate = 0u;
        var channels = 0;
        var sampleRate = 0;
        var dataBytes = 0L;
        while (stream.Position + 8 <= stream.Length)
        {
            var name = Encoding.ASCII.GetString(reader.ReadBytes(4));
            var size = reader.ReadUInt32();
            if (size > stream.Length - stream.Position) throw new InvalidDataException("Truncated WAV chunk.");
            var next = stream.Position + size + (size % 2);
            if (name == "fmt " && size >= 16)
            {
                hasFormat = true;
                reader.ReadUInt16(); // format tag
                channels = reader.ReadUInt16();
                sampleRate = reader.ReadInt32();
                byteRate = reader.ReadUInt32();
            }
            else if (name == "data" && size > 0)
            {
                hasAudio = true;
                dataBytes = size;
            }
            stream.Seek(next, SeekOrigin.Begin);
        }
        if (!hasFormat || !hasAudio) throw new InvalidDataException("WAV needs format and nonempty audio chunks.");
        return new WaveInfo(stream.Length, byteRate > 0 ? dataBytes / (double)byteRate : 0, sampleRate, channels);
    }
}

/// <summary>A validated WAV: file size plus the duration implied by its data chunk and byte rate.</summary>
public sealed record WaveInfo(long Bytes, double Seconds, int SampleRate, int Channels);
