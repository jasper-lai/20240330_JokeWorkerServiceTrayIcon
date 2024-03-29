namespace JokeWorkerServiceTrayIcon
{
    using System.Threading.Tasks;
    using System.Drawing;
    using System.Windows.Forms;
    using Microsoft.Extensions.Hosting;
    using System;

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
            // trayIcon.ContextMenuStrip.Items.Add("Change Icon and Text", null, ChangeIconAndText);
            trayIcon.ContextMenuStrip.Items.Add("Exit", null, (sender, e) => ExitApplication());

            // 執行背景服務
            Task.Run(() => _appHost.StartAsync());
        }

        //private void ChangeIconAndText(object? sender, EventArgs e)
        //{
        //    // Load a new icon from a file or resource
        //    Icon newIcon = new Icon("icons/my_tray_icon.ico");
        //    trayIcon.Icon = newIcon;

        //    // Update the tooltip text
        //    trayIcon.Text = "Hello World";
        //}

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
}
