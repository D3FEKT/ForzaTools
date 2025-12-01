using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Windows.Forms;
using System.Reflection;
using System.Linq;
using ForzaTools.Bundles;
using ForzaTools.Bundles.Blobs;
using ForzaTools.Shared;
using Syroot.BinaryData;

namespace ForzaTools.ModelBinEditor
{
    public partial class MainForm : Form
    {
        private Bundle currentBundle;
        private string currentFilePath;
        private ContextMenuStrip propertyGridContextMenu;
        private ToolStripMenuItem viewPropertyHexMenuItem;

        // Definition for Game Versions (Kept for future use)
        private class GameProfile
        {
            public string Name { get; set; }

            public Version LSCE_Version { get; set; }
            public Version Mesh_Version { get; set; }
            public Version MTPR_Version { get; set; }
            public Version VLay_Version { get; set; }
            public Version Modl_Version { get; set; }

            public byte BundleMajor { get; set; }
            public byte BundleMinor { get; set; }

            public override string ToString() => Name;
        }

        public MainForm()
        {
            InitializeComponent();
            InitializePropertyGridContextMenu();
            InitializeTargetVersionDropdown();
            InitializeEnhancedUI();
        }

        private void InitializeTargetVersionDropdown()
        {
            var games = new List<GameProfile>
            {
                new GameProfile { Name = "Forza Horizon 5 / Motorsport 2023",
                    LSCE_Version = new Version(1, 8), Mesh_Version = new Version(1, 9), MTPR_Version = new Version(2, 1), VLay_Version = new Version(1, 1), Modl_Version = new Version(1, 3), BundleMajor = 1, BundleMinor = 1 },

                new GameProfile { Name = "Forza Horizon 4",
                    LSCE_Version = new Version(1, 6), Mesh_Version = new Version(1, 9), MTPR_Version = new Version(2, 0), VLay_Version = new Version(1, 1), Modl_Version = new Version(1, 2), BundleMajor = 1, BundleMinor = 1 },

                new GameProfile { Name = "Forza Motorsport 7",
                    LSCE_Version = new Version(1, 6), Mesh_Version = new Version(1, 9), MTPR_Version = new Version(2, 0), VLay_Version = new Version(1, 1), Modl_Version = new Version(1, 2), BundleMajor = 1, BundleMinor = 1 },

                new GameProfile { Name = "Forza 6 Apex",
                    LSCE_Version = new Version(1, 6), Mesh_Version = new Version(1, 8), MTPR_Version = new Version(2, 0), VLay_Version = new Version(1, 1), Modl_Version = new Version(1, 2), BundleMajor = 1, BundleMinor = 1 },

                new GameProfile { Name = "Forza Horizon 3",
                    LSCE_Version = new Version(1, 5), Mesh_Version = new Version(1, 8), MTPR_Version = new Version(2, 0), VLay_Version = new Version(1, 1), Modl_Version = new Version(1, 2), BundleMajor = 1, BundleMinor = 1 },

                new GameProfile { Name = "Forza Horizon 2",
                    LSCE_Version = new Version(1, 4), Mesh_Version = new Version(1, 7), MTPR_Version = new Version(2, 0), VLay_Version = new Version(1, 1), Modl_Version = new Version(1, 1), BundleMajor = 1, BundleMinor = 0 },

                new GameProfile { Name = "Forza Motorsport 6",
                    LSCE_Version = new Version(1, 4), Mesh_Version = new Version(1, 7), MTPR_Version = new Version(2, 0), VLay_Version = new Version(1, 1), Modl_Version = new Version(1, 1), BundleMajor = 1, BundleMinor = 0 },

                new GameProfile { Name = "Forza Motorsport 5",
                    LSCE_Version = new Version(1, 3), Mesh_Version = new Version(1, 7), MTPR_Version = new Version(2, 0), VLay_Version = new Version(0, 0), Modl_Version = new Version(1, 1), BundleMajor = 1, BundleMinor = 0 }
            };

            foreach (var g in games)
                targetVersionComboBox.Items.Add(g);

            targetVersionComboBox.SelectedIndex = 0;
        }

