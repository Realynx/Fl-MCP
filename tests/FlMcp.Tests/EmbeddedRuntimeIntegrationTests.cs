using System.Reflection;
using FlMcp.Plugin;
using FlMcp.Protocol;
using FruityLink.Core.Abstractions;
using FruityLink.Scripting;
using Xunit;

namespace FlMcp.Tests;

public sealed class EmbeddedRuntimeIntegrationTests
{
    [EmbeddedRuntimeFact]
    public async Task ActualInterpreterRunsInsidePluginHostWithDirectSdkCallbacks()
    {
        using var files = new TestFiles();
        var runtime = Environment.GetEnvironmentVariable("FRUITYLINK_TEST_PYTHON_RUNTIME")!;
        var package = Environment.GetEnvironmentVariable("FRUITYLINK_TEST_PYTHON_PACKAGE")
            ?? throw new InvalidOperationException("Set FRUITYLINK_TEST_PYTHON_PACKAGE to the SDK wheel or python/src directory.");
        var fl = DispatchProxy.Create<INativeFlControl, EmbeddedPythonTests.ProjectControl>();
        var control = (EmbeddedPythonTests.ProjectControl)fl;
        control.Project = files.Project();
        await using var dispatcher = new CommandDispatcher(fl, new WorkspacePaths(files.Root),
            handler => new EmbeddedPythonRuntime(new(runtime, package), handler));
        const string code = """
            import os
            print("embedded callback")
            result = {"pid": os.getpid(), "tempo": fl.ops.get_tempo(),
                      "operations": [item["name"] for item in fl.catalog()["operations"]]}
            """;
        var response = await dispatcher.DispatchAsync("python_execute",
            Messages.Element(new PythonExecute(code, 15, control.Project)), CancellationToken.None);
        Assert.True(response.GetProperty("ok").GetBoolean(), response.ToString());
        var result = response.GetProperty("result");
        Assert.Equal(Environment.ProcessId, result.GetProperty("pid").GetInt32());
        Assert.Equal(123, result.GetProperty("tempo").GetDouble());
        Assert.Contains(result.GetProperty("operations").EnumerateArray(), item => item.GetString() == "set_tempo");
        Assert.Contains("embedded callback", response.GetProperty("stdout").GetString());
        Assert.Single(control.Calls, name => name == "GetTempoAsync");
    }

    private sealed class EmbeddedRuntimeFactAttribute : FactAttribute
    {
        public EmbeddedRuntimeFactAttribute()
        {
            if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("FRUITYLINK_TEST_PYTHON_RUNTIME")))
                Skip = "Set FRUITYLINK_TEST_PYTHON_RUNTIME to the private CPython 3.14.6 runtime for the real embedding test.";
        }
    }
}
