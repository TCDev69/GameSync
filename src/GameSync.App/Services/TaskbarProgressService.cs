using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using GameSync.Core.Models;
using Microsoft.UI.Xaml;
using WinRT.Interop;

namespace GameSync.App.Services;

/// <summary>
/// Mirrors <see cref="AppActivityService"/> progress on registered window taskbar buttons.
/// </summary>
public sealed class TaskbarProgressService : IDisposable
{
    private readonly AppActivityService _activity;
    private readonly ConcurrentDictionary<nint, byte> _windows = new();
    private readonly ITaskbarList3 _taskbarList;
    private bool _disposed;

    public TaskbarProgressService(AppActivityService activity)
    {
        _activity = activity;
        _taskbarList = (ITaskbarList3)Activator.CreateInstance(
            Type.GetTypeFromCLSID(new Guid("56FDF344-FD6D-11d0-958A-006097C9A090"))
            ?? throw new PlatformNotSupportedException("Windows taskbar progress is unavailable."))!;
        _taskbarList.HrInit();
        _activity.PropertyChanged += OnActivityChanged;
    }

    public void Register(Window window)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var hwnd = WindowNative.GetWindowHandle(window);
        _windows.TryAdd(hwnd, 0);
        ApplyToWindow(hwnd);
    }

    public void Unregister(Window window)
    {
        var hwnd = WindowNative.GetWindowHandle(window);
        _windows.TryRemove(hwnd, out _);
        _taskbarList.SetProgressState(hwnd, TaskbarProgressBarState.NoProgress);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _activity.PropertyChanged -= OnActivityChanged;
        foreach (var hwnd in _windows.Keys)
        {
            _taskbarList.SetProgressState(hwnd, TaskbarProgressBarState.NoProgress);
        }

        _windows.Clear();
    }

    private void OnActivityChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e) =>
        RefreshAll();

    private void RefreshAll()
    {
        foreach (var hwnd in _windows.Keys)
        {
            ApplyToWindow(hwnd);
        }
    }

    private void ApplyToWindow(nint hwnd)
    {
        if (!_activity.IsActive)
        {
            _taskbarList.SetProgressState(hwnd, TaskbarProgressBarState.NoProgress);
            return;
        }

        if (_activity.IsError)
        {
            _taskbarList.SetProgressState(hwnd, TaskbarProgressBarState.Error);
            return;
        }

        if (_activity.IsIndeterminate)
        {
            _taskbarList.SetProgressState(hwnd, TaskbarProgressBarState.Indeterminate);
            return;
        }

        _taskbarList.SetProgressState(hwnd, TaskbarProgressBarState.Normal);
        var completed = (ulong)Math.Round(_activity.Progress);
        _taskbarList.SetProgressValue(hwnd, completed, 100);
    }

    private enum TaskbarProgressBarState
    {
        NoProgress = 0,
        Indeterminate = 0x1,
        Normal = 0x2,
        Error = 0x4,
        Paused = 0x8
    }

    [ComImport]
    [Guid("ea1afb91-9e28-4b86-90e9-9e9f8a5eefaf")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface ITaskbarList3
    {
        void HrInit();
        void AddTab(nint hwnd);
        void DeleteTab(nint hwnd);
        void ActivateTab(nint hwnd);
        void SetActiveAlt(nint hwnd);
        void MarkFullscreenWindow(nint hwnd, [MarshalAs(UnmanagedType.Bool)] bool fFullscreen);
        void SetProgressValue(nint hwnd, ulong ullCompleted, ulong ullTotal);
        void SetProgressState(nint hwnd, TaskbarProgressBarState tbpFlags);
    }
}