        private void InitializePropertyGridContextMenu()
        {
            propertyGridContextMenu = new ContextMenuStrip();
            viewPropertyHexMenuItem = new ToolStripMenuItem("View/Edit Value in Hex Editor");
            viewPropertyHexMenuItem.Click += ViewPropertyHex_Click;
            propertyGridContextMenu.Items.Add(viewPropertyHexMenuItem);

            propertyGrid.ContextMenuStrip = propertyGridContextMenu;
            propertyGrid.ContextMenuStrip.Opening += PropertyGridContextMenu_Opening;

            propertyGrid.PropertySort = PropertySort.NoSort;
        }
        private bool MakeFH5Compatible(Bundle bundle)
        {
            // FIX 1: Call GetBlobByIndex on the 'bundle' instance
            MeshBlob meshBlob = (MeshBlob)bundle.GetBlobByIndex(Bundle.TAG_BLOB_Mesh, 0);
            if (meshBlob == null) throw new Exception("No mesh blob found in the model.");

            // FIX 2: Call GetBlobByIndex on the 'bundle' instance
            VertexLayoutBlob layout = (VertexLayoutBlob)bundle.GetBlobByIndex(Bundle.TAG_BLOB_VertexLayout, meshBlob.VertexLayoutIndex);
            if (layout == null) throw new Exception("No vertex layout blob found in the model.");

            // Check if the model already has a third tangent component
            bool hasThirdTangentComponent = false;
            foreach (var element in layout.Elements)
            {
                if (layout.SemanticNames[element.SemanticNameIndex] == "TANGENT" && element.SemanticIndex == 2)
                {
                    hasThirdTangentComponent = true;
                    break;
                }
            }

            // If it exists, we don't need to do anything
            if (hasThirdTangentComponent) return false;

            // Find where to insert the new component (after existing TANGENTs)
            int tangentIndex = layout.Elements.FindIndex(e => layout.SemanticNames[e.SemanticNameIndex] == "TANGENT");
            if (tangentIndex < 0) throw new Exception("No tangent semantic found in vertex layout.");

            // Create new Input Layout Element
            D3D12_INPUT_LAYOUT_DESC thirdTangentComponent = new D3D12_INPUT_LAYOUT_DESC()
            {
                SemanticNameIndex = (short)layout.SemanticNames.IndexOf("TANGENT"),
                Format = DXGI_FORMAT.DXGI_FORMAT_R10G10B10A2_UNORM, // FH5 specific format
                InputSlot = 1,
                SemanticIndex = 2,
                AlignedByteOffset = -1,
                InstanceDataStepRate = 0,
            };

            // Insert into Layout
            layout.Elements.Insert(tangentIndex + 2, thirdTangentComponent);
            layout.PackedFormats.Insert(tangentIndex + 2, DXGI_FORMAT.DXGI_FORMAT_R8G8_TYPELESS);
            layout.Flags |= 0x80; // Required flag for FH5

            // FIX 3: Call GetDataOffsetOfElement on the 'layout' instance
            // (Ensure you made this method PUBLIC in VertexLayoutBlob.cs)
            int offset = layout.GetDataOffsetOfElement("TANGENT", 2);

            // FIX 4: Call GetBlobByIndex on the 'bundle' instance
            VertexBufferBlob buffer = (VertexBufferBlob)bundle.GetBlobByIndex(Bundle.TAG_BLOB_VertexBuffer, 1);
            if (buffer == null) throw new Exception("No vertex buffer blob found in the model.");

            // Insert placeholder data (0xFF) into the vertex buffer streams
            for (int i = 0; i < buffer.Header.Data.Length; i++)
            {
                var l = buffer.Header.Data[i].ToList();
                l.Insert(offset, 0xFF);
                l.Insert(offset + 1, 0xFF);
                l.Insert(offset + 2, 0xFF);
                l.Insert(offset + 3, 0xFF);

                buffer.Header.Data[i] = l.ToArray();
            }

            // Update Header info
            byte totalSize = layout.GetTotalVertexSize();

            // Use Stride (from previous fix)
            buffer.Header.Stride = totalSize;

            buffer.Header.NumElements = (byte)layout.Elements.Count;

            return true;
        }

        // Helper to calculate the byte offset of a specific element in the vertex layout
        private int GetDataOffsetOfElement(VertexLayoutBlob layout, string semanticName, int semanticIndex)
        {
            int offset = 0;
            for (int i = 0; i < layout.Elements.Count; i++)
            {
                var element = layout.Elements[i];
                string name = layout.SemanticNames[element.SemanticNameIndex];

                if (name == semanticName && element.SemanticIndex == semanticIndex)
                    return offset;

                DXGI_FORMAT format = layout.PackedFormats[i];
                offset += GetSizeOfElementFormat(format);

                // Replicate alignment logic from VertexLayoutBlob
                if (i + 1 < layout.Elements.Count && offset % 4 != 0)
                {
                    if (GetSizeOfElementFormat(layout.PackedFormats[i + 1]) >= 4)
                        offset += (offset % 4);
                }
            }
            return -1;
        }

