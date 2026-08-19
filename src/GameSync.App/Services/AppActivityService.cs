using CommunityToolkit.Mvvm.ComponentModel;
using GameSync.Core.Models;
using WinRT;

namespace GameSync.App.Services;

/// <summary>
/// Global long-running operation state shown in the main window banner and Windows taskbar progress.
/// </summary>
[GeneratedBindableCustomProperty]
public sealed partial class AppActivityService : ObservableObject
{
    private AppActivityKind _currentKind = AppActivityKind.Library;

    [ObservableProperty]
    public partial bool IsActive { get; private set; }

    [ObservableProperty]
    public partial string Title { get; private set; } = string.Empty;

    [ObservableProperty]
    public partial string Message { get; private set; } = string.Empty;

    [ObservableProperty]
    public partial double Progress { get; private set; }

    [ObservableProperty]
    public partial bool IsIndeterminate { get; private set; }

    [ObservableProperty]
    public partial bool IsError { get; private set; }

    public AppActivityKind CurrentKind => _currentKind;

    public ActivityHandle Begin(
        AppActivityKind kind,
        string title,
        string message,
        double progress = 0,
        bool isIndeterminate = false,
        bool isError = false)
    {
        if (IsActive && (int)kind < (int)_currentKind)
        {
            return new ActivityHandle(this, kind, ownsState: false);
        }

        Apply(kind, title, message, progress, isIndeterminate, isError);
        return new ActivityHandle(this, kind, ownsState: true);
    }

    internal void Update(
        AppActivityKind kind,
        string message,
        double progress,
        bool isIndeterminate = false,
        bool isError = false)
    {
        if (!IsActive || _currentKind != kind)
        {
            return;
        }

        Message = message;
        Progress = Math.Clamp(progress, 0, 100);
        IsIndeterminate = isIndeterminate;
        IsError = isError;
    }

    internal void UpdateTitle(AppActivityKind kind, string title)
    {
        if (!IsActive || _currentKind != kind)
        {
            return;
        }

        Title = title;
    }

    internal void End(AppActivityKind kind)
    {
        if (!IsActive || _currentKind != kind)
        {
            return;
        }

        IsActive = false;
        IsError = false;
        Title = string.Empty;
        Message = string.Empty;
        Progress = 0;
        IsIndeterminate = false;
    }

    private void Apply(
        AppActivityKind kind,
        string title,
        string message,
        double progress,
        bool isIndeterminate,
        bool isError)
    {
        _currentKind = kind;
        IsActive = true;
        Title = title;
        Message = message;
        Progress = Math.Clamp(progress, 0, 100);
        IsIndeterminate = isIndeterminate;
        IsError = isError;
    }

    public sealed class ActivityHandle : IDisposable
    {
        private readonly AppActivityService _service;
        private readonly AppActivityKind _kind;
        private readonly bool _ownsState;
        private bool _disposed;

        internal ActivityHandle(AppActivityService service, AppActivityKind kind, bool ownsState)
        {
            _service = service;
            _kind = kind;
            _ownsState = ownsState;
        }

        public void Report(string message, double progress, bool isIndeterminate = false, bool isError = false) =>
            _service.Update(_kind, message, progress, isIndeterminate, isError);

        public void SetTitle(string title) => _service.UpdateTitle(_kind, title);

        public void Dispose()
        {
            if (_disposed || !_ownsState)
            {
                return;
            }

            _disposed = true;
            _service.End(_kind);
        }
    }
}
