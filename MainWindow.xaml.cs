using System.Windows;
using KeyboardTracker.Services;

namespace KeyboardTracker;

public partial class MainWindow : Window
{
    private readonly NotifyIconService _tray;

    public MainWindow()
    {
        InitializeComponent();
        _tray = new NotifyIconService(this);
    }

    public void ExitApplication()
    {
        _tray.ExitApplication();
    }
}
