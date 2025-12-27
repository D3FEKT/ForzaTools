using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Numerics;
using Windows.Storage.Pickers;
using ForzaTools.ForzaAnalyzer.Services;
using ForzaTools.Bundles;
using ForzaTools.Bundles.Blobs;
using Microsoft.UI.Xaml;

namespace ForzaTools.ForzaAnalyzer.ViewModels
{
    public partial class GroupViewModel : ObservableObject
    {
        public string Name { get; set; }
        public string TextureName { get; set; } // Displayed in UI
        public ObjGroup SourceGroup { get; set; }

        [ObservableProperty]
        private bool _isSelected = true;

        private Action _onSelectionChanged;

        public GroupViewModel(ObjGroup group, string texture, Action onSelectionChanged)
        {
            Name = group.Name;
            TextureName = texture ?? "No Texture";
            SourceGroup = group;
            _onSelectionChanged = onSelectionChanged;
        }

        partial void OnIsSelectedChanged(bool value) => _onSelectionChanged?.Invoke();
    }

    public partial class CreateModelBinViewModel : ObservableObject
    {
        private readonly ModelBuilderService _builderService = new ModelBuilderService();
        private readonly ObjParserService _parserService = new ObjParserService();

        private Bundle _activeBundle;
        private ObjSceneData _rawScene;

        [ObservableProperty] private string _statusMessage = "Select an OBJ file to begin.";
        [ObservableProperty] private string _mtlStatusMessage = ""; // Info about loaded .mtl
        [ObservableProperty] private string _inputFilePath;
        [ObservableProperty][NotifyPropertyChangedFor(nameof(IsNotBusy))] private bool _isBusy;
        public bool IsNotBusy => !IsBusy;
        [ObservableProperty] private bool _isFileSelected;

        // --- Live Edit Properties (Memory Only) ---
        [ObservableProperty] private bool _isOpaque = true;
        [ObservableProperty] private bool _isDecal;
        [ObservableProperty] private bool _isTransparent;
        [ObservableProperty] private bool _isShadow = true;
        [ObservableProperty] private bool _isNotShadow;
        [ObservableProperty] private bool _isAlphaToCoverage;
        [ObservableProperty] private bool _isMorphDamage = true;

        // --- Transform Editing ---
        [ObservableProperty] private float _posX;
        [ObservableProperty] private float _posY;
        [ObservableProperty] private float _posZ;
        [ObservableProperty] private float _scaleX = 1f;
        [ObservableProperty] private float _scaleY = 1f;
        [ObservableProperty] private float _scaleZ = 1f;

        // --- Geometry Modifiers ---
        [ObservableProperty] private float _rotationPitch;
        [ObservableProperty] private float _rotationYaw;
        [ObservableProperty] private float _rotationRoll;
        [ObservableProperty] private bool _isMirrorEnabled;
        [ObservableProperty] private bool _flipX_Pos;
        [ObservableProperty] private bool _flipY_Pos;
        [ObservableProperty] private bool _flipZ_Pos;
        [ObservableProperty] private bool _flipX_Norm;
        [ObservableProperty] private bool _flipY_Norm;
        [ObservableProperty] private bool _flipZ_Norm;
        [ObservableProperty] private bool _flipFaces;

        // --- Material & Groups ---
        public ObservableCollection<string> Materials { get; } = new ObservableCollection<string>();
        [ObservableProperty] private string _selectedMaterial;

        public ObservableCollection<GroupViewModel> ModelGroups { get; } = new ObservableCollection<GroupViewModel>();

        public CreateModelBinViewModel()
        {
            LoadMaterials();
        }

