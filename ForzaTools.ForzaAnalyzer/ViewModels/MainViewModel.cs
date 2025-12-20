using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ForzaTools.ForzaAnalyzer.Services;
using Microsoft.UI.Xaml.Controls;

namespace ForzaTools.ForzaAnalyzer.ViewModels
{
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

        public ObservableCollection<FileViewModel> Files { get; } = new();

        public void Initialize(nint windowHandle)
        {
            _windowHandle = windowHandle;
            _fileService = new FileService(windowHandle);
        }

        [RelayCommand]
        public async Task OpenFilesAsync()
        {
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
                    foreach (var item in results)
                    {
                        Files.Add(new FileViewModel(item.FileName, item.ParsedData));
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