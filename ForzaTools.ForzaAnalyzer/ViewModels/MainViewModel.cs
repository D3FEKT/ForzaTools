using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ForzaTools.ForzaAnalyzer.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading.Tasks;

namespace ForzaTools.ForzaAnalyzer.ViewModels
{
    public class FileGroupViewModel
    {
        public string GroupName { get; set; }
        public ObservableCollection<FileViewModel> Files { get; } = new();
        public FileGroupViewModel(string name) { GroupName = name; }
    }

    public partial class MainViewModel : ObservableObject
    {
        private FileService _fileService;
        private nint _windowHandle;

        [ObservableProperty]
        private bool _isBusy;

        [ObservableProperty]
        private string _statusMessage;

        [ObservableProperty]
        private FileViewModel _selectedFile;

        public bool IsHomeSelected => SelectedFile == null;
        public bool IsInitialized => _fileService != null;

        public ObservableCollection<FileGroupViewModel> FileGroups { get; } = new();

        public void Initialize(nint windowHandle)
        {
            _windowHandle = windowHandle;
            _fileService = new FileService(windowHandle);
        }

        [RelayCommand]
        public async Task OpenFilesAsync()
        {
            if (_fileService == null)
            {
                await ShowErrorDialog("Service not initialized. Please restart the app.");
                return;
            }

            try
            {
                var paths = await _fileService.PickFilesAsync();
                if (paths.Count > 0)
                {
                    await ProcessPathsAsync(paths);
                }
            }
            catch (Exception ex)
            {
                await ShowErrorDialog(ex.Message);
            }
        }

        [RelayCommand]
        public void CreateZip() // Changed from Async to void for simple Navigation
        {
            // Assuming App.MainWindow has a Frame we can access via a helper or ShellPage
            // For now, we assume the ShellPage handles navigation if we expose a request
            // OR we can pass the Frame to the ViewModel.

            // Typical WinUI 3 Navigation pattern from ShellPage:
            var frame = (App.MainWindow.Content as FrameworkElement)?.FindName("ContentFrame") as Frame;
            // Fallback: If your ShellPage structure differs, you might need an event.
            // But based on your ShellPage.xaml.cs, you have a 'ContentFrame'.

            // BETTER APPROACH: Add a navigation request event or use the ShellPage instance if static.
            // Since I cannot change ShellPage instance logic easily here, 
            // I will use a direct navigation hack or assume you wire it up in the View.

            // Let's assume we can trigger navigation via the ShellPage which usually holds the MainViewModel.
            // Actually, usually MainViewModel shouldn't know about Views.

            // TEMPORARY FIX: We will trigger a property or event that ShellPage listens to?
            // No, simplest way for this snippet:
            // Since ShellPage has `ViewModel.OpenFilesCommand`, we can just tell ShellPage to Navigate.
            // But the Command is in ViewModel. 

            // I will add a static event to MainViewModel that ShellPage subscribes to.
            NavigationRequested?.Invoke(typeof(Views.CreateZipPage));
        }

        // Add this to MainViewModel class
        public event Action<Type> NavigationRequested;

        [RelayCommand]
        public async Task CreateModelBinAsync()
        {
            await ShowErrorDialog("Create ModelBin functionality coming soon.");
        }

        public async Task ProcessPathsAsync(IEnumerable<string> paths)
        {
            IsBusy = true;
            StatusMessage = "Parsing files...";

            try
            {
                foreach (var path in paths)
                {
                    var results = await _fileService.ProcessFileAsync(path);

                    var groupName = Path.GetFileName(path);
                    var group = new FileGroupViewModel(groupName);

                    foreach (var item in results)
                    {
                        group.Files.Add(new FileViewModel(item.FileName, item.ParsedData));
                    }

                    if (group.Files.Count > 0)
                    {
                        FileGroups.Add(group);
                    }
                }
            }
            catch (Exception ex)
            {
                await ShowErrorDialog($"Error parsing file: {ex.Message}");
            }
            finally
            {
                IsBusy = false;
                StatusMessage = string.Empty;
            }
        }

        private async Task ShowErrorDialog(string message)
        {
            if (App.MainWindow?.Content?.XamlRoot == null) return;

            var dialog = new ContentDialog
            {
                XamlRoot = App.MainWindow.Content.XamlRoot,
                Title = "Notification",
                Content = message,
                CloseButtonText = "OK"
            };
            await dialog.ShowAsync();
        }
    }
}