        // --- Change Handlers ---
        partial void OnRotationPitchChanged(float value) => RebuildBundle();
        partial void OnRotationYawChanged(float value) => RebuildBundle();
        partial void OnRotationRollChanged(float value) => RebuildBundle();
        partial void OnIsMirrorEnabledChanged(bool value) => RebuildBundle();
        partial void OnFlipX_PosChanged(bool value) => RebuildBundle();
        partial void OnFlipY_PosChanged(bool value) => RebuildBundle();
        partial void OnFlipZ_PosChanged(bool value) => RebuildBundle();
        partial void OnFlipX_NormChanged(bool value) => RebuildBundle();
        partial void OnFlipY_NormChanged(bool value) => RebuildBundle();
        partial void OnFlipZ_NormChanged(bool value) => RebuildBundle();
        partial void OnFlipFacesChanged(bool value) => RebuildBundle();

        partial void OnPosXChanged(float value) => UpdateTransformInMemory();
        partial void OnPosYChanged(float value) => UpdateTransformInMemory();
        partial void OnPosZChanged(float value) => UpdateTransformInMemory();
        partial void OnScaleXChanged(float value) => UpdateTransformInMemory();
        partial void OnScaleYChanged(float value) => UpdateTransformInMemory();
        partial void OnScaleZChanged(float value) => UpdateTransformInMemory();

        partial void OnSelectedMaterialChanged(string value)
        {
            if (_activeBundle != null && !string.IsNullOrEmpty(value))
            {
                _builderService.UpdateMaterialInBundle(_activeBundle, value);
                StatusMessage = $"Updated material to '{value}'.";
            }
        }

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
        }

        private void UpdateTransformInMemory()
        {
            if (_activeBundle == null) return;
            foreach (var mesh in _activeBundle.Blobs.OfType<MeshBlob>())
            {
                mesh.PositionTranslate = new Vector4(PosX, PosY, PosZ, 0);
                mesh.PositionScale = new Vector4(ScaleX, ScaleY, ScaleZ, 1);
            }
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
            StatusMessage = "Parsing OBJ file...";
            MtlStatusMessage = "";

            try
            {
                // 1. Parse OBJ
                _rawScene = await Task.Run(() => _parserService.ParseObj(InputFilePath));

                // 2. Locate and Parse MTL
                Dictionary<string, string> matTextures = new Dictionary<string, string>();
                string mtlPath = null;

                // Try path from OBJ (mtllib)
                if (!string.IsNullOrEmpty(_rawScene.MaterialLib))
                {
                    string dir = Path.GetDirectoryName(InputFilePath);
                    string checkPath = Path.Combine(dir, _rawScene.MaterialLib);
                    if (File.Exists(checkPath)) mtlPath = checkPath;
                }

                // Fallback: Same name as OBJ
                if (mtlPath == null)
                {
                    string sameNameMtl = Path.ChangeExtension(InputFilePath, ".mtl");
                    if (File.Exists(sameNameMtl)) mtlPath = sameNameMtl;
                }

                if (mtlPath != null)
                {
                    matTextures = await Task.Run(() => _parserService.ParseMtl(mtlPath));
                    MtlStatusMessage = $"Loaded MTL: {Path.GetFileName(mtlPath)}";
                }
                else
                {
                    MtlStatusMessage = "No associated MTL file found.";
                }

                // 3. Populate Groups with Texture Info
                ModelGroups.Clear();
                foreach (var g in _rawScene.Groups)
                {
                    if (g.Indices.Count > 0)
                    {
                        string tex = null;
                        if (!string.IsNullOrEmpty(g.MaterialName) && matTextures.ContainsKey(g.MaterialName))
                        {
                            tex = matTextures[g.MaterialName];
                        }
                        ModelGroups.Add(new GroupViewModel(g, tex, () => RebuildBundle()));
                    }
                }

                await RebuildBundleInternalAsync();

                IsFileSelected = true;
                StatusMessage = "Ready.";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error: {ex.Message}";
                IsFileSelected = false;
            }
            finally { IsBusy = false; }
        }

        private void RebuildBundle()
        {
            if (_rawScene == null) return;
            if (IsBusy) return;
            _ = RebuildBundleInternalAsync();
        }

