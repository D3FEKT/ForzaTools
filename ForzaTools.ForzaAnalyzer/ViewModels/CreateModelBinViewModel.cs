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
        public string TextureName { get; set; }
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
        [ObservableProperty] private string _mtlStatusMessage = "";
        [ObservableProperty] private string _inputFilePath;
        [ObservableProperty][NotifyPropertyChangedFor(nameof(IsNotBusy))] private bool _isBusy;
        public bool IsNotBusy => !IsBusy;
        [ObservableProperty] private bool _isFileSelected;

        // --- Axis Selection (Import Settings) ---
        public ObservableCollection<string> AxisOptions { get; } =
        [
            "+X", "-X", "+Y", "-Y", "+Z", "-Z"
        ];

        [ObservableProperty] private string _selectedForwardAxis = "-Z";
        [ObservableProperty] private string _selectedUpAxis = "+Y";

        // --- Live Edit Properties ---
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

        // --- Rotation ---
        [ObservableProperty] private float _rotationPitch;
        [ObservableProperty] private float _rotationYaw;
        [ObservableProperty] private float _rotationRoll;

        // --- Vertex Flip Options ---
        [ObservableProperty] private bool _flipVertexX;
        [ObservableProperty] private bool _flipVertexY;
        [ObservableProperty] private bool _flipVertexZ;

        // --- Normal Flip Options ---
        [ObservableProperty] private bool _flipNormalX;
        [ObservableProperty] private bool _flipNormalY;
        [ObservableProperty] private bool _flipNormalZ;

        // --- Face Winding ---
        [ObservableProperty] private bool _flipFaces;

        // --- Mirror ---
        [ObservableProperty] private bool _isMirrorEnabled;

        // --- Recalculate Normals ---
        [ObservableProperty] private bool _recalculateNormals;

        // --- Material & Groups ---
        public ObservableCollection<string> Materials { get; } = new ObservableCollection<string>();
        [ObservableProperty] private string _selectedMaterial;

        public ObservableCollection<GroupViewModel> ModelGroups { get; } = new ObservableCollection<GroupViewModel>();

        [ObservableProperty]
        private string _selectAllButtonText = "Deselect All";

        public CreateModelBinViewModel()
        {
            LoadMaterials();
        }

        // --- Axis Conversion Helper ---
        private CoordinateAxis ParseAxisString(string axis) => axis switch
        {
            "+X" => CoordinateAxis.PositiveX,
            "-X" => CoordinateAxis.NegativeX,
            "+Y" => CoordinateAxis.PositiveY,
            "-Y" => CoordinateAxis.NegativeY,
            "+Z" => CoordinateAxis.PositiveZ,
            "-Z" => CoordinateAxis.NegativeZ,
            _ => CoordinateAxis.PositiveY
        };

        // --- Change Handlers ---
        partial void OnFlipVertexXChanged(bool value) => RebuildBundle();
        partial void OnFlipVertexYChanged(bool value) => RebuildBundle();
        partial void OnFlipVertexZChanged(bool value) => RebuildBundle();
        partial void OnFlipNormalXChanged(bool value) => RebuildBundle();
        partial void OnFlipNormalYChanged(bool value) => RebuildBundle();
        partial void OnFlipNormalZChanged(bool value) => RebuildBundle();
        partial void OnFlipFacesChanged(bool value) => RebuildBundle();
        partial void OnIsMirrorEnabledChanged(bool value) => RebuildBundle();
        partial void OnRecalculateNormalsChanged(bool value) => RebuildBundle();

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

        private void UpdateSelectAllButtonText()
        {
            bool allSelected = ModelGroups.Count > 0 && ModelGroups.All(g => g.IsSelected);
            SelectAllButtonText = allSelected ? "Deselect All" : "Select All";
        }

        [RelayCommand]
        private void ToggleSelectAllGroups()
        {
            if (ModelGroups.Count == 0) return;

            bool allSelected = ModelGroups.All(g => g.IsSelected);
            bool newState = !allSelected;

            foreach (var group in ModelGroups)
            {
                group.IsSelected = newState;
            }

            UpdateSelectAllButtonText();
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
                var importSettings = new ObjImportSettings
                {
                    ForwardAxis = ParseAxisString(SelectedForwardAxis),
                    UpAxis = ParseAxisString(SelectedUpAxis)
                };

                _rawScene = await Task.Run(() => _parserService.ParseObj(InputFilePath, importSettings));

                Dictionary<string, string> matTextures = new Dictionary<string, string>();
                string mtlPath = null;

                if (!string.IsNullOrEmpty(_rawScene.MaterialLib))
                {
                    string dir = Path.GetDirectoryName(InputFilePath);
                    string checkPath = Path.Combine(dir, _rawScene.MaterialLib);
                    if (File.Exists(checkPath)) mtlPath = checkPath;
                }

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
                        ModelGroups.Add(new GroupViewModel(g, tex, () => 
                        {
                            RebuildBundle();
                            UpdateSelectAllButtonText();
                        }));
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

                    // --- FIX: Remap vertices to only include those used by selected indices ---
                    var usedVertexIndices = selectedIndices.Distinct().OrderBy(i => i).ToList();
                    var oldToNewIndexMap = new Dictionary<int, int>();
                    for (int i = 0; i < usedVertexIndices.Count; i++)
                    {
                        oldToNewIndexMap[usedVertexIndices[i]] = i;
                    }

                    // Create compacted vertex arrays with only used vertices
                    var compactedPositions = new Vector3[usedVertexIndices.Count];
                    var compactedNormals = new Vector3[usedVertexIndices.Count];
                    var compactedUVs = new Vector2[usedVertexIndices.Count];
                    var compactedTangents = new Vector4[usedVertexIndices.Count];

                    for (int i = 0; i < usedVertexIndices.Count; i++)
                    {
                        int oldIdx = usedVertexIndices[i];
                        compactedPositions[i] = _rawScene.Positions[oldIdx];
                        compactedNormals[i] = oldIdx < _rawScene.Normals.Length ? _rawScene.Normals[oldIdx] : Vector3.UnitY;
                        compactedUVs[i] = oldIdx < _rawScene.UVs.Length ? _rawScene.UVs[oldIdx] : Vector2.Zero;
                        compactedTangents[i] = oldIdx < _rawScene.Tangents.Length ? _rawScene.Tangents[oldIdx] : new Vector4(1, 0, 0, 1);
                    }

                    // Remap indices to reference the compacted arrays
                    var remappedIndices = selectedIndices.Select(i => oldToNewIndexMap[i]).ToArray();

                    // --- 1. Apply Vertex Modifiers (Position & Tangents) ---
                    if (FlipVertexX) FlipVector3Array(compactedPositions, true, false, false);
                    if (FlipVertexY) FlipVector3Array(compactedPositions, false, true, false);
                    if (FlipVertexZ) FlipVector3Array(compactedPositions, false, false, true);

                    // Tangents must flip with geometry
                    if (FlipVertexX) FlipVector4Array(compactedTangents, true, false, false);
                    if (FlipVertexY) FlipVector4Array(compactedTangents, false, true, false);
                    if (FlipVertexZ) FlipVector4Array(compactedTangents, false, false, true);

                    if (IsMirrorEnabled)
                    {
                        FlipVector3Array(compactedPositions, true, false, false); // Mirror X
                        FlipVector4Array(compactedTangents, true, false, false); // Mirror Tangent X
                        
                        // Mirroring inverts the coordinate system, so we must flip tangent handedness (W)
                        for (int i = 0; i < compactedTangents.Length; i++) compactedTangents[i].W *= -1;
                    }

                    // --- 2. Apply Index Modifiers (Winding) ---
                    // Mirroring requires flipping winding to maintain surface orientation
                    bool shouldFlipWinding = FlipFaces;
                    if (IsMirrorEnabled) shouldFlipWinding = !shouldFlipWinding;

                    if (shouldFlipWinding)
                    {
                        FlipFaceWinding(remappedIndices);
                    }

                    // --- 3. Handle Normals ---
                    Vector3[] finalNormals;

                    if (RecalculateNormals)
                    {
                        // Calculate normals based on the FINAL geometry (positions and winding)
                        // This ensures normals point in the correct direction relative to the surface
                        finalNormals = RecalculateMeshNormals(compactedPositions, remappedIndices);
                    }
                    else
                    {
                        finalNormals = compactedNormals;

                        // Apply modifiers to existing normals
                        if (FlipNormalX) FlipVector3Array(finalNormals, true, false, false);
                        if (FlipNormalY) FlipVector3Array(finalNormals, false, true, false);
                        if (FlipNormalZ) FlipVector3Array(finalNormals, false, false, true);

                        if (IsMirrorEnabled)
                        {
                            FlipVector3Array(finalNormals, true, false, false); // Mirror Normal X
                        }
                        
                        // Re-normalize to be safe
                        for(int i=0; i<finalNormals.Length; i++) 
                            finalNormals[i] = Vector3.Normalize(finalNormals[i]);
                    }

                    var geometryInput = new GeometryInput
                    {
                        Name = _rawScene.Name,
                        Positions = compactedPositions,
                        Normals = finalNormals,
                        UVs = compactedUVs,
                        Indices = remappedIndices,
                        Tangents = compactedTangents // Pass tangents to builder
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

        private Vector3[] RecalculateMeshNormals(Vector3[] positions, int[] indices)
        {
            Vector3[] newNormals = new Vector3[positions.Length];

            for (int i = 0; i < indices.Length; i += 3)
            {
                int i1 = indices[i];
                int i2 = indices[i + 1];
                int i3 = indices[i + 2];

                if (i1 >= positions.Length || i2 >= positions.Length || i3 >= positions.Length) continue;

                Vector3 v1 = positions[i1];
                Vector3 v2 = positions[i2];
                Vector3 v3 = positions[i3];

                Vector3 edge1 = v2 - v1;
                Vector3 edge2 = v3 - v1;
                Vector3 faceNormal = Vector3.Cross(edge1, edge2);

                newNormals[i1] += faceNormal;
                newNormals[i2] += faceNormal;
                newNormals[i3] += faceNormal;
            }

            for (int i = 0; i < newNormals.Length; i++)
            {
                if (newNormals[i].LengthSquared() > 0.000001f)
                {
                    newNormals[i] = Vector3.Normalize(newNormals[i]);
                }
                else
                {
                    newNormals[i] = Vector3.UnitY;
                }
            }

            return newNormals;
        }

        private void FlipVector3Array(Vector3[] arr, bool x, bool y, bool z)
        {
            if (!x && !y && !z) return;
            for (int i = 0; i < arr.Length; i++)
            {
                var v = arr[i];
                if (x) v.X = -v.X;
                if (y) v.Y = -v.Y;
                if (z) v.Z = -v.Z;
                arr[i] = v;
            }
        }

        private void FlipVector4Array(Vector4[] arr, bool x, bool y, bool z)
        {
            if (!x && !y && !z) return;
            for (int i = 0; i < arr.Length; i++)
            {
                var v = arr[i];
                if (x) v.X = -v.X;
                if (y) v.Y = -v.Y;
                if (z) v.Z = -v.Z;
                arr[i] = v;
            }
        }

        private void FlipFaceWinding(int[] indices)
        {
            for (int i = 0; i < indices.Length; i += 3)
            {
                if (i + 2 < indices.Length)
                {
                    (indices[i + 1], indices[i + 2]) = (indices[i + 2], indices[i + 1]);
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