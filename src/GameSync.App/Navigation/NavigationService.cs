using Microsoft.UI.Xaml.Controls;

namespace GameSync.App.Navigation;

public interface INavigationService
{
    void Initialize(Frame frame);

    void NavigateTo(Type pageType, object? parameter = null);

    bool CanGoBack { get; }

    void GoBack();
}

public sealed class NavigationService : INavigationService
{
    private Frame? _frame;

    public void Initialize(Frame frame)
    {
        _frame = frame ?? throw new ArgumentNullException(nameof(frame));
    }

    public void NavigateTo(Type pageType, object? parameter = null)
    {
        if (_frame is null)
        {
            throw new InvalidOperationException("Navigation frame has not been initialized.");
        }

        _frame.Navigate(pageType, parameter);
    }

    public bool CanGoBack => _frame?.CanGoBack == true;

    public void GoBack()
    {
        if (_frame?.CanGoBack == true)
        {
            _frame.GoBack();
        }
    }
}
