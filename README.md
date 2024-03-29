# 如何在 .NET 8 建立基於 BackgroundService 的 Windows Service 應用程式 (2)
## How to create Windows Service Application in .NET 8 by BackgroundService (2) 

接續前一篇 "如何在 .NET 8 建立基於 BackgroundService 的 Windows Service 應用程式 (1)" 的內容.  
思考以下情境, 如果該程式是在使用者登入時, 以 Console 模式執行, 那麼在 Windows 10 的工作列上, 是否就很容易被使用者看到, 而去把它關掉. 過程可參考 [附錄一].     

因此, 筆者想到以前在 Windows Form 有一個叫作 TrayIcon 或 <a href="https://learn.microsoft.com/en-us/dotnet/api/system.windows.forms.notifyicon?view=windowsdesktop-8.0" target="_blank">NotifyIcon</a> 元件或類別, 可以將應用程式以圖示的方式, 存放在工作列(Task Bar) 的通知區(Notification Area) 或系統匣(System Tray).  

本文將以前述程式碼為基礎, 添加 NotifyIcon 的功能, 以使整個程式, 得以同時支援 TrayIcon / Console / Windows Service 的使用方式.  

一. [開發過程](#section1)  
二. [發行為單一執行檔](#section2)  
三. [以 TrayIcon 模式執行](#section3)  
四. [以 Console 模式執行](#section4)  
五. [以 Windows Service 模式執行](#section5)  
[附錄一: 將 JokeWorkerService 放在登入時執行](#sectionA)

<a href="https://github.com/jasper-lai/20240330_JokeWorkerServiceTrayIcon" target="_blank">範例由此下載</a>.  

<!-- more -->  

## 一. 開發過程 <a id="section1"></a>

### (一) 修改 .csproj 的內容, 並加入 icon 圖檔  

1.. 修改 .csproj 的設定.  

(1) 原有的設定:  

```xml
<Project Sdk="Microsoft.NET.Sdk.Worker">
	<TargetFramework>net8.0</TargetFramework>
	<OutputType>exe</OutputType>
```

(2) 修改後的設定:  
```xml
<Project Sdk="Microsoft.NET.Sdk">
	<TargetFramework>net8.0-windows</TargetFramework>
	<OutputType>WinExe</OutputType>
    ~~~
    <UseWindowsForms>true</UseWindowsForms>  
```

2.. 加入 TrayIcon 圖示: "icons\my_tray_icon.ico"  

### (二) 修正編譯錯誤  

因為以下原因, 所以, 會發生編譯錯誤, 要調整程式.  
* 專案的型態: 由 Microsoft.NET.Sdk.Worker 改為 Microsoft.NET.Sdk.
* 輸出種類: 由 exe 改為 WinExe.
* 啟用 WindowsForms.

1.. 處理錯誤 (Part 1): Worker.cs
```ini
CS0246	找不到類型或命名空間名稱 'BackgroundService' (是否遺漏了 using 指示詞或組件參考?)
CS0246	找不到類型或命名空間名稱 'ILogger<>' (是否遺漏了 using 指示詞或組件參考?)	
```
加入以下 using 
```csharp
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
```

2.. 處理錯誤 (Part 2): Program.cs
```ini
CS0103	名稱 'Host' 不存在於目前的內容中 (是否遺漏 using 指示詞或組件參考?)  
CS1061	'ILoggingBuilder' 未包含 'ClearProviders' 的定義，也找不到可接受類型 'ILoggingBuilder' 第一個引數的可存取擴充方法 'ClearProviders' (是否遺漏 using 指示詞或組件參考?)	
builder.Services.AddSingleton<JokeService>();  
CS1061	'IServiceCollection' 未包含 'AddSingleton' 的定義，也找不到可接受類型 'IServiceCollection' 第一個引數的可存取擴充方法 'AddSingleton' (是否遺漏 using 指示詞或組件參考?)  
CS1061	'IServiceCollection' 未包含 'AddHostedService' 的定義，也找不到可接受類型 'IServiceCollection' 第一個引數的可存取擴充方法 'AddHostedService' (是否遺漏 using 指示詞或組件參考?)  
```
加入以下 using
```csharp
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
```

### (三) 調整程式邏輯判斷, 以命令列傳入參數, 作為是在 TrayIcon / Console / Windows Service 模式.  

**1.. 修訂 Program.cs**  

(1) 目的: 以傳入的參數, 判斷執行模式:
* 預設: TrayIcon Mode
* --console: Console Mode
* --service: Windows Service Mode

(2) 完整的程式如下:  
其中會需要 WIN32 API Import 是因為 Windows Form (WinExe) 之下是沒有 Console 的, 要自已加上去.  

```csharp
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
```

**2.. 加入 TrayApplicationContext.cs**  
(1) 目的: 處理 TrayIcon, 加上滑鼠右鍵選單.   
(2) 程式碼說明:  

* 建構子: 傳入一個 IHost 的物件, 主要是用以在 Windows Form 裡啟動背景服務之用
  * Task.Run(() => _appHost.StartAsync());
* 建構子: 建立一個滑鼠右鍵選單, 只有 [Exit] 的功能, 以執行 ExitApplication() 函式, 結束程式執行.  
* ExitApplication(): 結束背景服務  
  * await _appHost.StopAsync();  
* Dispose(): 最後會執行到這個函式, 以釋放 trayIcon 物件.  

```csharp
public class TrayApplicationContext : ApplicationContext
{
    private readonly NotifyIcon trayIcon;
    private readonly IHost _appHost;

    public TrayApplicationContext(IHost host)
    {
        _appHost = host;

        Icon myIcon = new Icon("icons/my_tray_icon.ico");

        // Create and configure the tray icon
        trayIcon = new NotifyIcon
        {
            Icon = myIcon,
            //Icon = SystemIcons.Application, // Default icon
            Text = "JokeWorkerServiceTrayIcon", // Default tooltip text
            Visible = true,
            ContextMenuStrip = new ContextMenuStrip()
        };
        trayIcon.ContextMenuStrip.Items.Add("Exit", null, (sender, e) => ExitApplication());

        // 執行背景服務
        Task.Run(() => _appHost.StartAsync());
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            trayIcon?.Dispose();
        }
        base.Dispose(disposing);
    }

    private async void ExitApplication()
    {
        trayIcon.Visible = false;
        // _appHost.StopAsync().GetAwaiter().GetResult();
        await _appHost.StopAsync();
        Application.Exit();
    }
}
```

**3.. 請留意: 此步驟並未修訂 Worker.cs, 只是加上 Windows Form, 控制 Worker 這個背景服務的生命週期.** 
## 二. 發行為單一執行檔 <a id="section2"></a>

仿照前一篇的作法, [方式1] 使用 Visual Studio 2022 發佈 (publish) 或 [方式2] 使用 dotnet CLI 均可.  

**1.. [方式1] 使用 Visual Studio 2022 發佈 (publish)**  
![21 Publish_by_VS2022](pictures/21-Publish_by_VS2022.png)  
![22 Publish_by_VS2022](pictures/22-Publish_by_VS2022.png)  
![23 Publish_by_VS2022](pictures/23-Publish_by_VS2022.png)  
![24 Publish_by_VS2022](pictures/24-Publish_by_VS2022.png)  
![25 Publish_by_VS2022](pictures/25-Publish_by_VS2022.png)  
![26 Publish_by_VS2022](pictures/26-Publish_by_VS2022.png)  
![27 Publish_by_VS2022](pictures/27-Publish_by_VS2022.png)  
![28 Publish_by_VS2022](pictures/28-Publish_by_VS2022.png)  

**2.. [方式2] 使用 dotnet CLI**  
使用 dotnet publish -c Release -r win-x64 --no-self-contained -p:PublishSingleFile=true 編譯成單一 .exe 檔.  

以**系統管理員身份**, 在 Visual Studio 2022 Developer Command Prompt 執行以下指令.  

```powershell
D:\Temp\JokeWorkerServiceTrayIcon> dotnet publish -c Release -r win-x64 --no-self-contained -p:PublishSingleFile=true
.NET 的 MSBuild 版本 17.9.6+a4ecab324
  正在判斷要還原的專案...
  已還原 ~~~
  JokeWorkerServiceTrayIcon\bin\Release\net8.0-windows\win-x64\publish\

D:\Temp\JokeWorkerServiceTrayIcon> dir bin\Release\net8.0-windows\win-x64\publish\
2024/03/29  下午 12:01    <DIR>          icons
2024/03/29  下午 01:59         1,777,912 JokeWorkerServiceTrayIcon.exe
2024/03/29  下午 01:59            16,976 JokeWorkerServiceTrayIcon.pdb
```

3.. 請留意: 前述的 VS 2022 發佈 (約 3MB), 跟 dotnet CLI 發佈 (約 1.7MB) 的差異, 在於是否有 [V] <a href="https://learn.microsoft.com/zh-tw/dotnet/core/deploying/ready-to-run" target="_blank">啟用 ReadyToRun 編譯</a>. dotnet CLI 那串指令, 等同沒有打 V 啟用 ReadyToRun 編譯.  

4.. 複製檔案到 D:\Temp\publish\JokeWorkerServiceTrayIcon
```powershell
D:\Temp\JokeWorkerServiceTrayIcon> xcopy bin\Release\net8.0-windows\win-x64\publish\* D:\Temp\publish\JokeWorkerServiceTrayIcon /s
```

## 三. 以 TrayIcon 模式執行 <a id="section3"></a>

1.. 在檔案總管 (D:\Temp\publish\JokeWorkerServiceTrayIcon) double-click JokeWorkerServiceTrayIcon.exe  
![31 Run_in_TrayIcon_mode](pictures/31-Run_in_TrayIcon_mode.png)  
![32 Run_in_TrayIcon_mode](pictures/32-Run_in_TrayIcon_mode.png)  

2.. 檢查 Log 記錄檔.  

```ini
2024-03-29 14:11:42.581 +08:00 [INF] === in TrayIcon Mode ===
2024-03-29 14:11:42.858 +08:00 [INF] Service started
2024-03-29 14:11:42.873 +08:00 [WRN] ['hip', 'hip']
(hip hip array)
~~~
2024-03-29 14:13:02.986 +08:00 [WRN] What did the router say to the doctor?
It hurts when IP.
2024-03-29 14:13:12.987 +08:00 [WRN] What's the object-oriented way to become wealthy?
Inheritance
2024-03-29 14:13:17.222 +08:00 [INF] Application is shutting down...
2024-03-29 14:13:17.223 +08:00 [INF] Service stopped
```

## 四. 以 Console 模式執行 <a id="section4"></a>

在 Visual Studio 2022 Developer Command Prompt 執行以下指令. 

1.. 切換資料夾到 "D:\Temp\publish\JokeWorkerServiceTrayIcon".  
```powershell
D:\Temp>cd D:\Temp\publish\JokeWorkerServiceTrayIcon
```

2.. 執行 JokeWorkerServiceTrayIcon.exe --console  
![41 Run_in_Console_mode](pictures/41-Run_in_Console_mode.png)  

3.. 檢查 Log 記錄檔.  
```ini
2024-03-29 14:16:53.396 +08:00 [INF] === in Console Mode ===
2024-03-29 14:16:53.497 +08:00 [INF] Service started
2024-03-29 14:16:53.505 +08:00 [WRN] 3 SQL statements walk into a NoSQL bar. Soon, they walk out
They couldn't find a table.
2024-03-29 14:16:53.512 +08:00 [INF] Application started. Press Ctrl+C to shut down.
2024-03-29 14:16:53.512 +08:00 [INF] Hosting environment: Production
2024-03-29 14:16:53.512 +08:00 [INF] Content root path: D:\Temp\publish\JokeWorkerServiceTrayIcon
2024-03-29 14:17:03.519 +08:00 [WRN] 3 SQL statements walk into a NoSQL bar. Soon, they walk out
They couldn't find a table.
2024-03-29 14:17:13.535 +08:00 [WRN] There are 10 types of people in this world...
Those who understand binary and those who don't
2024-03-29 14:17:23.536 +08:00 [WRN] How do you check if a webpage is HTML5?
Try it out on Internet Explorer
2024-03-29 14:17:24.412 +08:00 [INF] Application is shutting down...
2024-03-29 14:17:24.414 +08:00 [INF] Service stopped
```

## 五. 以 Windows Service 模式執行 <a id="section5"></a>

以**系統管理員身份**, 在 Visual Studio 2022 Developer Command Prompt 執行以下指令.  

1.. 建立 Windows Service, 並設為自動啟動, 且給予描述.  
```powershell
D:\Temp\publish\JokeWorkerServiceTrayIcon>sc create ".NET8 Joke Service TrayIcon" binpath="D:\Temp\publish\JokeWorkerServiceTrayIcon\JokeWorkerServiceTrayIcon.exe --service" start=auto
[SC] CreateService 成功

D:\Temp\publish\JokeWorkerServiceTrayIcon>sc description ".NET8 Joke Service TrayIcon" "This is a big joke ..."
[SC] ChangeServiceConfig2 成功
```

對照一下 "服務" 裡的狀況, 確定有註冊成功, 且為自動啟動.  
![51 Services_List](pictures/51-Services_List.png)


3.. 啟動服務.  
```powershell
D:\Temp\publish\JokeWorkerServiceTrayIcon>sc start ".NET8 Joke Service TrayIcon"
SERVICE_NAME: .NET8 Joke Service TrayIcon
        TYPE               : 10  WIN32_OWN_PROCESS
        STATE              : 2  START_PENDING
                                (NOT_STOPPABLE, NOT_PAUSABLE, IGNORES_SHUTDOWN)
        WIN32_EXIT_CODE    : 0  (0x0)
        SERVICE_EXIT_CODE  : 0  (0x0)
        CHECKPOINT         : 0x0
        WAIT_HINT          : 0x7d0
        PID                : 31044
        FLAGS              :
```

4.. 停止服務.  
```powershell
D:\Temp\publish\JokeWorkerServiceTrayIcon>sc stop ".NET8 Joke Service TrayIcon"
SERVICE_NAME: .NET8 Joke Service TrayIcon
        TYPE               : 10  WIN32_OWN_PROCESS
        STATE              : 3  STOP_PENDING
                                (STOPPABLE, NOT_PAUSABLE, ACCEPTS_SHUTDOWN)
        WIN32_EXIT_CODE    : 0  (0x0)
        SERVICE_EXIT_CODE  : 0  (0x0)
        CHECKPOINT         : 0x0
        WAIT_HINT          : 0x0
```

5.. 刪除服務.  
```powershell
D:\Temp\publish\JokeWorkerServiceTrayIcon>sc delete ".NET8 Joke Service TrayIcon"
[SC] DeleteService 成功
```

6.. 檢查 Log 記錄檔.  
```ini
2024-03-29 14:26:49.555 +08:00 [INF] === in Service Mode ===
2024-03-29 14:26:49.745 +08:00 [INF] Service started
2024-03-29 14:26:49.751 +08:00 [WRN] What did the router say to the doctor?
It hurts when IP.
2024-03-29 14:26:49.913 +08:00 [INF] Application started. Hosting environment: Production; Content root path: D:\Temp\publish\JokeWorkerServiceTrayIcon\
2024-03-29 14:26:59.913 +08:00 [WRN] Which song would an exception sing?
Can't catch me - Avicii
2024-03-29 14:27:09.916 +08:00 [WRN] There are 10 types of people in this world...
Those who understand binary and those who don't
2024-03-29 14:27:19.916 +08:00 [WRN] If you put a million monkeys at a million keyboards, one of them will eventually write a Java program
the rest of them will write Perl
2024-03-29 14:27:29.924 +08:00 [WRN] If you put a million monkeys at a million keyboards, one of them will eventually write a Java program
the rest of them will write Perl
2024-03-29 14:27:31.220 +08:00 [INF] Application is shutting down...
2024-03-29 14:27:31.223 +08:00 [INF] Service stopped
```

7.. 仿照 [附錄1], 把 JokeWorkerServiceTrayIcon.exe 也加到登入後執行.  
重新登入後, 結果如下:  
![52 Add_WorkerServiceTrayIcon_to_login](pictures/52-Add_WorkerServiceTrayIcon_to_login.png)  

不過, 這 2 個登入後執行的程式, 都寫到同一個 Log 檔, 造成辨識困擾. 其實, 2 者功能相同, 只要執行其中一個就好; 這裡只是為了解說方便.  

如果真的要同時執行 TrayIcon / Console / Windows Service, 那就調整 Program.cs, 依 isConsoleMode, isServiceMode, isTrayMode 設置不同的 Log 檔名就可以了.  


## 附錄一: 將 JokeWorkerService 放在登入時執行  <a id="sectionA"></a>

1.. Windows + R : 輸入 shell:startup  
![A1 Add_Program_on_User_Login](pictures/A1-Add_Program_on_User_Login.png)  

2.. 按滑鼠右鍵, 新增捷徑  
![A2 Add_Program_on_User_Login](pictures/A2-Add_Program_on_User_Login.png)  
![A3 Add_Program_on_User_Login](pictures/A3-Add_Program_on_User_Login.png)  
![A4 Add_Program_on_User_Login](pictures/A4-Add_Program_on_User_Login.png)  
![A5 Add_Program_on_User_Login](pictures/A5-Add_Program_on_User_Login.png)  

3.. 重新登入 Windows, 可以看到一個 Console 在執行中.  
![A6 Add_Program_on_User_Login](pictures/A6-Add_Program_on_User_Login.png)  

## 參考文件

* <a href="https://blog.darkthread.net/blog/net6-windows-service/" target="_blank">(黑暗執行緒) 使用 .NET 6 開發 Windows Service</a>  
* <a href="https://blog.darkthread.net/blog/dotnet6-publish-notes/" target="_blank">(黑暗執行緒) 使用 dotnet 命令列工具發行 .NET 6 專案</a>  

* <a href="https://learn.microsoft.com/zh-tw/dotnet/core/extensions/windows-service" target="_blank">(Microsoft)(中文版) 使用 BackgroundService 建立 Windows 服務</a>  
* <a href="https://learn.microsoft.com/en-us/dotnet/core/extensions/windows-service" target="_blank">(Microsoft)(英文版) Create Windows Service using BackgroundService</a>  
* <a href="https://github.com/dotnet/samples/tree/main/core/workers" target="_blank">(Microsoft)(英文版) Sample Source in GitHub</a>  

* <a href="https://learn.microsoft.com/zh-tw/dotnet/api/system.windows.forms.notifyicon?view=windowsdesktop-8.0" target="_blank">(Microsoft) NotifyIcon 類別</a>  
* <a href="https://learn.microsoft.com/zh-tw/dotnet/desktop/winforms/controls/app-icons-to-the-taskbar-with-wf-notifyicon?view=netframeworkdesktop-4.8" target="_blank">(Microsoft) 如何：使用 Windows Form NotifyIcon 元件將應用程式圖示加入至 TaskBar</a>  
* <a href="https://learn.microsoft.com/zh-tw/windows/console/attachconsole" target="_blank">(Microsoft) AttachConsole 函式</a>   
* <a href="https://learn.microsoft.com/zh-tw/windows/console/allocconsole" target="_blank">(Microsoft) AllocConsole 函式</a>  
* <a href="https://learn.microsoft.com/zh-tw/dotnet/core/deploying/ready-to-run" target="_blank">(Microsoft) ReadyToRun 編譯</a>  

