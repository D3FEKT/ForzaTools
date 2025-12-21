using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reflection;
using ForzaTools.Bundles;
using ForzaTools.Bundles.Blobs;
using ForzaTools.Bundles.Metadata;
using ForzaTools.CarScene;

namespace ForzaTools.ForzaAnalyzer.ViewModels
{
    public partial class FileViewModel : ObservableObject
    {
        [ObservableProperty]
        private string _fileName;

        [ObservableProperty]
        private string _fileType;

        [ObservableProperty]
        private ObjectNode _selectedNode;

        public object ParsedObject { get; }

        public ObservableCollection<ObjectNode> Nodes { get; } = new();

        public FileViewModel(string fileName, object parsedObject)
        {
            FileName = fileName;
            ParsedObject = parsedObject;
            FileType = parsedObject.GetType().Name;

            BuildTree(parsedObject);
        }

        private void BuildTree(object obj)
        {
            if (obj is Bundle bundle)
            {
                var root = new ObjectNode("Bundle Root", bundle);
                Nodes.Add(root);

                int index = 0;
                foreach (var blob in bundle.Blobs)
                {
                    // 1. Determine Blob Name
                    string blobName = $"[{index}] {blob.GetType().Name.Replace("Blob", "")}";

                    // Check for Name Metadata to append to the tree node title
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

                    // Add Metadata Nodes
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
                    Properties.Add(new PropertyItem(p.Name, p.GetValue(Data), p, Data));
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
                            // 2. Custom Naming for Bones
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

        [ObservableProperty]
        private object _value;

        public string ValueAsString
        {
            get => _value?.ToString() ?? "";
            set
            {
                if (_propInfo.CanWrite && _target != null)
                {
                    try
                    {
                        var targetType = _propInfo.PropertyType;
                        var converted = Convert.ChangeType(value, targetType);
                        _propInfo.SetValue(_target, converted);
                        _value = converted;
                        OnPropertyChanged(nameof(Value));
                    }
                    catch { }
                }
            }
        }

        public bool IsReadOnly => !_propInfo.CanWrite;

        public PropertyItem(string name, object value, PropertyInfo propInfo, object target)
        {
            Name = name;
            _value = value;
            _propInfo = propInfo;
            _target = target;
        }
    }
}