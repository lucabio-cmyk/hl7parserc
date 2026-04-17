using Hl7Bridge.Service.Configuration;
using Hl7Bridge.Service.Excel;
using Hl7Bridge.Service.Hl7;
using Hl7Bridge.Service.Infrastructure;
using Hl7Bridge.Service.Processing;
using Hl7Bridge.Service.State;
using Microsoft.Extensions.Options;
using Serilog;

var builder = Host.CreateApplicationBuilder(args);

builder.Services
    .AddWindowsService(options => options.ServiceName = "Hl7InstrumentBridge")
    .AddOptions<BridgeOptions>().Bind(builder.Configuration.GetSection("Bridge")).ValidateDataAnnotations().ValidateOnStart();

builder.Services.AddSingleton<IExcelTargetParser, ClosedXmlTargetParser>();
builder.Services.AddSingleton<IResultMapper, ResultMapper>();
builder.Services.AddSingleton<IHl7MessageBuilder, Hl7MessageBuilder>();
builder.Services.AddSingleton<IFileStateManager, FileStateManager>();
builder.Services.AddSingleton<IDuplicateGuard, FileFingerprintDuplicateGuard>();
builder.Services.AddSingleton<IMllpClient, MllpClient>();
builder.Services.AddSingleton<ISampleDispatchService, SampleDispatchService>();
builder.Services.AddHostedService<InstrumentBridgeWorker>();

var logPath = builder.Configuration["Bridge:Folders:Logs"] ?? @"C:\Hl7Bridge\logs";
Directory.CreateDirectory(logPath);

Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .WriteTo.File(Path.Combine(logPath, "bridge-.log"), rollingInterval: RollingInterval.Day, retainedFileCountLimit: 30)
    .CreateLogger();

builder.Services.AddSerilog();

var app = builder.Build();

await EnsureFoldersAsync(app.Services);
await app.RunAsync();

static async Task EnsureFoldersAsync(IServiceProvider services)
{
    using var scope = services.CreateScope();
    var options = scope.ServiceProvider.GetRequiredService<IOptions<BridgeOptions>>().Value;
    Directory.CreateDirectory(options.Folders.Incoming);
    Directory.CreateDirectory(options.Folders.Processing);
    Directory.CreateDirectory(options.Folders.Sent);
    Directory.CreateDirectory(options.Folders.Error);
    Directory.CreateDirectory(options.Folders.Logs);

    if (options.Folders.Hl7ArchiveEnabled)
    {
        Directory.CreateDirectory(options.Folders.Hl7Archive);
    }

    await Task.CompletedTask;
}
