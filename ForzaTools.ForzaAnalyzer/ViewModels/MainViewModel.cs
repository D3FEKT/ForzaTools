using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ForzaTools.ForzaAnalyzer.Services;
using Microsoft.UI.Xaml.Controls;

namespace ForzaTools.ForzaAnalyzer.ViewModels
{
    // New class to hold groups of files
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

        // CHANGED: Now a collection of Groups instead of flat files
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

        public async Task ProcessPathsAsync(IEnumerable<string> paths)
        {
            IsBusy = true;
            StatusMessage = "Parsing files...";

            try
            {
                foreach (var path in paths)
                {
                    var results = await _fileService.ProcessFileAsync(path);

                    // Create a new Group for this file/zip
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
                Title = "Error",
                Content = message,
                CloseButtonText = "OK"
            };
            await dialog.ShowAsync();
        }
    }
}