using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Windows.Storage.Pickers;
using ForzaTools.ForzaAnalyzer.Services;
using Microsoft.UI.Xaml;

namespace ForzaTools.ForzaAnalyzer.ViewModels
{
    public partial class CreateModelBinViewModel : ObservableObject
    {
        private readonly ModelBuilderService _builderService = new ModelBuilderService();
        private readonly ObjParserService _parserService = new ObjParserService();

        [ObservableProperty]
        private string _statusMessage = "Ready to convert.";

        [ObservableProperty]
        private string _inputFilePath;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsNotBusy))]
        private bool _isBusy;

        public bool IsNotBusy => !IsBusy;

        // --- Material Selection ---
        public ObservableCollection<string> Materials { get; } = new ObservableCollection<string>();

        [ObservableProperty]
        private string _selectedMaterial;

        public CreateModelBinViewModel()
        {
            LoadMaterials();
        }

        private void LoadMaterials()
        {
            Materials.Clear();
            var names = MaterialLibrary.GetMaterialNames();

            if (names.Count == 0)
            {
                StatusMessage = "No materials found in Materials/materials.json.";
                return;
            }

            foreach (var name in names)
            {
                Materials.Add(name);
            }

            // Select the first one by default to avoid null errors
            SelectedMaterial = Materials.First();
        }

        [RelayCommand]
        public async Task PickInputFileAsync()
        {
            var picker = new FileOpenPicker();
            var window = App.MainWindow;
            var hWnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
            WinRT.Interop.InitializeWithWindow.Initialize(picker, hWnd);

            picker.ViewMode = PickerViewMode.List;
            picker.SuggestedStartLocation = PickerLocationId.Desktop;
            picker.FileTypeFilter.Add(".obj");

            var file = await picker.PickSingleFileAsync();
            if (file != null)
            {
                InputFilePath = file.Path;
                StatusMessage = $"Selected: {file.Name}. Ready to convert.";
            }
        }

        [RelayCommand]
        public async Task ConvertModelAsync()
        {
            if (string.IsNullOrEmpty(InputFilePath) || !File.Exists(InputFilePath))
            {
                StatusMessage = "Please select a valid .obj file first.";
                return;
            }

            if (string.IsNullOrEmpty(SelectedMaterial))
            {
                StatusMessage = "Please select a material from the list.";
                return;
            }

            // Capture data on UI thread
            string currentMaterial = SelectedMaterial;

            var savePicker = new FileSavePicker();
            var window = App.MainWindow;
            var hWnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
            WinRT.Interop.InitializeWithWindow.Initialize(savePicker, hWnd);

            savePicker.SuggestedStartLocation = PickerLocationId.Desktop;
            savePicker.SuggestedFileName = Path.GetFileNameWithoutExtension(InputFilePath) + ".modelbin";
            savePicker.FileTypeChoices.Add("Forza ModelBin", new[] { ".modelbin" });

            var outputFile = await savePicker.PickSaveFileAsync();
            if (outputFile == null)
            {
                StatusMessage = "Operation cancelled.";
                return;
            }

            IsBusy = true;
            StatusMessage = "Parsing OBJ file...";

            try
            {
                await Task.Run(() =>
                {
                    var geometry = _parserService.ParseObj(InputFilePath);

                    App.MainWindow.DispatcherQueue.TryEnqueue(() =>
                        StatusMessage = $"Building ModelBin with '{currentMaterial}'...");

                    _builderService.BuildCompatibleModelBin(outputFile.Path, geometry, currentMaterial);
                });

                StatusMessage = $"Success! Saved to {outputFile.Name}";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error: {ex.Message}";
            }
            finally
            {
                IsBusy = false;
            }
        }
    }
}