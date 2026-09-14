using System.Text;
using System.Text.Json;
using FlMcp.Protocol;
using FruityLink.Core.Abstractions;
using FruityLink.Scripting;

namespace FlMcp.Plugin;

public sealed partial class CommandDispatcher
{
    private readonly Func<Func<string, JsonElement, CancellationToken, Task<object?>>, IEmbeddedPythonRuntime> createPython;
    private readonly SemaphoreSlim pythonExecution = new(1, 1);
    private IEmbeddedPythonRuntime? embeddedPython;
    private string? embeddedProject;
    private ProjectIdentity? attachedProject;

    private static IEmbeddedPythonRuntime CreateEmbeddedPython(Func<string, JsonElement, CancellationToken, Task<object?>> handler)
    {
        // Plugin assemblies may be shadow-copied. Resolve defaults from the actual FL executable.
        var flRoot = Path.GetDirectoryName(Environment.ProcessPath)
            ?? throw new InvalidOperationException("Cannot locate the FL Studio executable directory.");
        var options = EmbeddedPythonRuntimeLocator.Resolve(Path.Combine(flRoot, "FruityLink"));
        return new EmbeddedPythonRuntime(options, handler);
    }

    private async Task<object?> PythonExecuteAsync(PythonExecute request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Code) || Encoding.UTF8.GetByteCount(request.Code) > 128 * 1024)
            throw new ArgumentException("Python code must contain 1..131072 UTF-8 bytes.");
        Arguments.Range(request.TimeoutSeconds, 1, 300, "timeoutSeconds");
        if (request.AttachedProject is null && !Path.IsPathFullyQualified(request.ExpectedProjectPath))
            throw new ArgumentException("Embedded execution requires an absolute managed project path.");
        var project = request.AttachedProject is null ? paths.Resolve(request.ExpectedProjectPath, ".flp") : request.ExpectedProjectPath;
        await pythonExecution.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            embeddedProject = project;
            attachedProject = request.AttachedProject;
            await RequireEmbeddedProjectAsync(ct).ConfigureAwait(false);
            embeddedPython ??= createPython(HandleEmbeddedRequestAsync);
            // SDK completion includes cooperative cancellation and all Python/native callback drain.
            return await embeddedPython.ExecuteAsync(request.Code, request.TimeoutSeconds, ct).ConfigureAwait(false);
        }
        finally
        {
            await scripting.DrainAsync().ConfigureAwait(false);
            embeddedProject = null;
            attachedProject = null;
            pythonExecution.Release();
        }
    }

    private async Task<object?> HandleEmbeddedRequestAsync(string method, JsonElement parameters, CancellationToken ct)
    {
        try
        {
            var prepared = scriptingPolicy.Prepare(method, parameters);
            await RequireEmbeddedProjectAsync(ct).ConfigureAwait(false);
            // Direct managed call inside FL: no callback through the pipe that owns this invocation.
            return await scripting.HandleRequestAsync(method, prepared, ct).ConfigureAwait(false);
        }
        finally { await scripting.DrainAsync().ConfigureAwait(false); }
    }

    private async Task RequireEmbeddedProjectAsync(CancellationToken ct)
    {
        if (embeddedProject is null) throw new InvalidOperationException("No embedded Python invocation owns a managed project.");
        if (attachedProject is not null)
        {
            var status = (SessionStatus)(await StatusAsync(ct).ConfigureAwait(false))!;
            if (!status.Available || !attachedProject.Matches(ProjectIdentity.FromStatus(status)))
                throw new InvalidOperationException("The attached project changed. Call fl_attach again before editing it.");
            return;
        }
        string? reported;
        if (structuredQueries) reported = (await InvokeAsync<FlProjectInfo>("query_project", new { }, ct).ConfigureAwait(false)).Path;
        else
        {
            var info = await InvokeAsync<string>("get_project_info", new { }, ct).ConfigureAwait(false);
            reported = info.Split('\n').LastOrDefault(line => line.StartsWith("Path: ", StringComparison.Ordinal))?[6..].Trim();
        }
        if (reported is null || !Path.IsPathFullyQualified(reported) ||
            !string.Equals(Path.GetFullPath(reported), embeddedProject, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("FL no longer reports the expected managed project. Embedded Python cannot continue editing it.");
    }

    private async Task<object?> PythonCallAsync(PythonCall call, CancellationToken ct)
    {
        try
        {
            var parameters = scriptingPolicy.Prepare(call.Method, call.Parameters);
            var result = await scripting.HandleRequestAsync(call.Method, parameters, ct).ConfigureAwait(false);
            return new PythonReply(Messages.Element(result));
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception ex) { return new PythonReply(null, Messages.Element(ScriptingError.FromException(ex))); }
        finally { await scripting.DrainAsync().ConfigureAwait(false); }
    }

    public async ValueTask DisposeAsync()
    {
        await pythonExecution.WaitAsync(CancellationToken.None).ConfigureAwait(false);
        try
        {
            if (embeddedPython is not null) await embeddedPython.DisposeAsync().ConfigureAwait(false);
            await scripting.DisposeAsync().ConfigureAwait(false);
        }
        finally { pythonExecution.Release(); }
    }
}
