using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Reflection;
using System.Threading.Tasks;
using Windows.Storage.Pickers;
using ForzaTools.Bundles;
using ForzaTools.Bundles.Blobs;
using ForzaTools.Bundles.Metadata;
using ForzaTools.CarScene;
using ForzaTools.ForzaAnalyzer.Services;

namespace ForzaTools.ForzaAnalyzer.ViewModels
{
    public partial class FileViewModel : ObservableObject
    {
        private readonly string _filePath;
        private readonly FileService _fileService;
        private bool _isLoaded;
        private bool _isLoading;

        [ObservableProperty]
        private string _fileName;

        [ObservableProperty]
        private string _fileType;

        [ObservableProperty]
        private ObjectNode _selectedNode;

        // Make ParsedObject settable so we can populate it later
        private object _parsedObject;
        public object ParsedObject
        {
            get => _parsedObject;
            private set => SetProperty(ref _parsedObject, value);
        }

        public ObservableCollection<ObjectNode> Nodes { get; } = new();

        public FileViewModel(string fileName, string filePath, FileService fileService)
        {
            FileName = fileName;
            _filePath = filePath;
            _fileService = fileService;

            // Initial state
            FileType = "Not Loaded";
        }

        public async Task EnsureLoadedAsync()
        {
            if (_isLoaded || _isLoading) return;

            try
            {
                _isLoading = true;
                FileType = "Loading...";

                var obj = await _fileService.LoadFileAsync(_filePath);

                if (obj != null)
                {
                    ParsedObject = obj;
                    FileType = obj.GetType().Name;
                    BuildTree(obj);
                    _isLoaded = true;
                }
                else
                {
                    FileType = "Load Failed";
                }
            }
            catch (Exception ex)
            {
                FileType = $"Error: {ex.Message}";
            }
            finally
            {
                _isLoading = false;
            }
        }

        // NEW: Unload method to clear memory
        public void Unload()
        {
            if (!_isLoaded) return;

            // 1. Drop references to heavy objects
            ParsedObject = null;
            Nodes.Clear();
            SelectedNode = null;

            // 2. Reset state
            FileType = "Not Loaded";
            _isLoaded = false;
            _isLoading = false;

            // 3. Force GC to reclaim memory immediately (Aggressive cleanup)
            GC.Collect();
            GC.WaitForPendingFinalizers();
        }

        [RelayCommand]
        public async Task SaveFileAsync()
        {
            if (ParsedObject is Bundle bundle)
            {
                var picker = new FileSavePicker();
                var window = App.MainWindow;
                var hWnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
                WinRT.Interop.InitializeWithWindow.Initialize(picker, hWnd);

                picker.SuggestedStartLocation = PickerLocationId.ComputerFolder;
                picker.FileTypeChoices.Add("Forza ModelBin", new List<string>() { ".modelbin" });
                picker.SuggestedFileName = FileName;

                var file = await picker.PickSaveFileAsync();
                if (file != null)
                {
                    try
                    {
                        using var stream = await file.OpenStreamForWriteAsync();
                        stream.SetLength(0);
                        bundle.Serialize(stream);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Save Error: {ex.Message}");
                    }
                }
            }
        }

        private void BuildTree(object obj)
        {
            Nodes.Clear(); // Ensure empty before filling

            if (obj is Bundle bundle)
            {
                var root = new ObjectNode("Bundle Root", bundle);
                Nodes.Add(root);

                int index = 0;
                foreach (var blob in bundle.Blobs)
                {
                    string blobName = $"[{index}] {blob.GetType().Name.Replace("Blob", "")}";
                    var nameMeta = blob.Metadatas.OfType<NameMetadata>().FirstOrDefault();
                    if (nameMeta != null && !string.IsNullOrWhiteSpace(nameMeta.Name))
                    {
                        blobName += $" ({nameMeta.Name})";
                    }
                    else if (blob is GenericBlob generic)
                    {
                        blobName += $" (Tag: {blob.Tag:X})";
                    }

                    var blobNode = new ObjectNode(blobName, blob);
                    root.Children.Add(blobNode);

                    int metaIndex = 0;
                    foreach (var meta in blob.Metadatas)
                    {
                        var metaNode = new ObjectNode($"Meta [{metaIndex}] {meta.GetType().Name}", meta);
                        blobNode.Children.Add(metaNode);
                        metaIndex++;
                    }
                    index++;
                }
            }
            else if (obj is Scene scene)
            {
                var root = new ObjectNode("Scene Root", scene);
                Nodes.Add(root);
                root.PopulateChildrenFromProperties(scene);
            }
        }
    }

