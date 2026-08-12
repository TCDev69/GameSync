using Microsoft.UI.Xaml;
using WinRT.Interop;

namespace GameSync.App.Services;

public static class PickerInterop
{
    public static void Attach(object picker)
    {
        var window = App.MainWindow ?? throw new InvalidOperationException("Main window is not available.");
        Attach(picker, window);
    }

    public static void Attach(object picker, Window window)
    {
        var hwnd = WindowNative.GetWindowHandle(window);
        InitializeWithWindow.Initialize(picker, hwnd);
    }
}
