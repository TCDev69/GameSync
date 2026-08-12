using GameSync.Core.Models;
using GameSync.Core.Services;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace GameSync.App.Converters;

public sealed class GameLibraryStatusToTextConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language) =>
        value is GameLibraryStatus status
            ? GameLibraryStatusMapper.ToDisplayText(status)
            : GameLibraryStatusMapper.ToDisplayText(GameLibraryStatus.Unknown);

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotSupportedException();
}

public sealed class GameLibraryStatusToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        var status = value is GameLibraryStatus s ? s : GameLibraryStatus.Unknown;
        var color = status switch
        {
            GameLibraryStatus.Synced => Color.FromArgb(255, 16, 124, 16),
            GameLibraryStatus.LocalChanges => Color.FromArgb(255, 0, 120, 212),
            GameLibraryStatus.RemoteChanges => Color.FromArgb(255, 136, 23, 152),
            GameLibraryStatus.Conflict => Color.FromArgb(255, 196, 43, 28),
            GameLibraryStatus.Running => Color.FromArgb(255, 0, 153, 188),
            GameLibraryStatus.Syncing => Color.FromArgb(255, 0, 120, 212),
            GameLibraryStatus.Error => Color.FromArgb(255, 196, 43, 28),
            GameLibraryStatus.NotConfigured => Color.FromArgb(255, 157, 93, 0),
            _ => Color.FromArgb(255, 96, 94, 92)
        };
        return new SolidColorBrush(color);
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotSupportedException();
}
