using Hl7Bridge.Service;
using Hl7Bridge.Service.Configuration;
using Hl7Bridge.Service.Excel;
using Hl7Bridge.Service.Hl7;
using Hl7Bridge.Service.Infrastructure;
using Hl7Bridge.Service.Processing;
using Hl7Bridge.Service.State;
using Microsoft.Extensions.Options;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddWindowsService(options => options.ServiceName = "Hl7InstrumentBridge")
    .AddOptions<BridgeOptions>().Bind(builder.Configuration.GetSection("Bridge")).ValidateDataAnnotations().ValidateOnStart();

builder.Services.AddSingleton<BridgeConfigurationStore>();
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
    .MinimumLevel.Verbose()
    .WriteTo.File(Path.Combine(logPath, "bridge-.log"), rollingInterval: RollingInterval.Day, retainedFileCountLimit: 30)
    .WriteTo.File(
        Path.Combine(logPath, "events-.log"),
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 30,
        restrictedToMinimumLevel: Serilog.Events.LogEventLevel.Verbose,
        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] ({SourceContext}) {Message:lj}{NewLine}{Exception}")
    .CreateLogger();

builder.Services.AddSerilog();

var app = builder.Build();

app.UseSerilogRequestLogging();
app.UseDefaultFiles();
app.UseStaticFiles();

app.MapGet("/api/config", (BridgeConfigurationStore store) => Results.Ok(store.LoadCurrent()));

app.MapPut("/api/config", (BridgeOptions options, BridgeConfigurationStore store) =>
{
    var result = store.Save(options);
    return result.IsValid ? Results.Ok(result) : Results.BadRequest(result);
});

app.MapGet("/api/status", (IOptions<BridgeOptions> options) =>
{
    var current = options.Value;

    var dto = new BridgeStatusDto(
        DateTime.UtcNow.ToString("O"),
        CountFiles(current.Folders.Incoming),
        CountFiles(current.Folders.Processing),
        CountFiles(current.Folders.Sent),
        CountFiles(current.Folders.Error),
        current.Lis.Host,
        current.Lis.Port,
        current.Folders.Hl7ArchiveEnabled);

    return Results.Ok(dto);
});

await EnsureFoldersAsync(app.Services);
await app.RunAsync();

static int CountFiles(string folder)
{
    if (!Directory.Exists(folder))
    {
        return 0;
    }

    return Directory.EnumerateFiles(folder, "*.xlsx", SearchOption.TopDirectoryOnly).Count();
}

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
