using System.ComponentModel;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace FlMcp.Protocol;

/// <summary>Reads Windows-authenticated named-pipe peer identities, independent of JSON claims.</summary>
public static class PipePeerIdentity
{
    /// <summary>Returns the process that owns the connected server endpoint.</summary>
    public static int GetServerProcessId(NamedPipeClientStream pipe)
    {
        ArgumentNullException.ThrowIfNull(pipe);
        if (!GetNamedPipeServerProcessId(pipe.SafePipeHandle, out uint processId))
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Unable to verify the named-pipe server process.");
        return CheckedId(processId);
    }

    /// <summary>Returns the process connected to this server instance.</summary>
    public static int GetClientProcessId(NamedPipeServerStream pipe)
    {
        ArgumentNullException.ThrowIfNull(pipe);
        if (!GetNamedPipeClientProcessId(pipe.SafePipeHandle, out uint processId))
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Unable to verify the named-pipe client process.");
        return CheckedId(processId);
    }

    private static int CheckedId(uint processId)
    {
        if (processId is 0 or > int.MaxValue) throw new IOException("Named-pipe peer has an invalid process identity.");
        return (int)processId;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetNamedPipeServerProcessId(SafePipeHandle pipe, out uint processId);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetNamedPipeClientProcessId(SafePipeHandle pipe, out uint processId);
}
