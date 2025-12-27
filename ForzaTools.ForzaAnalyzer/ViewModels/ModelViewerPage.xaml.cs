using ForzaTools.ForzaAnalyzer.Services;
using ForzaTools.ForzaAnalyzer.ViewModels;
using HelixToolkit.SharpDX.Core;
using HelixToolkit.WinUI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Windows.UI;
using SDX = SharpDX;

namespace ForzaTools.ForzaAnalyzer.Views
{
    public class ForzaMeshViewModel : INotifyPropertyChanged
    {
        public string Name { get; set; }
        public int Id { get; set; }
        public ForzaGeometryData Data { get; set; }

        private bool _isVisible = true;
        public bool IsVisible
        {
            get => _isVisible;
            set { _isVisible = value; OnPropertyChanged(); }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public sealed partial class ModelViewerPage : Page
    {
        public MainViewModel ViewModel { get; private set; }
        public ObservableCollection<ForzaMeshViewModel> Meshes { get; } = new();

        private Viewport3DX _viewport;
        private GroupModel3D _modelGroup;
        private Dictionary<ForzaMeshViewModel, MeshGeometryModel3D> _meshRenderMap = new();
        private bool _isUpdatingUi = false;

        // Track the currently selected file for Saving and Unloading
        public FileViewModel SelectedFile
        {
            get { return (FileViewModel)GetValue(SelectedFileProperty); }
            set { SetValue(SelectedFileProperty, value); }
        }
        public static readonly DependencyProperty SelectedFileProperty =
            DependencyProperty.Register("SelectedFile", typeof(FileViewModel), typeof(ModelViewerPage), new PropertyMetadata(null));

        public bool IsLoading
        {
            get { return (bool)GetValue(IsLoadingProperty); }
            set { SetValue(IsLoadingProperty, value); }
        }
        public static readonly DependencyProperty IsLoadingProperty =
            DependencyProperty.Register("IsLoading", typeof(bool), typeof(ModelViewerPage), new PropertyMetadata(false));

        public ModelViewerPage()
        {
            this.InitializeComponent();
            this.Loaded += ModelViewerPage_Loaded;
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            if (e.Parameter is MainViewModel mainVm) ViewModel = mainVm;
        }

        private void ModelViewerPage_Loaded(object sender, RoutedEventArgs e)
        {
            this.DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, () => Initialize3DView());
        }

        private void Initialize3DView()
        {
            if (_viewport != null) return;

            _viewport = new Viewport3DX
            {
                BackgroundColor = Color.FromArgb(255, 30, 30, 30),
                ShowCoordinateSystem = true,
                ShowViewCube = true,
                EffectsManager = new DefaultEffectsManager()
            };

            _viewport.Camera = new PerspectiveCamera
            {
                Position = new SDX.Vector3(50, 50, 50),
                LookDirection = new SDX.Vector3(-50, -50, -50),
                UpDirection = new SDX.Vector3(0, 1, 0),
                FarPlaneDistance = 50000
            };

            _modelGroup = new GroupModel3D();

            // Default Debug Cube (Displayed only until first file load)
            var builder = new MeshBuilder();
            builder.AddBox(new SDX.Vector3(0, 0, 0), 15, 15, 15);
            var cubeModel = new MeshGeometryModel3D
            {
                Geometry = builder.ToMesh(),
                Material = new PhongMaterial { DiffuseColor = new SDX.Color4(1, 0, 0, 1) }
            };
            _modelGroup.Children.Add(cubeModel);

            _viewport.Items.Add(new DirectionalLight3D { Direction = new SDX.Vector3(-1, -1, -1), Color = Microsoft.UI.Colors.White });
            _viewport.Items.Add(new AmbientLight3D { Color = Color.FromArgb(255, 100, 100, 100) });
            _viewport.Items.Add(_modelGroup);

            ViewportContainer.Children.Add(_viewport);
        }

        private async void FileList_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is not FileViewModel fileVm) return;