    // ObjectNode and PropertyItem classes remain unchanged...
    public partial class ObjectNode : ObservableObject
    {
        public string Title { get; }
        public object Data { get; }
        public ObservableCollection<ObjectNode> Children { get; } = new();
        public ObservableCollection<PropertyItem> Properties { get; } = new();

        public ObjectNode(string title, object data)
        {
            Title = title;
            Data = data;
            GenerateProperties();
            PopulateChildrenFromProperties(data);
        }

        private void GenerateProperties()
        {
            if (Data == null) return;

            var props = Data.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);
            foreach (var p in props)
            {
                if (p.PropertyType != typeof(string) && typeof(IEnumerable).IsAssignableFrom(p.PropertyType)) continue;

                try
                {
                    Properties.Add(new PropertyItem(p.Name, p, Data));
                }
                catch { }
            }
        }

        public void PopulateChildrenFromProperties(object obj)
        {
            if (obj == null) return;

            var props = obj.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);
            foreach (var p in props)
            {
                if (p.PropertyType != typeof(string) && typeof(IEnumerable).IsAssignableFrom(p.PropertyType))
                {
                    var val = p.GetValue(obj) as IEnumerable;
                    if (val != null)
                    {
                        var collectionNode = new ObjectNode(p.Name, val);
                        Children.Add(collectionNode);

                        int i = 0;
                        foreach (var item in val)
                        {
                            string itemTitle = $"[{i}] {item?.GetType().Name ?? "Null"}";
                            if (item is Bone bone && !string.IsNullOrWhiteSpace(bone.Name))
                            {
                                itemTitle = $"Bone: {bone.Name}";
                            }
                            var itemNode = new ObjectNode(itemTitle, item);
                            collectionNode.Children.Add(itemNode);
                            i++;
                        }
                    }
                }
            }
        }
    }

    public partial class PropertyItem : ObservableObject
    {
        private PropertyInfo _propInfo;
        private object _target;

        public string Name { get; }

        public string ValueAsString
        {
            get
            {
                object currentValue = null;
                try { currentValue = _propInfo.GetValue(_target); } catch { }

                if (currentValue is Vector4 v4) return $"{v4.X}, {v4.Y}, {v4.Z}, {v4.W}";
                return currentValue?.ToString() ?? "";
            }
            set
            {
                if (_propInfo.CanWrite && _target != null)
                {
                    try
                    {
                        var targetType = _propInfo.PropertyType;
                        object converted = null;

                        if (targetType == typeof(Vector4))
                        {
                            var parts = value.Split(',', StringSplitOptions.RemoveEmptyEntries)
                                             .Select(s => float.Parse(s.Trim()))
                                             .ToArray();

                            if (parts.Length >= 4)
                                converted = new Vector4(parts[0], parts[1], parts[2], parts[3]);
                            else if (parts.Length == 3)
                                converted = new Vector4(parts[0], parts[1], parts[2], 1.0f);
                            else
                                return;
                        }
                        else
                        {
                            converted = Convert.ChangeType(value, targetType);
                        }

                        _propInfo.SetValue(_target, converted);
                        OnPropertyChanged(nameof(ValueAsString));
                    }
                    catch { }
                }
            }
        }

        public bool IsReadOnly => !_propInfo.CanWrite;

        public PropertyItem(string name, PropertyInfo propInfo, object target)
        {
            Name = name;
            _propInfo = propInfo;
            _target = target;
        }

        public void Refresh()
        {
            OnPropertyChanged(nameof(ValueAsString));
        }
    }
}