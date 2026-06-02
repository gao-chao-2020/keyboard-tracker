using System.Runtime.InteropServices;
using System.Threading.Channels;
using KeyboardTracker.Helpers;
using KeyboardTracker.Models;

namespace KeyboardTracker.Services;

public sealed class MouseHookService : IDisposable
{
    private readonly ChannelWriter<InputEvent> _writer;
    private readonly Thread _thread;
    private readonly CancellationTokenSource _cts = new();
    private IntPtr _hookId;
    private NativeMethods.LowLevelHookProc? _proc;
    private int _moveCounter;
    private volatile bool _disposed;
    private const int DecimationRate = 10;

    public uint ThreadId { get; private set; }

    public MouseHookService(ChannelWriter<InputEvent> writer)
    {
        _writer = writer;
        _thread = new Thread(MessagePump)
        {
            Name = "mouse-hook",
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
            NativeMethods.WH_MOUSE_LL,
            _proc,
            hmod,
            0);

        if (_hookId == IntPtr.Zero)
        {
            var err = Marshal.GetLastWin32Error();
            Console.Error.WriteLine($"[MouseHook] SetWindowsHookEx failed: {err}");
            return;
        }

        Console.WriteLine("[MouseHook] Installed. Pumping messages...");

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

        Console.WriteLine("[MouseHook] Exited message pump.");
    }

    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            var msg = (uint)wParam;
            var info = Marshal.PtrToStructure<NativeMethods.MSLLHOOKSTRUCT>(lParam);

            switch (msg)
            {
                case NativeMethods.WM_LBUTTONDOWN:
                case NativeMethods.WM_RBUTTONDOWN:
                case NativeMethods.WM_MBUTTONDOWN:
                case NativeMethods.WM_XBUTTONDOWN:
                    {
                        var button = msg switch
                        {
                            NativeMethods.WM_LBUTTONDOWN => MouseButton.Left,
                            NativeMethods.WM_RBUTTONDOWN => MouseButton.Right,
                            NativeMethods.WM_MBUTTONDOWN => MouseButton.Middle,
                            NativeMethods.WM_XBUTTONDOWN => (info.mouseData >> 16) == NativeMethods.XBUTTON1
                                ? MouseButton.X1 : MouseButton.X2,
                            _ => MouseButton.Left
                        };
                        _writer.TryWrite(new MouseClickEvent
                        {
                            Button = button,
                            X = info.pt.x,
                            Y = info.pt.y,
                            TimeMs = info.time
                        });
                    }
                    break;

                case NativeMethods.WM_MOUSEMOVE:
                    {
                        _moveCounter++;
                        if (_moveCounter % DecimationRate == 0)
                        {
                            _writer.TryWrite(new MouseMoveEvent
                            {
                                X = info.pt.x,
                                Y = info.pt.y,
                                TimeMs = info.time
                            });
                        }
                    }
                    break;
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
        _cts.Dispose();
    }
}