            // Prevent re-loading same file
            if (SelectedFile == fileVm) return;

            // 1. Unload previous file to free memory
            if (SelectedFile != null)
            {
                SelectedFile.Unload();
            }

            // 2. Clear Scene IMMEDIATELY (Removes debug cube or previous model)
            if (_modelGroup != null) _modelGroup.Children.Clear();
            Meshes.Clear();
            _meshRenderMap.Clear();

            // 3. Set new selection and Show Loading
            SelectedFile = fileVm;
            IsLoading = true;

            try
            {
                // 4. Force Load (Parses file into memory)
                await fileVm.EnsureLoadedAsync();

                if (fileVm.ParsedObject is not ForzaTools.Bundles.Bundle bundle)
                {
                    IsLoading = false;
                    return;
                }

                // 5. Extract Meshes (Heavy work on background thread)
                var result = await Task.Run(() =>
                {
                    var importer = new ModelImporter();
                    return importer.ExtractModels(bundle);
                });

                // 6. Update 3D View (UI Thread)
                this.DispatcherQueue.TryEnqueue(() =>
                {
                    var totalMin = new System.Numerics.Vector3(float.MaxValue);
                    var totalMax = new System.Numerics.Vector3(float.MinValue);
                    bool hasVertices = false;

                    for (int i = 0; i < result.Meshes.Count; i++)
                    {
                        var data = result.Meshes[i];
                        var mesh3d = CreateMesh3D(data);
                        _modelGroup.Children.Add(mesh3d);

                        var meshVm = new ForzaMeshViewModel
                        {
                            Name = data.Name,
                            Id = i,
                            IsVisible = true,
                            Data = data
                        };

                        _meshRenderMap[meshVm] = mesh3d;
                        meshVm.PropertyChanged += MeshVm_PropertyChanged;
                        Meshes.Add(meshVm);

                        if (data.Positions != null)
                        {
                            foreach (var p in data.Positions)
                            {
                                totalMin = System.Numerics.Vector3.Min(totalMin, p);
                                totalMax = System.Numerics.Vector3.Max(totalMax, p);
                                hasVertices = true;
                            }
                        }
                    }

                    if (hasVertices && _viewport.Camera is PerspectiveCamera pCam)
                    {
                        var min = new SDX.Vector3(totalMin.X, totalMin.Y, totalMin.Z);
                        var max = new SDX.Vector3(totalMax.X, totalMax.Y, totalMax.Z);
                        var center = (max + min) * 0.5f;
                        var sizeVec = max - min;
                        var diagonal = sizeVec.Length();

                        if (diagonal < 0.1f) diagonal = 10.0f;

                        var fovRad = pCam.FieldOfView * (Math.PI / 180.0);
                        var dist = (diagonal / 2.0) / Math.Sin(fovRad / 2.0);
                        dist *= 2.0f;

                        var dir = pCam.LookDirection;
                        if (dir.LengthSquared() < 0.001f) dir = new SDX.Vector3(-1, -1, -1);
                        dir.Normalize();

                        pCam.Position = center - (dir * (float)dist);
                        pCam.LookDirection = dir * (float)dist;
                    }

                    IsLoading = false;
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Viewer Load Error: {ex}");
                IsLoading = false;
            }
        }

        private void MeshVm_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ForzaMeshViewModel.IsVisible))
            {
                if (sender is ForzaMeshViewModel vm && _meshRenderMap.TryGetValue(vm, out var mesh3d))
                {
                    mesh3d.IsRendering = vm.IsVisible;
                }
            }
        }

        private MeshGeometryModel3D CreateMesh3D(ForzaGeometryData data)
        {
            var geometry = new MeshGeometry3D();
            var posCol = new Vector3Collection(); foreach (var p in data.Positions) posCol.Add(new SDX.Vector3(p.X, p.Y, p.Z));
            var normCol = new Vector3Collection(); foreach (var n in data.Normals) normCol.Add(new SDX.Vector3(n.X, n.Y, n.Z));
            var uvCol = new Vector2Collection(); foreach (var u in data.UVs) uvCol.Add(new SDX.Vector2(u.X, u.Y));
            var indCol = new IntCollection(); foreach (var i in data.Indices) indCol.Add(i);

            geometry.Positions = posCol; geometry.Normals = normCol; geometry.TextureCoordinates = uvCol; geometry.TriangleIndices = indCol;
            geometry.UpdateBounds();

            var rnd = new Random(data.Name?.GetHashCode() ?? 0);
            return new MeshGeometryModel3D
            {
                Geometry = geometry,
                Material = new PhongMaterial { DiffuseColor = new SDX.Color4((float)rnd.NextDouble(), (float)rnd.NextDouble(), (float)rnd.NextDouble(), 1.0f) },
                CullMode = SDX.Direct3D11.CullMode.None
            };
        }

        private void PopulateTransformFields(ForzaMeshViewModel vm)
        {
            if (vm == null || vm.Data?.SourceMesh == null) return;

            _isUpdatingUi = true;
            var mesh = vm.Data.SourceMesh;

            ScaleX.Text = mesh.PositionScale.X.ToString("0.0000");
            ScaleY.Text = mesh.PositionScale.Y.ToString("0.0000");
            ScaleZ.Text = mesh.PositionScale.Z.ToString("0.0000");

            TransX.Text = mesh.PositionTranslate.X.ToString("0.0000");
            TransY.Text = mesh.PositionTranslate.Y.ToString("0.0000");
            TransZ.Text = mesh.PositionTranslate.Z.ToString("0.0000");
            _isUpdatingUi = false;
        }

        private void MeshList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.AddedItems.FirstOrDefault() is ForzaMeshViewModel selectedVm)
            {
                PopulateTransformFields(selectedVm);
            }
        }

        private void CheckBox_Checked(object sender, RoutedEventArgs e)
        {
            if (sender is CheckBox cb && cb.DataContext is ForzaMeshViewModel vm)
            {
                PopulateTransformFields(vm);
            }
        }

        private void Transform_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isUpdatingUi) return;

            if (!float.TryParse(ScaleX.Text, out float sx)) return;
            if (!float.TryParse(ScaleY.Text, out float sy)) return;
            if (!float.TryParse(ScaleZ.Text, out float sz)) return;
            if (!float.TryParse(TransX.Text, out float tx)) return;
            if (!float.TryParse(TransY.Text, out float ty)) return;
            if (!float.TryParse(TransZ.Text, out float tz)) return;

            foreach (var vm in Meshes)
            {
                if (vm.IsVisible && _meshRenderMap.TryGetValue(vm, out var mesh3d))
                {
                    // 1. Update Source Blob (for saving)
                    var meshBlob = vm.Data.SourceMesh;
                    meshBlob.PositionScale = new Vector4(sx, sy, sz, meshBlob.PositionScale.W);
                    meshBlob.PositionTranslate = new Vector4(tx, ty, tz, meshBlob.PositionTranslate.W);

                    // 2. Re-calculate Vertex Positions Live
                    var geometry = mesh3d.Geometry as MeshGeometry3D;
                    if (geometry == null) continue;

                    var newPositions = new Vector3Collection(vm.Data.RawPositions.Length);
                    for (int i = 0; i < vm.Data.RawPositions.Length; i++)
                    {
                        Vector3 raw = vm.Data.RawPositions[i];
                        float nx = raw.X * sx + tx;
                        float ny = raw.Y * sy + ty;
                        float nz = raw.Z * sz + tz;
                        newPositions.Add(new SDX.Vector3(nx, ny, nz));
                    }
                    geometry.Positions = newPositions;
                    geometry.UpdateBounds();
                }
            }
        }
    }
}