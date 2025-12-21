using ForzaTools.ForzaAnalyzer.Services;
using ForzaTools.ForzaAnalyzer.ViewModels;
using HelixToolkit.WinUI;
using HelixToolkit.SharpDX.Core;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.Threading.Tasks;
using Windows.UI;
using SDX = SharpDX;

namespace ForzaTools.ForzaAnalyzer.Views
{
    public class ForzaMeshViewModel
    {
        public string Name { get; set; }
        public int Id { get; set; }
    }

    public sealed partial class ModelViewerPage : Page
    {
        public MainViewModel ViewModel { get; private set; }
        public ObservableCollection<ForzaMeshViewModel> Meshes { get; } = new();

        private Viewport3DX _viewport;
        private GroupModel3D _modelGroup;
        private List<ForzaGeometryData> _cachedGeometry = new();

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
            try
            {
                _viewport = new Viewport3DX
                {
                    BackgroundColor = Color.FromArgb(255, 30, 30, 30),
                    ShowCoordinateSystem = true,
                    ShowViewCube = true
                };

                _viewport.EffectsManager = new DefaultEffectsManager();
                _viewport.Camera = new PerspectiveCamera
                {
                    Position = new SDX.Vector3(50, 50, 50),
                    LookDirection = new SDX.Vector3(-50, -50, -50),
                    UpDirection = new SDX.Vector3(0, 1, 0),
                    FarPlaneDistance = 50000
                };

                _modelGroup = new GroupModel3D();
                AddDefaultCube();

                _viewport.Items.Add(new DirectionalLight3D { Direction = new SDX.Vector3(-1, -1, -1), Color = Microsoft.UI.Colors.White });
                _viewport.Items.Add(new AmbientLight3D { Color = Color.FromArgb(255, 100, 100, 100) });
                _viewport.Items.Add(_modelGroup);

                ViewportContainer.Children.Add(_viewport);
            }
            catch (Exception ex) { Log($"CRITICAL INIT ERROR: {ex.Message}"); }
        }

        private void AddDefaultCube()
        {
            var builder = new MeshBuilder();
            builder.AddBox(new SDX.Vector3(0, 0, 0), 10, 10, 10);
            _modelGroup.Children.Add(new MeshGeometryModel3D
            {
                Geometry = builder.ToMesh(),
                Material = new PhongMaterial { DiffuseColor = new SDX.Color4(0.0f, 0.47f, 0.84f, 1.0f) },
                CullMode = SDX.Direct3D11.CullMode.Back
            });
        }

        // --- LOAD FILE & RENDER ALL ---
        private async void FileList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (FileList.SelectedItem is not FileViewModel fileVm) return;
            if (fileVm.ParsedObject is not ForzaTools.Bundles.Bundle bundle) return;

            IsLoading = true;
            Meshes.Clear();
            _cachedGeometry.Clear();

            try
            {
                Log($"Parsing {fileVm.FileName}...");
                var result = await Task.Run(() => { var importer = new ModelImporter(); return importer.ExtractModels(bundle); });

                this.DispatcherQueue.TryEnqueue(() =>
                {
                    _cachedGeometry = result.Meshes;
                    if (_modelGroup != null) _modelGroup.Children.Clear();

                    for (int i = 0; i < _cachedGeometry.Count; i++)
                    {
                        var data = _cachedGeometry[i];
                        Meshes.Add(new ForzaMeshViewModel { Name = data.Name, Id = i });
                        RenderMesh(data);
                    }

                    foreach (var log in result.Logs) Log(log);

                    if (Meshes.Count > 0) _viewport.Camera.ZoomExtents(_viewport, 200);
                    else AddDefaultCube();

                    IsLoading = false;
                });
            }
            catch (Exception ex) { Log($"Error: {ex.Message}"); IsLoading = false; }
        }

        // --- ISOLATE SINGLE MESH ---
        private void MeshList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (MeshList.SelectedItem is not ForzaMeshViewModel vm) return;
            if (vm.Id < 0 || vm.Id >= _cachedGeometry.Count) return;

            try
            {
                var data = _cachedGeometry[vm.Id];
                _modelGroup.Children.Clear(); // Clear all
                RenderMesh(data);             // Render ONE
                Log($"Isolated: {data.Name}");
            }
            catch (Exception ex) { Log($"Render Error: {ex.Message}"); }
        }

        private void RenderMesh(ForzaGeometryData data)
        {
            var geometry = new MeshGeometry3D();
            var posCol = new Vector3Collection(); foreach (var p in data.Positions) posCol.Add(new SDX.Vector3(p.X, p.Y, p.Z));
            var normCol = new Vector3Collection(); foreach (var n in data.Normals) normCol.Add(new SDX.Vector3(n.X, n.Y, n.Z));
            var uvCol = new Vector2Collection(); foreach (var u in data.UVs) uvCol.Add(new SDX.Vector2(u.X, u.Y));
            var indCol = new IntCollection(); foreach (var i in data.Indices) indCol.Add(i);

            geometry.Positions = posCol; geometry.Normals = normCol; geometry.TextureCoordinates = uvCol; geometry.TriangleIndices = indCol;
            geometry.UpdateBounds();

            var rnd = new Random(data.Name?.GetHashCode() ?? 0);
            var matColor = new SDX.Color4((float)rnd.NextDouble(), (float)rnd.NextDouble(), (float)rnd.NextDouble(), 1.0f);

            _modelGroup.Children.Add(new MeshGeometryModel3D
            {
                Geometry = geometry,
                Material = new PhongMaterial { DiffuseColor = matColor },
                CullMode = SDX.Direct3D11.CullMode.Back
            });
        }

        private void Log(string message)
        {
            if (this.DispatcherQueue.HasThreadAccess) ErrorLog.Text += $"{DateTime.Now:mm:ss}: {message}\n";
            else this.DispatcherQueue.TryEnqueue(() => ErrorLog.Text += $"{DateTime.Now:mm:ss}: {message}\n");
        }
    }
}