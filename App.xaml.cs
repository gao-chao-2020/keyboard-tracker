using System.IO;
using System.Threading.Channels;
using System.Windows;
using KeyboardTracker.Helpers;
using KeyboardTracker.Models;
using KeyboardTracker.Services;
using Application = System.Windows.Application;
using MessageBox = System.Windows.MessageBox;

namespace KeyboardTracker;

public partial class App : Application
{
    private KeyboardHookService? _keyboardHook;
    private MouseHookService? _mouseHook;
    private StatsProcessor? _processor;
    private DatabaseService? _db;
    private CancellationTokenSource? _appCts;
    private SingleInstance? _singleInstance;

    public DatabaseService? Database => _db;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _singleInstance = new SingleInstance("Global\\KeyboardTracker_SingleInstance");
        if (!_singleInstance.IsFirstInstance)
        {
            MessageBox.Show("Keyboard Tracker is already running.", "Keyboard Tracker",
                MessageBoxButton.OK, MessageBoxImage.Information);
            Shutdown();
            return;
        }

        _appCts = new CancellationTokenSource();

        var dbPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "KeyboardTracker", "stats.db");
        _db = new DatabaseService(dbPath);

        var channel = Channel.CreateBounded<InputEvent>(new BoundedChannelOptions(10000)
        {
            FullMode = BoundedChannelFullMode.DropWrite
        });

        _keyboardHook = new KeyboardHookService(channel.Writer);
        _mouseHook = new MouseHookService(channel.Writer);
        _processor = new StatsProcessor(_db, channel.Reader);

        _keyboardHook.Start();
        _mouseHook.Start();

    }

    protected override void OnExit(ExitEventArgs e)
    {

        _keyboardHook?.SignalStop();
        _mouseHook?.SignalStop();

        // Give hooks time to exit their message pumps
        Thread.Sleep(100);

        _processor?.Dispose();
        _appCts?.CancelAfter(1000);

        _keyboardHook?.Dispose();
        _mouseHook?.Dispose();
        _db?.Dispose();
        _appCts?.Dispose();
        _singleInstance?.Dispose();

        base.OnExit(e);
    }
}
