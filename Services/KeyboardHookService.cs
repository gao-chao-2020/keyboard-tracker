using System.Runtime.InteropServices;
using System.Threading.Channels;
using KeyboardTracker.Helpers;
using KeyboardTracker.Models;

namespace KeyboardTracker.Services;

public sealed class KeyboardHookService : IDisposable
{
    private readonly ChannelWriter<InputEvent> _writer;
    private readonly Thread _thread;
    private readonly CancellationTokenSource _cts = new();
    private IntPtr _hookId;
    private NativeMethods.LowLevelHookProc? _proc;
    private volatile bool _disposed;

    public uint ThreadId { get; private set; }

    public KeyboardHookService(ChannelWriter<InputEvent> writer)
    {
        _writer = writer;
        _thread = new Thread(MessagePump)
        {
            Name = "keyboard-hook",
            IsBackground = true
        };
    }

    public void Start()
    {
        _thread.Start();
    }

    private void MessagePump()
    {
        ThreadId = NativeMethods.GetCurrentThreadId();

        _proc = HookCallback;
        var hmod = NativeMethods.GetModuleHandle(null);
        _hookId = NativeMethods.SetWindowsHookExW(
            NativeMethods.WH_KEYBOARD_LL,
            _proc,
            hmod,
            0);

        if (_hookId == IntPtr.Zero)
        {
            var err = Marshal.GetLastWin32Error();
            Console.Error.WriteLine($"[KeyboardHook] SetWindowsHookEx failed: {err}");
            return;
        }

        Console.WriteLine("[KeyboardHook] Installed. Pumping messages...");

        while (!_cts.IsCancellationRequested)
        {
            if (NativeMethods.GetMessageW(out var msg, IntPtr.Zero, 0, 0) <= 0)
                break;

            NativeMethods.TranslateMessage(ref msg);
            NativeMethods.DispatchMessageW(ref msg);
        }

        if (_hookId != IntPtr.Zero)
        {
            NativeMethods.UnhookWindowsHookEx(_hookId);
            _hookId = IntPtr.Zero;
        }

        Console.WriteLine("[KeyboardHook] Exited message pump.");
    }

    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            var msg = (uint)wParam;
            if (msg is NativeMethods.WM_KEYDOWN or NativeMethods.WM_SYSKEYDOWN)
            {
                var info = Marshal.PtrToStructure<NativeMethods.KBDLLHOOKSTRUCT>(lParam);
                var injected = (info.flags & NativeMethods.LLKHF_INJECTED) != 0;
                if (!injected)
                {
                    var evt = new KeyEvent
                    {
                        VkCode = info.vkCode,
                        ScanCode = info.scanCode,
                        IsExtended = (info.flags & NativeMethods.LLKHF_EXTENDED) != 0,
                        TimeMs = info.time
                    };
                    _writer.TryWrite(evt);
                }
            }
        }
        return NativeMethods.CallNextHookEx(IntPtr.Zero, nCode, wParam, lParam);
    }

    public void SignalStop()
    {
        if (_cts.IsCancellationRequested) return;
        _cts.Cancel();
        if (ThreadId != 0)
            NativeMethods.PostThreadMessageW(ThreadId, NativeMethods.WM_QUIT, UIntPtr.Zero, IntPtr.Zero);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        SignalStop();
        // Don't Join() here — let App orchestrate shutdown
        _cts.Dispose();
    }
}
