using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
using System.Text.Json;

namespace FlMcp.Protocol;

public static class PipeProtocol
{
    public const int MaximumMessageBytes = 1024 * 1024;
    public const string TokenVariable = "FL_MCP_SESSION_TOKEN";
    public const string WorkspaceVariable = "FL_MCP_WORKSPACE";

    public static string Name(int processId)
    {
        using var identity = WindowsIdentity.GetCurrent();
        var sid = identity.User?.Value ?? throw new InvalidOperationException("A Windows user identity is required.");
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(sid)))[..16];
        return $"fl-mcp-{hash}-{processId}";
    }

    public static bool TokenMatches(string expected, string? supplied)
    {
        if (supplied is null) return false;
        return CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(expected), Encoding.UTF8.GetBytes(supplied));
    }

    public static async Task WriteAsync<T>(Stream stream, T value, CancellationToken ct, int maximumMessageBytes = MaximumMessageBytes)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(value, Messages.Json);
        ValidateLength(bytes.Length, maximumMessageBytes);
        var header = new byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(header, bytes.Length);
        await stream.WriteAsync(header, ct).ConfigureAwait(false);
        await stream.WriteAsync(bytes, ct).ConfigureAwait(false);
        await stream.FlushAsync(ct).ConfigureAwait(false);
    }

    public static async Task<T> ReadAsync<T>(Stream stream, CancellationToken ct, int maximumMessageBytes = MaximumMessageBytes)
    {
        var header = new byte[4];
        await stream.ReadExactlyAsync(header, ct).ConfigureAwait(false);
        var length = BinaryPrimitives.ReadInt32LittleEndian(header);
        ValidateLength(length, maximumMessageBytes);
        var bytes = new byte[length];
        await stream.ReadExactlyAsync(bytes, ct).ConfigureAwait(false);
        return JsonSerializer.Deserialize<T>(bytes, Messages.Json) ?? throw new InvalidDataException("Empty bridge message.");
    }

    private static void ValidateLength(int length, int maximum)
    {
        if (length <= 0 || length > maximum)
            throw new InvalidDataException($"Bridge message must be 1..{maximum} bytes.");
    }
}
