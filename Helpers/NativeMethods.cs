using System.Runtime.InteropServices;

namespace KeyboardTracker.Helpers;

public static class NativeMethods
{
    // ── Hook Constants ──
    public const int WH_KEYBOARD_LL = 13;
    public const int WH_MOUSE_LL    = 14;

    // ── Keyboard Messages ──
    public const int WM_KEYDOWN    = 0x0100;
    public const int WM_KEYUP      = 0x0101;
    public const int WM_SYSKEYDOWN = 0x0104;
    public const int WM_SYSKEYUP   = 0x0105;

    // ── Mouse Messages ──
    public const int WM_MOUSEMOVE     = 0x0200;
    public const int WM_LBUTTONDOWN   = 0x0201;
    public const int WM_LBUTTONUP     = 0x0202;
    public const int WM_RBUTTONDOWN   = 0x0204;
    public const int WM_RBUTTONUP     = 0x0205;
    public const int WM_MBUTTONDOWN   = 0x0207;
    public const int WM_MBUTTONUP     = 0x0208;
    public const int WM_XBUTTONDOWN   = 0x020B;
    public const int WM_XBUTTONUP     = 0x020C;

    // ── Hook Flags ──
    public const int LLKHF_INJECTED     = 0x00000010;
    public const int LLKHF_EXTENDED     = 0x00000001;
    public const int LLKHF_ALTDOWN      = 0x00000020;

    // ── XButton ──
    public const ushort XBUTTON1 = 1;
    public const ushort XBUTTON2 = 2;

    // ── Shell Notify ──
    public const int NIM_ADD    = 0;
    public const int NIM_MODIFY = 1;
    public const int NIM_DELETE = 2;
    public const int NIF_ICON   = 0x00000002;
    public const int NIF_MESSAGE = 0x00000001;
    public const int NIF_TIP    = 0x00000004;
    public const int NIF_STATE  = 0x00000008;
    public const int NIF_INFO   = 0x00000010;
    public const int WM_APP     = 0x8000;

    // ── WM_COMMAND, WM_DESTROY ──
    public const int WM_COMMAND    = 0x0111;
    public const int WM_DESTROY    = 0x0002;
    public const int WM_CLOSE      = 0x0010;
    public const int WM_QUIT       = 0x0012;
    public const int WM_LBUTTONDBLCLK = 0x0203;

    public const int TPM_RIGHTBUTTON = 2;
    public const int MF_STRING       = 0x00000000;
    public const int MF_SEPARATOR    = 0x00000800;

    // ── Windows Message ──
    public const int WM_QUERYENDSESSION = 0x0011;
    public const int WM_ENDSESSION      = 0x0016;

    // ── Structs ──

    [StructLayout(LayoutKind.Sequential)]
    public struct POINT
    {
        public int x;
        public int y;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct MSG
    {
        public IntPtr hwnd;
        public uint message;
        public UIntPtr wParam;
        public IntPtr lParam;
        public uint time;
        public POINT pt;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct KBDLLHOOKSTRUCT
    {
        public uint vkCode;
        public uint scanCode;
        public int flags;
        public uint time;
        public UIntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct MSLLHOOKSTRUCT
    {
        public POINT pt;
        public uint mouseData;
        public int flags;
        public uint time;
        public UIntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct NOTIFYICONDATAW
    {
        public int cbSize;
        public IntPtr hWnd;
        public uint uID;
        public uint uFlags;
        public uint uCallbackMessage;
        public IntPtr hIcon;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string szTip;
        public uint dwState;
        public uint dwStateMask;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        public string szInfo;
        public uint uVersion;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
        public string szInfoTitle;
        public uint dwInfoFlags;
        public Guid guidItem;
        public IntPtr hBalloonIcon;
    }

    // ── Delegates ──

    public delegate IntPtr LowLevelHookProc(int nCode, IntPtr wParam, IntPtr lParam);

    // ── Kernel32 ──

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    public static extern IntPtr GetModuleHandle(string? lpModuleName);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool CloseHandle(IntPtr hObject);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern uint GetCurrentThreadId();

    // ── User32 ──

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    public static extern IntPtr SetWindowsHookExW(
        int idHook,
        LowLevelHookProc lpfn,
        IntPtr hMod,
        uint dwThreadId);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll")]
    public static extern IntPtr CallNextHookEx(
        IntPtr hhk,
        int nCode,
        IntPtr wParam,
        IntPtr lParam);

    [DllImport("user32.dll")]
    public static extern int GetMessageW(
        out MSG lpMsg,
        IntPtr hWnd,
        uint wMsgFilterMin,
        uint wMsgFilterMax);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool TranslateMessage([In] ref MSG lpMsg);

    [DllImport("user32.dll")]
    public static extern IntPtr DispatchMessageW([In] ref MSG lpMsg);

    [DllImport("user32.dll")]
    public static extern void PostThreadMessageW(
        uint idThread,
        uint Msg,
        UIntPtr wParam,
        IntPtr lParam);

    [DllImport("user32.dll")]
    public static extern void PostQuitMessage(int nExitCode);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern int GetKeyNameTextW(int lParam, char[] lpString, int cchSize);

    // ── Shell32 ──

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool Shell_NotifyIconW(uint dwMessage, ref NOTIFYICONDATAW lpData);

    // ── User32 Shell ──

    [DllImport("user32.dll")]
    public static extern IntPtr CreatePopupMenu();

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool AppendMenuW(IntPtr hMenu, uint uFlags, UIntPtr uIDNewItem, string? lpNewItem);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    public static extern int TrackPopupMenu(
        IntPtr hMenu,
        uint uFlags,
        int x,
        int y,
        int nReserved,
        IntPtr hWnd,
        IntPtr prcRect);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool DestroyMenu(IntPtr hMenu);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool GetCursorPos(out POINT lpPoint);
}
