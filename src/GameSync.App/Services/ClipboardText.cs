using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace GameSync.App.Services;

/// <summary>
/// Win32 clipboard write. WinRT Clipboard.SetContent often throws COMException in unpackaged WinUI.
/// </summary>
[SupportedOSPlatform("windows10.0.19041.0")]
internal static class ClipboardText
{
    private const uint CfUnicodeText = 13;
    private const uint GmemMoveable = 0x0002;

    public static void SetText(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        if (!OpenClipboard(IntPtr.Zero))
        {
            throw new InvalidOperationException("Could not open the Windows clipboard.");
        }

        try
        {
            EmptyClipboard();

            var bytes = (text.Length + 1) * sizeof(char);
            var hGlobal = GlobalAlloc(GmemMoveable, (UIntPtr)bytes);
            if (hGlobal == IntPtr.Zero)
            {
                throw new InvalidOperationException("Could not allocate clipboard memory.");
            }

            var target = GlobalLock(hGlobal);
            if (target == IntPtr.Zero)
            {
                GlobalFree(hGlobal);
                throw new InvalidOperationException("Could not lock clipboard memory.");
            }

            try
            {
                Marshal.Copy(text.ToCharArray(), 0, target, text.Length);
                Marshal.WriteInt16(target, text.Length * sizeof(char), 0);
            }
            finally
            {
                GlobalUnlock(hGlobal);
            }

            if (SetClipboardData(CfUnicodeText, hGlobal) == IntPtr.Zero)
            {
                GlobalFree(hGlobal);
                throw new InvalidOperationException("Could not set clipboard data.");
            }

            // Ownership of hGlobal transferred to the system on success.
        }
        finally
        {
            CloseClipboard();
        }
    }

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool OpenClipboard(IntPtr hWndNewOwner);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseClipboard();

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EmptyClipboard();

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetClipboardData(uint uFormat, IntPtr hMem);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GlobalAlloc(uint uFlags, UIntPtr dwBytes);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GlobalLock(IntPtr hMem);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalUnlock(IntPtr hMem);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GlobalFree(IntPtr hMem);
}