        private async Task RebuildBundleInternalAsync()
        {
            IsBusy = true;
            StatusMessage = "Processing geometry...";

            try
            {
                await Task.Run(() =>
                {
                    var selectedIndices = ModelGroups
                        .Where(g => g.IsSelected)
                        .SelectMany(g => g.SourceGroup.Indices)
                        .ToArray();

                    if (selectedIndices.Length == 0) return;

                    var positions = (Vector3[])_rawScene.Positions.Clone();
                    var normals = (Vector3[])_rawScene.Normals.Clone();

                    ApplyGeometryModifiers(positions, normals, ref selectedIndices);

                    var geometryInput = new GeometryInput
                    {
                        Name = _rawScene.Name,
                        Positions = positions,
                        Normals = normals,
                        UVs = _rawScene.UVs,
                        Indices = selectedIndices
                    };

                    var processed = _builderService.ProcessGeometry(geometryInput);
                    string currentMat = SelectedMaterial ?? "Default";
                    _activeBundle = _builderService.CreateBundleInMemory(processed, currentMat);
                });

                if (_activeBundle != null)
                {
                    var firstMesh = _activeBundle.Blobs.OfType<MeshBlob>().FirstOrDefault();
                    if (firstMesh != null)
                    {
                        _posX = firstMesh.PositionTranslate.X;
                        _posY = firstMesh.PositionTranslate.Y;
                        _posZ = firstMesh.PositionTranslate.Z;
                        _scaleX = firstMesh.PositionScale.X;
                        _scaleY = firstMesh.PositionScale.Y;
                        _scaleZ = firstMesh.PositionScale.Z;

                        OnPropertyChanged(nameof(PosX));
                        OnPropertyChanged(nameof(PosY));
                        OnPropertyChanged(nameof(PosZ));
                        OnPropertyChanged(nameof(ScaleX));
                        OnPropertyChanged(nameof(ScaleY));
                        OnPropertyChanged(nameof(ScaleZ));

                        UpdateMeshFlags();
                    }
                    StatusMessage = "Geometry updated.";
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Rebuild Error: {ex.Message}";
            }
            finally
            {
                IsBusy = false;
            }
        }

        private void ApplyGeometryModifiers(Vector3[] pos, Vector3[] norm, ref int[] indices)
        {
            if (IsMirrorEnabled)
            {
                for (int i = 0; i < pos.Length; i++)
                {
                    pos[i].X *= -1;
                    norm[i].X *= -1;
                }
                for (int i = 0; i < indices.Length; i += 3)
                {
                    int temp = indices[i + 1];
                    indices[i + 1] = indices[i + 2];
                    indices[i + 2] = temp;
                }
            }

            if (RotationPitch != 0 || RotationYaw != 0 || RotationRoll != 0)
            {
                float p = RotationPitch * (MathF.PI / 180f);
                float y = RotationYaw * (MathF.PI / 180f);
                float r = RotationRoll * (MathF.PI / 180f);
                var rotMatrix = Matrix4x4.CreateFromYawPitchRoll(y, p, r);

                for (int i = 0; i < pos.Length; i++)
                {
                    pos[i] = Vector3.Transform(pos[i], rotMatrix);
                    norm[i] = Vector3.TransformNormal(norm[i], rotMatrix);
                }
            }

            bool fx = FlipX_Pos, fy = FlipY_Pos, fz = FlipZ_Pos;
            bool nx = FlipX_Norm, ny = FlipY_Norm, nz = FlipZ_Norm;

            if (fx || fy || fz || nx || ny || nz)
            {
                for (int i = 0; i < pos.Length; i++)
                {
                    if (fx) pos[i].X *= -1;
                    if (fy) pos[i].Y *= -1;
                    if (fz) pos[i].Z *= -1;

                    if (nx) norm[i].X *= -1;
                    if (ny) norm[i].Y *= -1;
                    if (nz) norm[i].Z *= -1;
                }
            }

            if (FlipFaces)
            {
                for (int i = 0; i < indices.Length; i += 3)
                {
                    int temp = indices[i + 1];
                    indices[i + 1] = indices[i + 2];
                    indices[i + 2] = temp;
                }
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
                UpdateTransformInMemory();
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