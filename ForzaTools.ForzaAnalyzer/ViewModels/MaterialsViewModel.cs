using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Windows.Storage.Pickers;
using ForzaTools.ForzaAnalyzer.Services;

namespace ForzaTools.ForzaAnalyzer.ViewModels
{
    public partial class MaterialsViewModel : ObservableObject
    {
        private readonly MaterialExtractionService _extractionService = new();

        [ObservableProperty]
        private string _statusMessage;

        [ObservableProperty]
        private bool _isBusy;

        public ObservableCollection<MaterialDisplayItem> Materials { get; } = new();

        public MaterialsViewModel()
        {
            LoadExistingMaterials();
        }

        private void LoadExistingMaterials()
        {
            try
            {
                var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Materials", "materials.json");
                if (File.Exists(path))
                {
                    string json = File.ReadAllText(path);

                    // FIXED: Reference the context directly (no longer nested)
                    var data = JsonSerializer.Deserialize(
                        json,
                        MaterialJsonContext.Default.DictionaryStringMaterialEntry
                    );

                    Materials.Clear();
                    if (data != null)
                    {
                        foreach (var kvp in data)
                        {
                            Materials.Add(new MaterialDisplayItem { Name = kvp.Key, DataSize = kvp.Value.MaterialBlob.Length / 3 + " bytes" });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Load error: {ex.Message}");
            }
        }

        [RelayCommand]
        public async Task ExtractMaterialsAsync()
        {
            var picker = new FileOpenPicker();
            var window = App.MainWindow;
            var hWnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
            WinRT.Interop.InitializeWithWindow.Initialize(picker, hWnd);

            picker.ViewMode = PickerViewMode.List;
            picker.FileTypeFilter.Add(".modelbin");
            picker.FileTypeFilter.Add(".zip");

            var files = await picker.PickMultipleFilesAsync();
            if (files.Count > 0)
            {
                IsBusy = true;
                StatusMessage = "Extracting Materials...";

                var filePaths = new System.Collections.Generic.List<string>();
                foreach (var f in files) filePaths.Add(f.Path);

                int count = await _extractionService.ExtractMaterialsAsync(filePaths);

                StatusMessage = $"Extracted {count} materials.";
                LoadExistingMaterials();
                IsBusy = false;
            }
        }
    }

    public class MaterialDisplayItem
    {
        public string Name { get; set; }
        public string DataSize { get; set; }
    }
}