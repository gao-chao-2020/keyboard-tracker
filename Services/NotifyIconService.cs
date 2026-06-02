using System.Drawing;
using System.Windows;
using System.Windows.Forms;
using Application = System.Windows.Application;
using FontStyle = System.Drawing.FontStyle;

namespace KeyboardTracker.Services;

public sealed class NotifyIconService : IDisposable
{
    private readonly Window _window;
    private readonly NotifyIcon _notifyIcon;
    private bool _disposed;

    public NotifyIconService(Window window)
    {
        _window = window;
        _window.Closing += OnWindowClosing;

        var icon = CreateIcon();
        _notifyIcon = new NotifyIcon
        {
            Icon = icon,
            Text = "Keyboard Tracker",
            Visible = true,
        };

        _notifyIcon.DoubleClick += (_, _) => ShowWindow();
        _notifyIcon.BalloonTipTitle = "Keyboard Tracker";
        _notifyIcon.BalloonTipText = "Running in background. Right-click to exit, double-click to open.";
        _notifyIcon.BalloonTipIcon = ToolTipIcon.Info;
        _notifyIcon.ShowBalloonTip(3000);

        var menu = new ContextMenuStrip();
        menu.Items.Add("Show", null, (_, _) => ShowWindow());
        menu.Items.Add("-");
        menu.Items.Add("Exit", null, (_, _) => ExitApplication());
        _notifyIcon.ContextMenuStrip = menu;
    }

    private static Icon CreateIcon()
    {
        using var bmp = new Bitmap(32, 32);
        using var g = Graphics.FromImage(bmp);
        g.Clear(Color.Transparent);
        using var brush = new SolidBrush(Color.DodgerBlue);
        g.FillEllipse(brush, 2, 2, 28, 28);
        using var pen = new Pen(Color.White, 2f);
        g.DrawEllipse(pen, 2, 2, 28, 28);
        using var font = new Font("Segoe UI", 10f, FontStyle.Bold);
        using var textBrush = new SolidBrush(Color.White);
        g.DrawString("K", font, textBrush, 9, 6);
        return Icon.FromHandle(bmp.GetHicon());
    }

    private void OnWindowClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (!_disposed)
        {
            e.Cancel = true;
            _window.Hide();
            _window.ShowInTaskbar = false;
        }
    }

    public void ShowWindow()
    {
        _window.Dispatcher.Invoke(() =>
        {
            _window.ShowInTaskbar = true;
            _window.Show();
            _window.WindowState = WindowState.Normal;
            _window.Activate();
        });
    }

    public void ExitApplication()
    {
        _disposed = true;
        _notifyIcon.Visible = false;
        Application.Current.Shutdown();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
    }
}
