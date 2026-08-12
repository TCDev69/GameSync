using GameSync.App.Services;
using GameSync.App.ViewModels;
using GameSync.Core.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using Windows.Storage.Pickers;

namespace GameSync.App.Views;

public sealed partial class AddGamePage : Page
{
    public AddGameViewModel ViewModel { get; }

    public AddGamePage()
    {
        ViewModel = App.Services.GetRequiredService<AddGameViewModel>();
        InitializeComponent();
    }

    private async void SearchResults_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is GameSearchResult result)
        {
            await ViewModel.SelectSearchResultCommand.ExecuteAsync(result);
        }
    }

    private async void BrowseExecutable_Click(object sender, RoutedEventArgs e)
    {
        var picker = new FileOpenPicker();
        PickerInterop.Attach(picker);
        picker.FileTypeFilter.Add(".exe");
        picker.FileTypeFilter.Add(".lnk");
        picker.SuggestedStartLocation = PickerLocationId.ComputerFolder;
        var file = await picker.PickSingleFileAsync();
        if (file is not null)
        {
            ViewModel.ApplyLaunchPath(file.Path);
        }
    }

    private void ExecutableDropBorder_DragOver(object sender, DragEventArgs e)
    {
        e.AcceptedOperation = DataPackageOperation.Copy;
        e.DragUIOverride.Caption = "Set launch target";
    }

    private async void ExecutableDropBorder_Drop(object sender, DragEventArgs e)
    {
        if (!e.DataView.Contains(StandardDataFormats.StorageItems))
        {
            return;
        }

        var items = await e.DataView.GetStorageItemsAsync();
        if (items.Count > 0 && items[0] is StorageFile file)
        {
            ViewModel.ApplyLaunchPath(file.Path);
        }
    }

    private async void BrowseSaveFolder_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: SaveLocationEditItem item })
        {
            return;
        }

        var picker = new FolderPicker();
        PickerInterop.Attach(picker);
        picker.FileTypeFilter.Add("*");
        picker.SuggestedStartLocation = PickerLocationId.ComputerFolder;
        var folder = await picker.PickSingleFolderAsync();
        if (folder is not null)
        {
            var paths = App.Services.GetRequiredService<GameSync.Core.Abstractions.IPathResolver>();
            item.LocalPath = paths.ToPortableTemplate(folder.Path);
            item.IsUserEdited = true;
        }
    }

    private void RemoveSaveLocation_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: SaveLocationEditItem item })
        {
            ViewModel.RemoveSaveLocationCommand.Execute(item);
        }
    }
}
