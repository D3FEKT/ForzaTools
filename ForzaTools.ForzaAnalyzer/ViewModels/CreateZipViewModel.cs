using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.Threading.Tasks;
using Windows.Storage.Pickers;
using ForzaTools.ForzaAnalyzer.Services;
using System.IO;

namespace ForzaTools.ForzaAnalyzer.ViewModels
{
    public class ZipItem
    {
        public string Name { get; set; }
        public string Type { get; set; } // "File" or "Folder"
        public string FullPath { get; set; }
        public string Icon { get; set; } // Glyph
    }

    public partial class CreateZipViewModel : ObservableObject
    {
        private ZipCreationService _zipService = new ZipCreationService();

        [ObservableProperty]
        private string _zipName = "NewArchive";

        [ObservableProperty]
        private string _statusMessage;

        [ObservableProperty]
        private int _selectedFormatIndex = 0; // 0 = Standard, 1 = Forza

        public ObservableCollection<ZipItem> Items { get; } = new();

        public List<string> Formats { get; } = new() { "Standard Zip (Deflate)", "Forza Zip (Store)" };

        [RelayCommand]
        public async Task AddFilesAsync()
        {
            var picker = new FileOpenPicker();
            var window = App.MainWindow;
            var hWnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
            WinRT.Interop.InitializeWithWindow.Initialize(picker, hWnd);

            picker.ViewMode = PickerViewMode.List;
            picker.FileTypeFilter.Add("*");

            var files = await picker.PickMultipleFilesAsync();
            foreach (var file in files)
            {
                Items.Add(new ZipItem
                {
                    Name = file.Name,
                    Type = "File",
                    FullPath = file.Path,
                    Icon = "\uE8A5" // Document Icon
                });
            }
        }

        [RelayCommand]
        public async Task AddFolderAsync()
        {
            var picker = new FolderPicker();
            var window = App.MainWindow;
            var hWnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
            WinRT.Interop.InitializeWithWindow.Initialize(picker, hWnd);
            picker.FileTypeFilter.Add("*");

            var folder = await picker.PickSingleFolderAsync();
            if (folder != null)
            {
                Items.Add(new ZipItem
                {
                    Name = folder.Name,
                    Type = "Folder",
                    FullPath = folder.Path,
                    Icon = "\uE8B7" // Folder Icon
                });
            }
        }

        [RelayCommand]
        public async Task CreateZipAsync()
        {
            if (Items.Count == 0)
            {
                StatusMessage = "No files selected.";
                return;
            }

            var picker = new FileSavePicker();
            var window = App.MainWindow;
            var hWnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
            WinRT.Interop.InitializeWithWindow.Initialize(picker, hWnd);

            picker.SuggestedStartLocation = PickerLocationId.ComputerFolder;
            picker.SuggestedFileName = ZipName;

            if (SelectedFormatIndex == 0)
                picker.FileTypeChoices.Add("Zip Archive", new List<string>() { ".zip" });
            else
                picker.FileTypeChoices.Add("Forza Archive", new List<string>() { ".zip", ".minizip" });

            var file = await picker.PickSaveFileAsync();
            if (file != null)
            {
                StatusMessage = "Creating Zip...";
                try
                {
                    var fileList = new List<string>();
                    var folderList = new List<string>();

                    foreach (var item in Items)
                    {
                        if (item.Type == "File") fileList.Add(item.FullPath);
                        else folderList.Add(item.FullPath);
                    }

                    if (SelectedFormatIndex == 0)
                    {
                        await _zipService.CreateStandardZipAsync(file.Path, fileList, folderList);
                    }
                    else
                    {
                        await _zipService.CreateForzaZipAsync(file.Path, fileList, folderList);
                    }

                    StatusMessage = "Zip Created Successfully!";
                }
                catch (Exception ex)
                {
                    StatusMessage = $"Error: {ex.Message}";
                }
            }
        }

        [RelayCommand]
        public void ClearList()
        {
            Items.Clear();
            StatusMessage = "";
        }
    }
}