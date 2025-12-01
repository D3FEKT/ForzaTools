using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Windows.Forms;
using System.Reflection;
using System.Linq;
using ForzaTools.Bundles;
using ForzaTools.Bundles.Blobs;
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

        // --- CONVERSION LOGIC (DISABLED FOR NOW) ---
        private void ConvertButton_Click(object sender, EventArgs e)
        {
            MessageBox.Show("Conversion functionality is currently disabled.", "Feature Disabled", MessageBoxButtons.OK, MessageBoxIcon.Information);
            /*
            if (currentBundle == null) return;
            if (!(targetVersionComboBox.SelectedItem is GameProfile targetProfile)) return;

            try
            {
                // ... (Previous logic kept commented out for future reference)
                // 1. Update Version Numbers
                // 2. Serialize to Memory
                // 3. Reload from Memory
                // 4. Update UI
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Conversion failed: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            */
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

                TreeNode root = new TreeNode("Bundle");
                root.Tag = currentBundle;
                treeView.Nodes.Add(root);

                if (currentBundle.Blobs.Count == 0)
                {
                    root.Nodes.Add(new TreeNode("No Blobs found (Check file version)"));
                }

                for (int i = 0; i < currentBundle.Blobs.Count; i++)
                {
                    var blob = currentBundle.Blobs[i];
                    TreeNode blobNode = new TreeNode(GetBlobName(blob, i));
                    blobNode.Tag = blob;

                    TreeNode metaRoot = new TreeNode("Metadata");
                    foreach (var meta in blob.Metadatas)
                    {
                        TreeNode metaNode = new TreeNode(GetMetadataName(meta));
                        metaNode.Tag = meta;
                        metaRoot.Nodes.Add(metaNode);
                    }
                    if (metaRoot.Nodes.Count > 0) blobNode.Nodes.Add(metaRoot);

                    if (blob is MaterialBlob matBlob && matBlob.Bundle != null)
                    {
                        blobNode.Nodes.Add(new TreeNode("Nested Bundle") { Tag = matBlob.Bundle });
                    }

                    root.Nodes.Add(blobNode);
                }
                root.Expand();
            }
            finally
            {
                treeView.EndUpdate();
            }
        }

        private string GetBlobName(BundleBlob blob, int index)
        {
            try
            {
                var nameMeta = blob.GetMetadataByTag<ForzaTools.Bundles.Metadata.NameMetadata>(BundleMetadata.TAG_METADATA_Name);
                string name = nameMeta?.Name ?? blob.GetType().Name.Replace("Blob", "");

                // Simplified name, removed detection info
                return $"[{index}] {name}";
            }
            catch { return $"[{index}] Blob"; }
        }

        private string GetMetadataName(BundleMetadata meta)
        {
            try
            {
                byte[] bytes = BitConverter.GetBytes(meta.Tag);
                if (BitConverter.IsLittleEndian) Array.Reverse(bytes);
                string tagStr = Encoding.ASCII.GetString(bytes).Trim('\0', ' ');
                if (tagStr.Any(c => c < 32 || c > 126)) return $"Metadata: 0x{meta.Tag:X8}";
                return $"Metadata: {tagStr}";
            }
            catch { return $"Metadata: 0x{meta.Tag:X8}"; }
        }

        private void TreeView_AfterSelect(object sender, TreeViewEventArgs e) => propertyGrid.SelectedObject = e.Node.Tag;
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