        // Helper to get size of DXGI Formats (Local copy as the original might be inaccessible)
        private static byte GetSizeOfElementFormat(DXGI_FORMAT format)
        {
            return format switch
            {
                DXGI_FORMAT.DXGI_FORMAT_R32G32B32A32_FLOAT => 16,
                DXGI_FORMAT.DXGI_FORMAT_R32G32B32_FLOAT => 12,
                DXGI_FORMAT.DXGI_FORMAT_R32G32_FLOAT => 8,
                DXGI_FORMAT.DXGI_FORMAT_R32_FLOAT => 4,
                DXGI_FORMAT.DXGI_FORMAT_R16G16_UNORM => 4,
                DXGI_FORMAT.DXGI_FORMAT_R16G16_SNORM => 4,
                DXGI_FORMAT.DXGI_FORMAT_R16G16_FLOAT => 4,
                DXGI_FORMAT.DXGI_FORMAT_R8G8B8A8_UNORM => 4,
                DXGI_FORMAT.DXGI_FORMAT_R8G8_UNORM => 2,
                DXGI_FORMAT.DXGI_FORMAT_R8G8_SINT => 2,
                DXGI_FORMAT.DXGI_FORMAT_R10G10B10A2_UNORM => 4,
                DXGI_FORMAT.DXGI_FORMAT_R11G11B10_FLOAT => 4,
                DXGI_FORMAT.DXGI_FORMAT_R24_UNORM_X8_TYPELESS => 4,
                DXGI_FORMAT.DXGI_FORMAT_R8G8_TYPELESS => 2,
                DXGI_FORMAT.DXGI_FORMAT_X32_TYPELESS_G8X24_UINT => 8,
                _ => 4,
            };
        }

