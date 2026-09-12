using System.Diagnostics;
using FlMcp.Protocol;

namespace FlMcp.Server;

public static class LaunchCommands
{
    public static ProcessStartInfo Authoring(string executable, string project, string workspace, string token)
    {
        var info = Create(executable);
        info.ArgumentList.Add(project);
        info.Environment[PipeProtocol.TokenVariable] = token;
        info.Environment[PipeProtocol.WorkspaceVariable] = workspace;
        return info;
    }

    public static ProcessStartInfo Render(string executable, string project, string outputDirectory)
    {
        var info = Create(executable);
        info.ArgumentList.Add("/R");
        info.ArgumentList.Add("/Ewav");
        info.ArgumentList.Add("/O" + outputDirectory);
        info.ArgumentList.Add(project);
        info.Environment.Remove(PipeProtocol.TokenVariable);
        return info;
    }

    private static ProcessStartInfo Create(string executable) => new(executable)
    {
        UseShellExecute = false,
        CreateNoWindow = true,
        WindowStyle = ProcessWindowStyle.Hidden,
        WorkingDirectory = Path.GetDirectoryName(executable)!
    };
}
