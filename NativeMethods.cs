using System.Runtime.InteropServices;
using System.Windows.Interop;

namespace ClipNestWpf;

static class NativeMethods
{
    public const int WM_HOTKEY = 0x0312;
    public const int WM_CLIPBOARDUPDATE = 0x031D;
    public const int WM_GETMINMAXINFO = 0x0024;
    public const int WM_SYSCOMMAND = 0x0112;
    public const int SC_MAXIMIZE = 0xF030;
    public const int SC_MINIMIZE = 0xF020;
    public const int MOD_CONTROL = 2;
    public const int HOTKEY_ID = 1;

    public const int WS_EX_TOOLWINDOW = 0x80;
    public const int WS_EX_TOPMOST = 0x8;
    public const int WS_EX_NOACTIVATE = 0x08000000;
    public const int WS_EX_TRANSPARENT = 0x20;
    public const int WS_EX_LAYERED = 0x80000;
    public const int LWA_ALPHA = 0x2;

    public const int GWL_EXSTYLE = -20;

    [DllImport("user32.dll")]
    public static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll")]
    public static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    [DllImport("user32.dll")]
    public static extern bool AddClipboardFormatListener(IntPtr hwnd);

    [DllImport("user32.dll")]
    public static extern bool RemoveClipboardFormatListener(IntPtr hwnd);

    [DllImport("user32.dll")]
    public static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

    [DllImport("user32.dll")]
    public static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll")]
    public static extern bool SetLayeredWindowAttributes(IntPtr hwnd, uint crKey, byte bAlpha, uint dwFlags);

    [DllImport("user32.dll")]
    public static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

    public static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
    public const uint SWP_NOSIZE = 0x0001;
    public const uint SWP_NOMOVE = 0x0002;
    public const uint SWP_NOACTIVATE = 0x0010;
    public const uint SWP_SHOWWINDOW = 0x0040;

    [StructLayout(LayoutKind.Sequential)]
    public struct POINT { public int X; public int Y; }

    [StructLayout(LayoutKind.Sequential)]
    public struct MINMAXINFO
    {
        public POINT ptReserved;
        public POINT ptMaxSize;
        public POINT ptMaxPosition;
        public POINT ptMinTrackSize;
        public POINT ptMaxTrackSize;
    }

    public static void LimitMaxTrackSize(IntPtr lParam, int maxW, int maxH)
    {
        var mmi = Marshal.PtrToStructure<MINMAXINFO>(lParam);
        mmi.ptMaxTrackSize.X = maxW;
        mmi.ptMaxTrackSize.Y = maxH;
        Marshal.StructureToPtr(mmi, lParam, false);
    }
}
