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

        // Manual implementation of SelectedFile to handle Unload logic
        private FileViewModel _selectedFile;
        public FileViewModel SelectedFile
        {
            get => _selectedFile;
            set
            {
                var oldValue = _selectedFile;
                if (SetProperty(ref _selectedFile, value))
                {
                    // 1. Unload the previous file to free memory
                    oldValue?.Unload();

                    // 2. Load the new file
                    if (_selectedFile != null)
                    {
                        _ = _selectedFile.EnsureLoadedAsync();
                    }

                    // Notify that Home selection state changed
                    OnPropertyChanged(nameof(IsHomeSelected));
                }
            }
        }

        public bool IsHomeSelected => SelectedFile == null;
        public bool IsInitialized => _fileService != null;

        public ObservableCollection<FileGroupViewModel> FileGroups { get; } = new();
        public event Action<Type> NavigationRequested;

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
        public void CreateZip()
        {
            NavigationRequested?.Invoke(typeof(Views.CreateZipPage));
        }

        [RelayCommand]
        public void CreateModelBin()
        {
            NavigationRequested?.Invoke(typeof(Views.CreateModelBinPage));
        }

        public async Task ProcessPathsAsync(IEnumerable<string> paths)
        {
            IsBusy = true;
            StatusMessage = "Listing files...";

            try
            {
                foreach (var path in paths)
                {
                    var groupName = Path.GetFileName(path);
                    var group = new FileGroupViewModel(groupName);

                    // Add Group to UI IMMEDIATELY
                    FileGroups.Add(group);

                    // Stream file paths into the group one by one
                    // Now using GetFilePathsAsync which only extracts/lists, doesn't parse data
                    await foreach (var filePath in _fileService.GetFilePathsAsync(path))
                    {
                        var fileName = Path.GetFileName(filePath);
                        StatusMessage = $"Found {fileName}...";

                        // Pass the filePath and service, but don't load yet
                        group.Files.Add(new FileViewModel(fileName, filePath, _fileService));
                    }

                    if (group.Files.Count == 0)
                    {
                        FileGroups.Remove(group);
                    }
                }
            }
            catch (Exception ex)
            {
                await ShowErrorDialog($"Error listing files: {ex.Message}");
            }
            finally
            {
                IsBusy = false;
                StatusMessage = "Ready";
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