using FlMcp.Server;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

var builder = Host.CreateApplicationBuilder(args);
builder.Logging.ClearProviders();
builder.Logging.AddConsole(options => options.LogToStandardErrorThreshold = LogLevel.Trace);
builder.Services.AddSingleton(ServerSettings.FromEnvironment());
builder.Services.AddSingleton<IProcessHost, ProcessHost>();
builder.Services.AddSingleton<IBridgeClient, BridgeClient>();
builder.Services.AddSingleton<ManagedSession>();
builder.Services.AddMcpServer().WithStdioServerTransport().WithTools<FlTools>();
await builder.Build().RunAsync();