        private void ConvertButton_Click(object sender, EventArgs e)
        {
            if (currentBundle == null) return;

            // Check if FH5 is selected (or just run it as the primary conversion logic)
            var selectedProfile = targetVersionComboBox.SelectedItem as GameProfile;
            if (selectedProfile == null || !selectedProfile.Name.Contains("Horizon 5"))
            {
                if (MessageBox.Show("The conversion logic is specifically designed for Forza Horizon 5.\nContinue anyway?", "Version Mismatch", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
                    return;
            }

            try
            {
                // Perform the FH5 compatibility conversion
                bool modified = MakeFH5Compatible(currentBundle);

                if (modified)
                {
                    // Serialize to memory to refresh the bundle state
                    using (MemoryStream ms = new MemoryStream())
                    {
                        currentBundle.Serialize(ms);
                        ms.Position = 0;

                        // Reload the bundle to reflect changes in UI
                        var newBundle = new Bundle();
                        newBundle.Load(ms);
                        currentBundle = newBundle;
                        PopulateTree();
                    }
                    MessageBox.Show("Conversion to FH5 format completed successfully.", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                else
                {
                    MessageBox.Show("Model appears to already be compatible with FH5 (Tangent 3 found).", "No Changes Needed", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Conversion failed: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void Exit_Click(object sender, EventArgs e) => Close();

        private void OpenFile_Click(object sender, EventArgs e)
        {
            using (OpenFileDialog ofd = new OpenFileDialog())
            {
                ofd.Filter = "Forza Files (*.modelbin;*.swatchbin)|*.modelbin;*.swatchbin|All files (*.*)|*.*";
                if (ofd.ShowDialog() == DialogResult.OK) LoadFile(ofd.FileName);
            }
        }

        private void LoadFile(string path)
        {
            try
            {
                var newBundle = new Bundle();
                using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read))
                {
                    newBundle.Load(fs);
                }

                currentBundle = newBundle;
                currentFilePath = path;

                // Detection logic removed

                PopulateTree();

                this.Text = $"Forza ModelBin Editor - {Path.GetFileName(path)}";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading file: {ex.Message}", "Load Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }


        private void PopulateTree()
        {
            treeView.BeginUpdate();
            try
            {
                treeView.Nodes.Clear();
                propertyGrid.SelectedObject = null;

                if (currentBundle == null) return;

                TreeNode root = new TreeNode($"Bundle ({currentBundle.Blobs.Count} items)");
                root.Tag = currentBundle;
                treeView.Nodes.Add(root);

                // Get filter text
                string filter = searchTextBox.Text.ToLower();
                bool useGroups = groupByTagButton.Checked;

                // Dictionary to hold group nodes if grouping is enabled
                Dictionary<string, TreeNode> groups = new Dictionary<string, TreeNode>();

                for (int i = 0; i < currentBundle.Blobs.Count; i++)
                {
                    var blob = currentBundle.Blobs[i];

                    // Generate a descriptive name
                    string blobName = GetBlobName(blob, i);

                    // SEARCH FILTER: Skip if name doesn't match
                    if (!string.IsNullOrEmpty(filter) && !blobName.ToLower().Contains(filter))
                        continue;

                    TreeNode blobNode = new TreeNode(blobName);
                    blobNode.Tag = blob;

                    // --- Add Metadata Nodes (Keep existing logic) ---
                    TreeNode metaRoot = new TreeNode("Metadata");
                    foreach (var meta in blob.Metadatas)
                    {
                        TreeNode metaNode = new TreeNode(GetMetadataName(meta));
                        metaNode.Tag = meta;
                        metaRoot.Nodes.Add(metaNode);
                    }
                    if (metaRoot.Nodes.Count > 0) blobNode.Nodes.Add(metaRoot);
                    // -----------------------------------------------

                    // GROUPING LOGIC
                    if (useGroups)
                    {
                        // Extract tag name (e.g., "Mesh", "Material")
                        string typeName = blob.GetType().Name.Replace("Blob", "");

                        if (!groups.ContainsKey(typeName))
                        {
                            TreeNode groupNode = new TreeNode(typeName);
                            groups[typeName] = groupNode;
                            root.Nodes.Add(groupNode);
                        }
                        groups[typeName].Nodes.Add(blobNode);
                    }
                    else
                    {
                        root.Nodes.Add(blobNode);
                    }

                    if (blob is MaterialBlob matBlob && matBlob.Bundle != null)
                    {
                        // Create a folder for the nested bundle
                        TreeNode subBundleNode = new TreeNode($"Nested Bundle ({matBlob.Bundle.Blobs.Count} items)");

                        for (int j = 0; j < matBlob.Bundle.Blobs.Count; j++)
                        {
                            var subBlob = matBlob.Bundle.Blobs[j];

                            // Create node for the sub-blob using the same naming helper
                            TreeNode subBlobNode = new TreeNode(GetBlobName(subBlob, j));
                            subBlobNode.Tag = subBlob; // Allow PropertyGrid inspection

                            // Add Metadata for the sub-blob
                            TreeNode subMetaRoot = new TreeNode("Metadata");
                            foreach (var subMeta in subBlob.Metadatas)
                            {
                                TreeNode subMetaNode = new TreeNode(GetMetadataName(subMeta));
                                subMetaNode.Tag = subMeta;
                                subMetaRoot.Nodes.Add(subMetaNode);
                            }

                            // Only add metadata folder if it has items
                            if (subMetaRoot.Nodes.Count > 0)
                            {
                                subBlobNode.Nodes.Add(subMetaRoot);
                            }

                            // Add the sub-blob to the nested bundle folder
                            subBundleNode.Nodes.Add(subBlobNode);
                        }

                        // Add the nested bundle folder to the main Material node
                        blobNode.Nodes.Add(subBundleNode);
                    }

                    if (blob is SkeletonBlob skel)
                    {
                        TreeNode bonesRoot = new TreeNode($"BonesList ({skel.Bones.Count} items)");

                        for (int b = 0; b < skel.Bones.Count; b++)
                        {
                            var bone = skel.Bones[b];

                            // Format: [Index] BoneName (Parent: ParentIndex)
                            StringBuilder boneLabel = new StringBuilder();
                            boneLabel.Append($"[{b}] {bone.Name}");

                            if (bone.ParentId > -1)
                                boneLabel.Append($" (Parent: {bone.ParentId})");

                            TreeNode boneNode = new TreeNode(boneLabel.ToString());
                            boneNode.Tag = bone; // Links to PropertyGrid

                            bonesRoot.Nodes.Add(boneNode);
                        }

                        // Only add the folder if there are actual bones
                        if (bonesRoot.Nodes.Count > 0)
                        {
                            blobNode.Nodes.Add(bonesRoot);
                        }
                    }
                }

                root.Expand();
                // If searching, expand all matches
                if (!string.IsNullOrEmpty(filter)) root.ExpandAll();
            }
            finally
            {
                treeView.EndUpdate();
            }
        }

        private string GetBlobName(BundleBlob blob, int index)
        {
            // Try to get Name Metadata first
            var nameMeta = blob.GetMetadataByTag<ForzaTools.Bundles.Metadata.NameMetadata>(BundleMetadata.TAG_METADATA_Name);
            string metaName = nameMeta?.Name;

            // Base type name
            string typeName = blob.GetType().Name.Replace("Blob", "");

            StringBuilder sb = new StringBuilder();
            sb.Append($"[{index}] {typeName}");

            if (!string.IsNullOrEmpty(metaName))
            {
                sb.Append($" : {metaName}");
            }

            // Add specific details based on Blob Type
            if (blob is MeshBlob mesh)
            {
                sb.Append($" (LODs: {mesh.LODLevel1}-{mesh.LODLevel2}, Vtx: {mesh.VertexBuffers.Count})");
            }
            else if (blob is TextureContentBlob tex)
            {
                sb.Append($" ({tex.GetContents()?.Length ?? 0} bytes)");
            }

            // --- ADDED: Version Number ---
            sb.Append($" [v{blob.VersionMajor}.{blob.VersionMinor}]");

            return sb.ToString();
        }

        private string GetMetadataName(BundleMetadata meta)
        {
            try
            {
                // Convert the uint Tag to a 4-character string (e.g. 0x4E616D65 -> "Name")
                byte[] bytes = BitConverter.GetBytes(meta.Tag);
                if (BitConverter.IsLittleEndian) Array.Reverse(bytes);

                string tagStr = Encoding.ASCII.GetString(bytes).Trim('\0', ' ');

                // If the tag contains non-printable characters, show Hex instead
                if (tagStr.Any(c => c < 32 || c > 126))
                    return $"Metadata: 0x{meta.Tag:X8}";

                return $"Metadata: {tagStr}";
            }
            catch
            {
                return $"Metadata: 0x{meta.Tag:X8}";
            }
        }

        private void TreeView_AfterSelect(object sender, TreeViewEventArgs e)
        {
            object obj = e.Node.Tag;
            propertyGrid.SelectedObject = obj;

            // Update Hex View
            byte[] data = null;
            long offset = 0;

            if (obj is BundleBlob blob)
            {
                data = blob.GetContents();
                offset = blob.FileOffset;
            }
            else if (obj is BundleMetadata meta)
            {
                data = meta.GetContents();
                offset = meta.FileOffset;
            }

            // Push data to the hex control created in step 1
            if (embeddedHexView != null)
            {
                if (data != null)
                {
                    embeddedHexView.Data = data;
                    embeddedHexView.StartOffset = offset;
                    embeddedHexView.ReadOnly = true; // Safer for browse mode
                }
                else
                {
                    embeddedHexView.Data = new byte[0]; // Clear if no data
                }
            }
        }
        private void TreeView_NodeMouseClick(object sender, TreeNodeMouseClickEventArgs e)
        {
            if (e.Button == MouseButtons.Right) treeView.SelectedNode = e.Node;
        }

        // --- HEX VIEWERS (Unchanged) ---
        private void ViewHex_Click(object sender, EventArgs e)
        {
            if (treeView.SelectedNode?.Tag == null) return;
            object obj = treeView.SelectedNode.Tag;
            byte[] data = null;
            long offset = 0;

            if (obj is BundleBlob blob) { data = blob.GetContents(); offset = blob.FileOffset; }
            else if (obj is BundleMetadata meta) { data = meta.GetContents(); offset = meta.FileOffset; }

            if (data == null || data.Length == 0) { MessageBox.Show("No data."); return; }

            var hex = new HexEditorForm(data, offset);
            if (hex.ShowDialog() == DialogResult.OK)
            {
                var field = obj.GetType().GetField("_data", BindingFlags.NonPublic | BindingFlags.Instance)
                            ?? obj.GetType().BaseType.GetField("_data", BindingFlags.NonPublic | BindingFlags.Instance);
                if (field != null) field.SetValue(obj, hex.ModifiedData);

                var pubProp = obj.GetType().GetProperty("Data");
                if (pubProp != null && pubProp.PropertyType == typeof(byte[])) pubProp.SetValue(obj, hex.ModifiedData);

                if (obj is BundleBlob targetBlob)
                {
                    using (var ms = new MemoryStream(hex.ModifiedData))
                    using (var bs = new BinaryStream(ms))
                    {
                        if (targetBlob is MeshBlob mb) mb.VertexBuffers.Clear();
                        targetBlob.ReadBlobData(bs);
                    }
                    propertyGrid.Refresh();
                }
                else if (obj is BundleMetadata targetMeta)
                {
                    using (var ms = new MemoryStream(hex.ModifiedData))
                    using (var bs = new BinaryStream(ms))
                    {
                        targetMeta.ReadMetadataData(bs);
                    }
                    propertyGrid.Refresh();
                }
                MessageBox.Show("Updated.");
            }
        }

        private void PropertyGridContextMenu_Opening(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (propertyGrid.SelectedGridItem?.GridItemType != GridItemType.Property) e.Cancel = true;
        }

        private void ViewPropertyHex_Click(object sender, EventArgs e)
        {
            GridItem item = propertyGrid.SelectedGridItem;
            if (item == null || item.Value == null) return;
            object val = item.Value;
            byte[] data = ObjectToBytes(val);
            if (data == null) { MessageBox.Show("Type not supported."); return; }

            var hex = new HexEditorForm(data, 0);
            if (hex.ShowDialog() == DialogResult.OK)
            {
                try
                {
                    object newVal = BytesToObject(hex.ModifiedData, val.GetType());
                    if (newVal != null)
                    {
                        item.PropertyDescriptor.SetValue(propertyGrid.SelectedObject, newVal);
                        propertyGrid.Refresh();
                        MessageBox.Show("Updated.");
                    }
                }
                catch { MessageBox.Show("Error converting value."); }
            }
        }

        private byte[] ObjectToBytes(object obj)
        {
            if (obj is byte[] b) return b;
            if (obj is int i) return BitConverter.GetBytes(i);
            if (obj is uint ui) return BitConverter.GetBytes(ui);
            if (obj is long l) return BitConverter.GetBytes(l);
            if (obj is ulong ul) return BitConverter.GetBytes(ul);
            if (obj is short s) return BitConverter.GetBytes(s);
            if (obj is ushort us) return BitConverter.GetBytes(us);
            if (obj is float f) return BitConverter.GetBytes(f);
            if (obj is double d) return BitConverter.GetBytes(d);
            if (obj is bool bo) return BitConverter.GetBytes(bo);
            if (obj is string str) return Encoding.UTF8.GetBytes(str);
            return null;
        }

        private object BytesToObject(byte[] bytes, Type type)
        {
            if (type == typeof(byte[])) return bytes;
            if (type == typeof(int)) return BitConverter.ToInt32(bytes, 0);
            if (type == typeof(uint)) return BitConverter.ToUInt32(bytes, 0);
            if (type == typeof(long)) return BitConverter.ToInt64(bytes, 0);
            if (type == typeof(ulong)) return BitConverter.ToUInt64(bytes, 0);
            if (type == typeof(short)) return BitConverter.ToInt16(bytes, 0);
            if (type == typeof(ushort)) return BitConverter.ToUInt16(bytes, 0);
            if (type == typeof(float)) return BitConverter.ToSingle(bytes, 0);
            if (type == typeof(double)) return BitConverter.ToDouble(bytes, 0);
            if (type == typeof(bool)) return BitConverter.ToBoolean(bytes, 0);
            if (type == typeof(string)) return Encoding.UTF8.GetString(bytes);
            return null;
        }

        private void SaveFile_Click(object sender, EventArgs e)
        {
            if (currentBundle == null) return;
            using (SaveFileDialog sfd = new SaveFileDialog())
            {
                sfd.Filter = "ModelBin|*.modelbin|All|*.*";
                if (!string.IsNullOrEmpty(currentFilePath)) sfd.FileName = Path.GetFileName(currentFilePath);
                else sfd.FileName = "file.modelbin";

                if (sfd.ShowDialog() == DialogResult.OK)
                {
                    try
                    {
                        using (var fs = new FileStream(sfd.FileName, FileMode.Create))
                            currentBundle.Serialize(fs);
                        MessageBox.Show("Saved successfully.");
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Error saving: {ex.Message}");
                    }
                }
            }
        }
    }
}