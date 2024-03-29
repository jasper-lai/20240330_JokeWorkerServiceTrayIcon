using JokeWorkerServiceTrayIcon;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using System.Runtime.InteropServices;


#region WIN32 API Import
[DllImport("kernel32.dll", SetLastError = true)]
static extern bool AllocConsole();

[DllImport("kernel32.dll", SetLastError = true)]
static extern bool AttachConsole(int dwProcessId);
#endregion

#region 設置 Serilog 
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .WriteTo.Console()
    //.WriteTo.File("logs/JokeService-.txt", rollingInterval: RollingInterval.Day)
    .WriteTo.File("D:/Temp/logs/JokeService-.txt", rollingInterval: RollingInterval.Day)
    .CreateLogger();
#endregion

var builder = Host.CreateApplicationBuilder(args);

#region 採用 Serilog 作為 Log 的工具
// Configure your application
builder.Logging.ClearProviders(); // Clear default logging providers
builder.Logging.AddSerilog(); // Use Serilog for logging
#endregion

builder.Services.AddSingleton<JokeService>();
builder.Services.AddHostedService<Worker>();


// 重要: 因為輸出的 exe 是 WinExe: 係指不含 console 視窗的 windows 程式, 例如: Window Form
var isConsoleMode = args.Contains("--console");
var isServiceMode = args.Contains("--service");
var isTrayMode = !isConsoleMode && !isServiceMode;

// If in console mode, attempt to attach to an existing console or create a new one
// 如果是 Console Mode,
// (1) 如果母視窗是 console, 就直接拿來用. 例如: 命令列提示 視窗.
// (2) 如果母視窗不是 console, 就配置一個新的 console 視窗. 例如: VS2022 執行偵錯.
if (isConsoleMode)
{
    if (!AttachConsole(-1)) // Attach to a parent process console
    {
        AllocConsole(); // Alloc a new console if none available
    }
    Log.Information("=== in Console Mode ===");
}

if (isServiceMode)
{
    builder.Services.AddWindowsService(options =>
    {
        options.ServiceName = ".NET8 Joke Service TrayIcon";
    });
    Log.Information("=== in Service Mode ===");
}

var host = builder.Build();

// Check if the application should show a tray icon
if (isConsoleMode || isServiceMode)
{
    host.Run();
}
else
{
    Log.Information("=== in TrayIcon Mode ===");
    Application.Run(new TrayApplicationContext(host));
}