using System.Security;

namespace kredi_karti_takip;

public partial class Form1
{
    private readonly NotifyIcon trayIcon = new() { Icon = BrandIcon, Text = "Kart Defteri" };
    private readonly System.Windows.Forms.Timer activationTimer = new() { Interval = 500 };
    private readonly ToolStripMenuItem startupItem = new("Windows açılışında başlat");
    private bool exiting;
    private bool backgroundOnly;
    private bool startupApplied;

    private void InitializeBackgroundMode(bool startInBackground, EventWaitHandle activation)
    {
        backgroundOnly = startInBackground;
        if (backgroundOnly)
        {
            ShowInTaskbar = false;
            Opacity = 0;
        }

        var open = new ToolStripMenuItem("Kart Defteri’ni aç");
        open.Click += (_, _) => RestoreFromTray();
        var exit = new ToolStripMenuItem("Çıkış");
        exit.Click += (_, _) =>
        {
            exiting = true;
            Close();
        };
        startupItem.Visible = StartupRegistration.IsPackaged;
        startupItem.Click += (_, _) => ConfigureStartup(!startupItem.Checked);
        trayIcon.ContextMenuStrip = new ContextMenuStrip();
        trayIcon.ContextMenuStrip.Items.AddRange([open, startupItem, new ToolStripSeparator(), exit]);
        trayIcon.DoubleClick += (_, _) => RestoreFromTray();
        trayIcon.Visible = true;
        activationTimer.Tick += (_, _) =>
        {
            if (activation.WaitOne(0)) RestoreFromTray();
        };
        activationTimer.Start();

        FormClosing += (_, args) =>
        {
            if (args.CloseReason != CloseReason.UserClosing || exiting) return;
            args.Cancel = true;
            Hide();
        };
        FormClosed += (_, _) =>
        {
            activationTimer.Dispose();
            trayIcon.ContextMenuStrip?.Dispose();
            trayIcon.Dispose();
        };
    }

    private void RestoreFromTray()
    {
        backgroundOnly = false;
        ShowInTaskbar = true;
        Opacity = 1;
        if (WindowState == FormWindowState.Minimized) WindowState = FormWindowState.Normal;
        Show();
        Activate();
        if (alarmWindow is { Visible: true }) alarmWindow.Activate();
    }

    private void ConfigureStartup(bool? enabled = null)
    {
        if (!StartupRegistration.IsPackaged) return;
        try
        {
            if (enabled is bool requested)
                StartupRegistration.SetEnabled(requested);
            bool preference = StartupRegistration.IsEnabled();
            if (enabled is null && !startupApplied && !backgroundOnly && connection is not null)
            {
                StartupRegistration.Apply(preference);
                startupApplied = true;
            }
            startupItem.Checked = preference;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or SecurityException or InvalidOperationException)
        {
            Program.LogError(ex);
            startupItem.Checked = false;
            if (enabled is not null || !backgroundOnly)
                MessageBox.Show("Windows otomatik başlatma ayarı kaydedilemedi.", "Kart Defteri");
        }
    }
}
