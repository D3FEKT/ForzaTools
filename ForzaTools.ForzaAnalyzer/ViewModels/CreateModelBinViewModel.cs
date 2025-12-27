using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Windows.Storage.Pickers;
using ForzaTools.ForzaAnalyzer.Services;
using ForzaTools.Bundles;
using ForzaTools.Bundles.Blobs;
using Microsoft.UI.Xaml;

namespace ForzaTools.ForzaAnalyzer.ViewModels
{
    public partial class CreateModelBinViewModel : ObservableObject
    {
        private readonly ModelBuilderService _builderService = new ModelBuilderService();
        private readonly ObjParserService _parserService = new ObjParserService();

        // The in-memory bundle we are editing
        private Bundle _activeBundle;

        // Changed type from GeometryInput to ProcessedGeometry
        private ProcessedGeometry _cachedGeometry;

        [ObservableProperty]
        private string _statusMessage = "Select an OBJ file to begin.";

        [ObservableProperty]
        private string _inputFilePath;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsNotBusy))]
        private bool _isBusy;

        public bool IsNotBusy => !IsBusy;

        [ObservableProperty]
        private bool _isFileSelected;

        // --- Live Edit Properties ---
        [ObservableProperty] private bool _isOpaque = true;
        [ObservableProperty] private bool _isDecal;
        [ObservableProperty] private bool _isTransparent;
        [ObservableProperty] private bool _isShadow = true;
        [ObservableProperty] private bool _isNotShadow;
        [ObservableProperty] private bool _isAlphaToCoverage;
        [ObservableProperty] private bool _isMorphDamage = true;

        // --- Material Selection ---
        public ObservableCollection<string> Materials { get; } = new ObservableCollection<string>();

        [ObservableProperty]
        private string _selectedMaterial;

        public CreateModelBinViewModel()
        {
            LoadMaterials();
        }

        partial void OnSelectedMaterialChanged(string value)
        {
            if (_activeBundle != null && !string.IsNullOrEmpty(value))
            {
                _builderService.UpdateMaterialInBundle(_activeBundle, value);
                StatusMessage = $"Updated material to '{value}'.";
            }
        }

        // Property changed handlers for Live Edit flags
        partial void OnIsOpaqueChanged(bool value) => UpdateMeshFlags();
        partial void OnIsDecalChanged(bool value) => UpdateMeshFlags();
        partial void OnIsTransparentChanged(bool value) => UpdateMeshFlags();
        partial void OnIsShadowChanged(bool value) => UpdateMeshFlags();
        partial void OnIsNotShadowChanged(bool value) => UpdateMeshFlags();
        partial void OnIsAlphaToCoverageChanged(bool value) => UpdateMeshFlags();
        partial void OnIsMorphDamageChanged(bool value) => UpdateMeshFlags();

        private void UpdateMeshFlags()
        {
            if (_activeBundle == null) return;

            foreach (var mesh in _activeBundle.Blobs.OfType<MeshBlob>())
            {
                mesh.IsOpaque = IsOpaque;
                mesh.IsDecal = IsDecal;
                mesh.IsTransparent = IsTransparent;
                mesh.IsShadow = IsShadow;
                mesh.IsNotShadow = IsNotShadow;
                mesh.IsAlphaToCoverage = IsAlphaToCoverage;
                mesh.IsMorphDamage = IsMorphDamage;
            }
            StatusMessage = "Updated mesh settings in memory.";
        }

        private void LoadMaterials()
        {
            Materials.Clear();
            var names = MaterialLibrary.GetMaterialNames();
            if (names.Count > 0)
            {
                foreach (var name in names) Materials.Add(name);
                SelectedMaterial = Materials.First();
            }
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
                await InitializeConversionAsync();
            }
        }

        private async Task InitializeConversionAsync()
        {
            IsBusy = true;
            StatusMessage = "Parsing OBJ file and creating base Bundle...";

            try
            {
                await Task.Run(() =>
                {
                    // 1. Parse Geometry
                    var geometryRaw = _parserService.ParseObj(InputFilePath);

                    // 2. Process it (GeometryInput -> ProcessedGeometry)
                    _cachedGeometry = _builderService.ProcessGeometry(geometryRaw);

                    // 3. Create Base Bundle in Memory
                    string currentMat = SelectedMaterial ?? "Default";
                    _activeBundle = _builderService.CreateBundleInMemory(_cachedGeometry, currentMat);
                });

                // 4. Sync UI to Bundle Defaults
                var firstMesh = _activeBundle.Blobs.OfType<MeshBlob>().FirstOrDefault();
                if (firstMesh != null)
                {
                    _isOpaque = firstMesh.IsOpaque;
                    _isDecal = firstMesh.IsDecal;
                    _isTransparent = firstMesh.IsTransparent;
                    _isShadow = firstMesh.IsShadow;
                    _isNotShadow = firstMesh.IsNotShadow;
                    _isAlphaToCoverage = firstMesh.IsAlphaToCoverage;
                    _isMorphDamage = firstMesh.IsMorphDamage;

                    // Trigger UI updates for properties
                    OnPropertyChanged(nameof(IsOpaque));
                    OnPropertyChanged(nameof(IsDecal));
                    OnPropertyChanged(nameof(IsTransparent));
                    OnPropertyChanged(nameof(IsShadow));
                    OnPropertyChanged(nameof(IsNotShadow));
                    OnPropertyChanged(nameof(IsAlphaToCoverage));
                    OnPropertyChanged(nameof(IsMorphDamage));
                }

                IsFileSelected = true;
                StatusMessage = "Ready. Adjust settings and Save.";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error parsing file: {ex.Message}";
                IsFileSelected = false;
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        public async Task ConvertModelAsync()
        {
            if (_activeBundle == null)
            {
                StatusMessage = "No model loaded.";
                return;
            }

            var savePicker = new FileSavePicker();
            var window = App.MainWindow;
            var hWnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
            WinRT.Interop.InitializeWithWindow.Initialize(savePicker, hWnd);

            savePicker.SuggestedStartLocation = PickerLocationId.Desktop;
            savePicker.SuggestedFileName = Path.GetFileNameWithoutExtension(InputFilePath) + ".modelbin";
            savePicker.FileTypeChoices.Add("Forza ModelBin", new[] { ".modelbin" });

            var outputFile = await savePicker.PickSaveFileAsync();
            if (outputFile == null) return;

            IsBusy = true;
            StatusMessage = "Saving...";

            try
            {
                await Task.Run(() =>
                {
                    _builderService.SaveBundle(_activeBundle, outputFile.Path);
                });

                StatusMessage = $"Success! Saved to {outputFile.Name}";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error saving: {ex.Message}";
            }
            finally
            {
                IsBusy = false;
            }
        }
    }
}