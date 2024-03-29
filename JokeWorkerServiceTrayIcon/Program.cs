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

#region �]�m Serilog 
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .WriteTo.Console()
    //.WriteTo.File("logs/JokeService-.txt", rollingInterval: RollingInterval.Day)
    .WriteTo.File("D:/Temp/logs/JokeService-.txt", rollingInterval: RollingInterval.Day)
    .CreateLogger();
#endregion

var builder = Host.CreateApplicationBuilder(args);

#region �ĥ� Serilog �@�� Log ���u��
// Configure your application
builder.Logging.ClearProviders(); // Clear default logging providers
builder.Logging.AddSerilog(); // Use Serilog for logging
#endregion

builder.Services.AddSingleton<JokeService>();
builder.Services.AddHostedService<Worker>();


// ���n: �]����X�� exe �O WinExe: �Y�����t console ������ windows �{��, �Ҧp: Window Form
var isConsoleMode = args.Contains("--console");
var isServiceMode = args.Contains("--service");
var isTrayMode = !isConsoleMode && !isServiceMode;

// If in console mode, attempt to attach to an existing console or create a new one
// �p�G�O Console Mode,
// (1) �p�G�������O console, �N�������ӥ�. �Ҧp: �R�O�C���� ����.
// (2) �p�G���������O console, �N�t�m�@�ӷs�� console ����. �Ҧp: VS2022 ���氻��.
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