using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace QuickSIP.Services;

public static class TaskbarFlasher
{
    private const uint FlashwStop = 0;
    private const uint FlashwAll = 3;
    private const uint FlashwTimernofg = 12;

    public static void Start(Window window)
    {
        Flash(window, FlashwAll | FlashwTimernofg, uint.MaxValue);
    }

    public static void Stop(Window window)
    {
        Flash(window, FlashwStop, 0);
    }

    private static void Flash(Window window, uint flags, uint count)
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var hwnd = new WindowInteropHelper(window).Handle;
        if (hwnd == 0)
        {
            hwnd = new WindowInteropHelper(window).EnsureHandle();
        }

        var info = new FlashWInfo
        {
            CbSize = (uint)Marshal.SizeOf<FlashWInfo>(),
            Hwnd = hwnd,
            DwFlags = flags,
            UCount = count,
            DwTimeout = 0
        };
        _ = FlashWindowEx(ref info);
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool FlashWindowEx(ref FlashWInfo pwfi);

    [StructLayout(LayoutKind.Sequential)]
    private struct FlashWInfo
    {
        public uint CbSize;
        public IntPtr Hwnd;
        public uint DwFlags;
        public uint UCount;
        public uint DwTimeout;
    }
}
