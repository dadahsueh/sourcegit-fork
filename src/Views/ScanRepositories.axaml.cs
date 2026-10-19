using System;

using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Threading;

namespace SourceGit.Views
{
    public partial class ScanRepositories : UserControl
    {
        public ScanRepositories()
        {
            InitializeComponent();
        }

        private async void OnAddDirectory(object _, RoutedEventArgs e)
        {
            var provider = TopLevel.GetTopLevel(this)?.StorageProvider;
            if (provider == null)
                return;

            if (DataContext is not ViewModels.ScanRepositories vm)
                return;

            var options = new FolderPickerOpenOptions() { AllowMultiple = true };
            try
            {
                var selected = await provider.OpenFolderPickerAsync(options);
                foreach (var folder in selected)
                {
                    var folderPath = folder is { Path: { IsAbsoluteUri: true } path } ? path.LocalPath : folder?.Path.ToString();
                    vm.AddDirectory(folderPath);
                }
            }
            catch (Exception ex)
            {
                Models.Notification.Send(null, $"Failed to select root scanning directory: {ex.Message}", true);
            }

            e.Handled = true;
        }

        private void OnRemoveDirectory(object sender, RoutedEventArgs e)
        {
            if (DataContext is ViewModels.ScanRepositories vm && sender is Button { Tag: Models.ScanDir dir })
                vm.RemoveDirectory(dir);

            e.Handled = true;
        }

        private void OnToggleRecursive(object _, RoutedEventArgs e)
        {
            if (DataContext is ViewModels.ScanRepositories vm)
            {
                // The value of the checkbox is applied to the model after the click has been handled,
                // so the configuration is persisted once the current event is processed.
                Dispatcher.UIThread.Post(() => vm.SaveConfig());
            }

            e.Handled = true;
        }

        private async void OnRescan(object _, RoutedEventArgs e)
        {
            if (DataContext is ViewModels.ScanRepositories vm)
                await vm.ScanAsync();

            e.Handled = true;
        }
    }
}
