using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Windows.Forms;
using System.Drawing;
using System.Reflection;
using System.Linq;
using System.Threading.Tasks;
using ForzaTools.Bundles;
using ForzaTools.Bundles.Blobs;
using ForzaTools.Shared;
using Syroot.BinaryData;

namespace ForzaTools.ModelBinEditor
{
    public partial class MainForm : Form
    {
        // --- Fields ---
        private Bundle currentBundle;
        private ForzaTools.CarScene.CarbinFile currentCarbin; // Added missing field for Carbin support
        private string currentFilePath;
        private ContextMenuStrip propertyGridContextMenu;
        private ToolStripMenuItem viewPropertyHexMenuItem;

        // Enhanced UI Controls
        private System.Windows.Forms.ToolStrip toolStrip;
        private System.Windows.Forms.ToolStripTextBox searchTextBox;
        private System.Windows.Forms.ToolStripButton searchButton;
        private System.Windows.Forms.ToolStripButton groupByTagButton;
        private System.Windows.Forms.TabControl rightTabControl;
        private System.Windows.Forms.TabPage propertyPage;
        private System.Windows.Forms.TabPage hexPage;
        private ForzaTools.ModelBinEditor.HexViewControl embeddedHexView;

        // Console Controls
        private RichTextBox consoleBox;
        private Panel consolePanel;

        // Game Profile Class
        private class GameProfile
        {
            public string Name { get; set; }
            public override string ToString() => Name;
        }

        // --- Constructor ---
        public MainForm()
        {
            InitializeComponent();

            // --- UI SIZE ADJUSTMENTS ---
            // Set larger default size (Double width ~2000, Taller ~1000)
            this.Size = new Size(1900, 1000);
            this.StartPosition = FormStartPosition.CenterScreen;
            // ---------------------------

            InitializePropertyGridContextMenu();
            InitializeTargetVersionDropdown();
            InitializeEnhancedUI();     // Build tabs, toolbar, hex view
            InitializeConsoleWindow();  // Build bottom console
        }

        // --- Initialization ---

        private void InitializeTargetVersionDropdown()
        {
            var games = new List<GameProfile>
            {
                new GameProfile { Name = "Forza Horizon 5 / Motorsport 2023" },
                new GameProfile { Name = "Forza Horizon 4" },
                new GameProfile { Name = "Forza Motorsport 7" },
                new GameProfile { Name = "Forza 6 Apex" },
                new GameProfile { Name = "Forza Horizon 3" },
                new GameProfile { Name = "Forza Horizon 2" },
                new GameProfile { Name = "Forza Motorsport 6" },
                new GameProfile { Name = "Forza Motorsport 5" }
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

        private void InitializeEnhancedUI()
        {
            // 1. ToolStrip Setup
            this.toolStrip = new System.Windows.Forms.ToolStrip();
            this.searchTextBox = new System.Windows.Forms.ToolStripTextBox();
            this.searchButton = new System.Windows.Forms.ToolStripButton();
            this.groupByTagButton = new System.Windows.Forms.ToolStripButton();

            this.toolStrip.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
                new System.Windows.Forms.ToolStripLabel("Search:"),
                this.searchTextBox,
                this.searchButton,
                new System.Windows.Forms.ToolStripSeparator(),
                this.groupByTagButton
            });

            this.searchTextBox.Size = new System.Drawing.Size(150, 23);
            this.searchButton.Text = "Find";
            this.searchButton.Click += (s, e) => PopulateTree();
            this.searchTextBox.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) PopulateTree(); };

            this.groupByTagButton.Text = "Group by Type";
            this.groupByTagButton.CheckOnClick = true;
            this.groupByTagButton.Checked = true;
            this.groupByTagButton.Click += (s, e) => PopulateTree();

            // 2. Adjust Layout and Add Controls
            if (this.splitContainer != null)
            {
                this.splitContainer.Panel1.Controls.Add(this.toolStrip);
                this.treeView.Top = 25;
                this.treeView.Height -= 25;

                // 3. Tab Control Setup
                this.rightTabControl = new System.Windows.Forms.TabControl();
                this.propertyPage = new System.Windows.Forms.TabPage("Properties");
                this.hexPage = new System.Windows.Forms.TabPage("Hex View");

                this.rightTabControl.Dock = DockStyle.Fill;
                this.rightTabControl.Controls.Add(this.propertyPage);
                this.rightTabControl.Controls.Add(this.hexPage);

                // Move PropertyGrid
                this.propertyGrid.Parent = this.propertyPage;
                this.propertyGrid.Dock = DockStyle.Fill;

                // Add Embedded Hex View
                this.embeddedHexView = new HexViewControl();
                this.embeddedHexView.Dock = DockStyle.Fill;
                this.hexPage.Controls.Add(this.embeddedHexView);

                this.splitContainer.Panel2.Controls.Clear();
                this.splitContainer.Panel2.Controls.Add(this.rightTabControl);
            }
        }

        private void InitializeConsoleWindow()
        {
            consolePanel = new Panel();
            // --- CONSOLE SIZE ADJUSTMENT ---
            consolePanel.Height = 300; // Increased from 150 to 300
            // -------------------------------
            consolePanel.Dock = DockStyle.Bottom;
            consolePanel.Padding = new Padding(5);
            consolePanel.BackColor = SystemColors.ControlDark;

            consoleBox = new RichTextBox();
            consoleBox.Dock = DockStyle.Fill;
            consoleBox.BackColor = Color.FromArgb(30, 30, 30);
            consoleBox.ForeColor = Color.LightGray;
            consoleBox.Font = new Font("Consolas", 10f); // Slightly larger font for readability
            consoleBox.ReadOnly = true;
            consoleBox.WordWrap = false;
            consoleBox.ScrollBars = RichTextBoxScrollBars.Vertical;

            consolePanel.Controls.Add(consoleBox);
            this.Controls.Add(consolePanel);

            // Ensure Correct Z-Order
            this.Controls.SetChildIndex(this.splitContainer, 0);
            this.Controls.SetChildIndex(this.consolePanel, 1);
            if (this.bottomPanel != null)
                this.Controls.SetChildIndex(this.bottomPanel, 2);
        }

        private void LogToConsole(string message, Color? color = null)
        {
            if (consoleBox.InvokeRequired)
            {
                consoleBox.Invoke(new Action(() => LogToConsole(message, color)));
                return;
            }

            consoleBox.SelectionStart = consoleBox.TextLength;
            consoleBox.SelectionLength = 0;
            consoleBox.SelectionColor = color ?? Color.LightGray;
            consoleBox.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}\r\n");
            consoleBox.ScrollToCaret();
        }

        // --- File Operations ---

        private void OpenFile_Click(object sender, EventArgs e)
        {
            using (OpenFileDialog ofd = new OpenFileDialog())
            {
                ofd.Filter = "All Supported Files|*.modelbin;*.swatchbin;*.carbin|ModelBin|*.modelbin|CarScene|*.carbin|All files (*.*)|*.*";
                if (ofd.ShowDialog() == DialogResult.OK) LoadFile(ofd.FileName);
            }
        }

        private void LoadFile(string path)
        {
            try
            {
                currentFilePath = path;
                string ext = Path.GetExtension(path).ToLower();

                if (ext == ".carbin")
                {
                    using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read))
                    {
                        currentCarbin = new ForzaTools.CarScene.CarbinFile();
                        currentCarbin.Load(fs);
                    }
                    currentBundle = null; // Clear modelbin data
                    PopulateCarbinTree();
                    this.Text = $"Forza ModelBin Editor - {Path.GetFileName(path)} (Scene v{currentCarbin.Scene.Version})";
                    LogToConsole($"Loaded Scene {path}", Color.LimeGreen);
                }
                else
                {
                    var newBundle = new Bundle();
                    using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read))
                    {
                        newBundle.Load(fs);
                    }
                    currentBundle = newBundle;
                    currentCarbin = null;
                    PopulateTree();
                    this.Text = $"Forza ModelBin Editor - {Path.GetFileName(path)}";
                    LogToConsole($"Loaded Bundle {path}", Color.LimeGreen);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading file: {ex.Message}", "Load Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                LogToConsole($"Error loading file: {ex.Message}", Color.Red);
            }
        }

        private void SaveFile_Click(object sender, EventArgs e)
        {
            if (currentBundle == null && currentCarbin == null) return;

            using (SaveFileDialog sfd = new SaveFileDialog())
            {
                sfd.Filter = "ModelBin|*.modelbin|CarScene|*.carbin|All|*.*";
                if (!string.IsNullOrEmpty(currentFilePath)) sfd.FileName = Path.GetFileName(currentFilePath);
                else sfd.FileName = "file.modelbin";

                if (sfd.ShowDialog() == DialogResult.OK)
                {
                    try
                    {
                        using (var fs = new FileStream(sfd.FileName, FileMode.Create))
                        {
                            if (currentCarbin != null)
                                currentCarbin.Save(fs);
                            else if (currentBundle != null)
                                currentBundle.Serialize(fs);
                        }

                        MessageBox.Show("Saved successfully.");
                        LogToConsole($"Saved to {sfd.FileName}", Color.LimeGreen);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Error saving: {ex.Message}");
                        LogToConsole($"Error saving: {ex.Message}", Color.Red);
                    }
                }
            }
        }

        private void Exit_Click(object sender, EventArgs e) => Close();

        // --- Tree View Logic (ModelBin) ---

        private void PopulateTree()
        {
            // Safety check for UI initialization
            if (searchTextBox == null || groupByTagButton == null) return;

            treeView.BeginUpdate();
            try
            {
                treeView.Nodes.Clear();
                propertyGrid.SelectedObject = null;

                if (currentBundle == null) return;

                TreeNode root = new TreeNode($"Bundle ({currentBundle.Blobs.Count} items)");
                root.Tag = currentBundle;
                treeView.Nodes.Add(root);

                string filter = searchTextBox.Text.ToLower();
                bool useGroups = groupByTagButton.Checked;
                Dictionary<string, TreeNode> groups = new Dictionary<string, TreeNode>();

                for (int i = 0; i < currentBundle.Blobs.Count; i++)
                {
                    var blob = currentBundle.Blobs[i];
                    string blobName = GetBlobName(blob, i);

                    // Filter
                    if (!string.IsNullOrEmpty(filter) && !blobName.ToLower().Contains(filter))
                        continue;

                    TreeNode blobNode = new TreeNode(blobName);
                    blobNode.Tag = blob;

                    // 1. Metadata
                    TreeNode metaRoot = new TreeNode("Metadata");
                    foreach (var meta in blob.Metadatas)
                    {
                        TreeNode metaNode = new TreeNode(GetMetadataName(meta));
                        metaNode.Tag = meta;
                        metaRoot.Nodes.Add(metaNode);
                    }
                    if (metaRoot.Nodes.Count > 0) blobNode.Nodes.Add(metaRoot);

                    // 2. Special Blob Logic
                    if (blob is SkeletonBlob skel)
                    {
                        TreeNode bonesRoot = new TreeNode($"BonesList ({skel.Bones.Count} items)");
                        for (int b = 0; b < skel.Bones.Count; b++)
                        {
                            var bone = skel.Bones[b];
                            StringBuilder boneLabel = new StringBuilder();
                            boneLabel.Append($"[{b}] {bone.Name}");
                            if (bone.ParentId > -1) boneLabel.Append($" (Parent: {bone.ParentId})");
                            TreeNode boneNode = new TreeNode(boneLabel.ToString());
                            boneNode.Tag = bone;
                            bonesRoot.Nodes.Add(boneNode);
                        }
                        if (bonesRoot.Nodes.Count > 0) blobNode.Nodes.Add(bonesRoot);
                    }

                    if (blob is MaterialBlob matBlob && matBlob.Bundle != null)
                    {
                        TreeNode subBundleNode = new TreeNode($"Nested Bundle ({matBlob.Bundle.Blobs.Count} items)");
                        for (int j = 0; j < matBlob.Bundle.Blobs.Count; j++)
                        {
                            var subBlob = matBlob.Bundle.Blobs[j];
                            TreeNode subBlobNode = new TreeNode(GetBlobName(subBlob, j));
                            subBlobNode.Tag = subBlob;

                            TreeNode subMetaRoot = new TreeNode("Metadata");
                            foreach (var subMeta in subBlob.Metadatas)
                            {
                                TreeNode subMetaNode = new TreeNode(GetMetadataName(subMeta));
                                subMetaNode.Tag = subMeta;
                                subMetaRoot.Nodes.Add(subMetaNode);
                            }
                            if (subMetaRoot.Nodes.Count > 0) subBlobNode.Nodes.Add(subMetaRoot);
                            subBundleNode.Nodes.Add(subBlobNode);
                        }
                        blobNode.Nodes.Add(subBundleNode);
                    }

                    // 3. Add to Tree (Grouped or Flat)
                    if (useGroups)
                    {
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
                }

                root.Expand();
                if (!string.IsNullOrEmpty(filter)) root.ExpandAll();
            }
            finally
            {
                treeView.EndUpdate();
            }
        }

        // --- Tree View Logic (Carbin) ---

        private void PopulateCarbinTree()
        {
            treeView.BeginUpdate();
            treeView.Nodes.Clear();
            propertyGrid.SelectedObject = null;

            if (currentCarbin == null || currentCarbin.Scene == null)
            {
                treeView.EndUpdate();
                return;
            }

            var scene = currentCarbin.Scene;
            TreeNode root = new TreeNode($"Scene (v{scene.Version}) - {scene.Series}");
            root.Tag = scene;

            TreeNode infoNode = new TreeNode("Info");
            infoNode.Nodes.Add($"Ordinal: {scene.Ordinal}");
            infoNode.Nodes.Add($"MediaName: {scene.MediaName}");
            infoNode.Nodes.Add($"Skeleton: {scene.SkeletonPath}");
            infoNode.Nodes.Add($"Strict Build: {scene.BuildStrict}");
            infoNode.Nodes.Add($"Build GUID: {scene.BuildGuid}");
            root.Nodes.Add(infoNode);

            TreeNode nonUpgNode = new TreeNode($"Non-Upgradable Parts ({scene.NonUpgradableParts.Count})");
            foreach (var entry in scene.NonUpgradableParts)
            {
                TreeNode partNode = new TreeNode($"{entry.Type} (v{entry.Part.Version})");
                partNode.Tag = entry.Part;

                foreach (var model in entry.Part.Models)
                {
                    partNode.Nodes.Add(CreateCarRenderModelNode(model));
                }
                nonUpgNode.Nodes.Add(partNode);
            }
            root.Nodes.Add(nonUpgNode);

            TreeNode upgNode = new TreeNode($"Upgradable Parts ({scene.UpgradableParts.Count})");
            foreach (var upg in scene.UpgradableParts)
            {
                TreeNode partNode = new TreeNode($"{upg.Type} (v{upg.Version})");
                partNode.Tag = upg;

                TreeNode upgradesNode = new TreeNode($"Upgrades ({upg.Upgrades.Count})");
                foreach (var upgrade in upg.Upgrades)
                {
                    TreeNode uNode = new TreeNode($"Upgrade (ID: {upgrade.Id}) Level {upgrade.Level}");
                    uNode.Tag = upgrade;
                    foreach (var model in upgrade.Models)
                    {
                        uNode.Nodes.Add(CreateCarRenderModelNode(model));
                    }
                    upgradesNode.Nodes.Add(uNode);
                }
                partNode.Nodes.Add(upgradesNode);

                if (upg.SharedModels.Count > 0)
                {
                    TreeNode sharedNode = new TreeNode($"Shared Models ({upg.SharedModels.Count})");
                    foreach (var shared in upg.SharedModels)
                    {
                        TreeNode sNode = CreateCarRenderModelNode(shared.Model);
                        sNode.Text = $"Shared Model (Ids: {string.Join(",", shared.UpgradeIds)})";
                        sharedNode.Nodes.Add(sNode);
                    }
                    partNode.Nodes.Add(sharedNode);
                }
                upgNode.Nodes.Add(partNode);
            }
            root.Nodes.Add(upgNode);

            treeView.Nodes.Add(root);
            root.Expand();
            treeView.EndUpdate();
        }

        private TreeNode CreateCarRenderModelNode(ForzaTools.CarScene.CarRenderModel model)
        {
            TreeNode node = new TreeNode($"Model: {model.Path}");
            node.Tag = model;
            node.Nodes.Add($"Bone: {model.BoneName} ({model.BoneId})");
            node.Nodes.Add($"LODs: {model.LODDetails}");
            node.Nodes.Add($"DrawGroups: {model.DrawGroups}");

            if (model.MaterialOverrides.Count > 0)
                node.Nodes.Add(new TreeNode($"Material Overrides ({model.MaterialOverrides.Count})") { Tag = model.MaterialOverrides });

            if (model.AOMapInfos.Count > 0)
            {
                TreeNode aoNode = new TreeNode($"AO Maps ({model.AOMapInfos.Count})");
                foreach (var ao in model.AOMapInfos)
                    aoNode.Nodes.Add(new TreeNode($"{ao.Path} ({ao.PartType})") { Tag = ao });
                node.Nodes.Add(aoNode);
            }

            return node;
        }

        private string GetBlobName(BundleBlob blob, int index)
        {
            var nameMeta = blob.GetMetadataByTag<ForzaTools.Bundles.Metadata.NameMetadata>(BundleMetadata.TAG_METADATA_Name);
            string metaName = nameMeta?.Name;
            string typeName = blob.GetType().Name.Replace("Blob", "");

            StringBuilder sb = new StringBuilder();
            sb.Append($"[{index}] {typeName}");

            if (!string.IsNullOrEmpty(metaName))
            {
                sb.Append($" : {metaName}");
            }

            if (blob is MeshBlob mesh)
            {
                sb.Append($" (LODs: {mesh.LODLevel1}-{mesh.LODLevel2}, Vtx: {mesh.VertexBuffers.Count})");
            }
            else if (blob is TextureContentBlob tex)
            {
                sb.Append($" ({tex.GetContents()?.Length ?? 0} bytes)");
            }

            sb.Append($" [v{blob.VersionMajor}.{blob.VersionMinor}]");
            return sb.ToString();
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

        private void TreeView_AfterSelect(object sender, TreeViewEventArgs e)
        {
            object obj = e.Node.Tag;
            propertyGrid.SelectedObject = obj;

            // Update Hex View
            byte[] data = null;
            long offset = 0;

            if (obj is BundleBlob blob) { data = blob.GetContents(); offset = blob.FileOffset; }
            else if (obj is BundleMetadata meta) { data = meta.GetContents(); offset = meta.FileOffset; }

            if (embeddedHexView != null)
            {
                if (data != null)
                {
                    embeddedHexView.Data = data;
                    embeddedHexView.StartOffset = offset;
                    embeddedHexView.ReadOnly = true; // Browse mode
                }
                else
                {
                    embeddedHexView.Data = new byte[0];
                }
            }
        }

        private void TreeView_NodeMouseClick(object sender, TreeNodeMouseClickEventArgs e)
        {
            if (e.Button == MouseButtons.Right) treeView.SelectedNode = e.Node;
        }

        // --- Conversions ---

        private void ConvertButton_Click(object sender, EventArgs e)
        {
            if (currentBundle == null && currentCarbin == null)
            {
                MessageBox.Show("No file loaded to convert.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // --- CARBIN CONVERSION ---
            if (currentCarbin != null)
            {
                try
                {
                    LogToConsole("Starting .carbin conversion...", Color.White);
                    currentCarbin.ConvertToFH5();
                    PopulateCarbinTree();
                    this.Text = $"Forza ModelBin Editor - {Path.GetFileName(currentFilePath)} (Converted to FH5)";
                    LogToConsole("Carbin file converted to Forza Horizon 5 format.", Color.Cyan);
                    MessageBox.Show("Carbin conversion complete.\nPlease use 'Save As' to write the file to disk.", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    LogToConsole($"Carbin conversion failed: {ex.Message}", Color.Red);
                    MessageBox.Show($"Carbin conversion failed:\n{ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                return;
            }

            // --- MODELBIN CONVERSION ---
            if (currentBundle != null)
            {
                var selectedProfile = targetVersionComboBox.SelectedItem as GameProfile;
                if (selectedProfile == null || !selectedProfile.Name.Contains("Horizon 5"))
                {
                    if (MessageBox.Show("The conversion logic is specifically designed for Forza Horizon 5.\nContinue anyway?", "Version Mismatch", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
                        return;
                }

                try
                {
                    bool modified = ModelConverter.MakeFH5Compatible(currentBundle);

                    if (modified)
                    {
                        using (MemoryStream ms = new MemoryStream())
                        {
                            currentBundle.Serialize(ms);
                            ms.Position = 0;

                            var newBundle = new Bundle();
                            newBundle.Load(ms);
                            currentBundle = newBundle;
                            PopulateTree();
                        }
                        MessageBox.Show("Conversion to FH5 format completed successfully.", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        LogToConsole("ModelBin converted to FH5 format.", Color.Cyan);
                    }
                    else
                    {
                        MessageBox.Show("Model appears to already be compatible with FH5.", "No Changes Needed", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        LogToConsole("ModelBin conversion skipped (Already FH5 compatible).", Color.Yellow);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Conversion failed: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    LogToConsole($"ModelBin conversion failed: {ex.Message}", Color.Red);
                }
            }
        }

        private void BatchConvert_Click(object sender, EventArgs e)
        {
            using (OpenFileDialog ofd = new OpenFileDialog())
            {
                ofd.Filter = "Zip Archives (*.zip)|*.zip";
                ofd.Multiselect = true;
                ofd.Title = "Select ZIP files for Batch Conversion";

                if (ofd.ShowDialog() == DialogResult.OK)
                {
                    using (var verDialog = new BatchConversionDialog())
                    {
                        if (verDialog.ShowDialog() == DialogResult.OK)
                        {
                            var processor = new BatchArchiveProcessor();

                            consoleBox.Clear();
                            LogToConsole($"Starting batch conversion for {ofd.FileNames.Length} archive(s)...", Color.Cyan);

                            Task.Run(() =>
                            {
                                try
                                {
                                    processor.ProcessArchives(ofd.FileNames, (msg, type) =>
                                    {
                                        Color msgColor = Color.LightGray;
                                        if (type == "ERROR") msgColor = Color.Red;
                                        else if (type == "SUCCESS") msgColor = Color.LimeGreen;
                                        else if (type == "WARN") msgColor = Color.Yellow;
                                        else if (type == "INFO") msgColor = Color.White;

                                        LogToConsole(msg, msgColor);
                                    });
                                }
                                catch (Exception ex)
                                {
                                    LogToConsole($"CRITICAL ERROR: {ex.Message}", Color.Red);
                                }
                            });
                        }
                    }
                }
            }
        }

        // --- Hex View Popups ---

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
                UpdateObjectData(obj, hex.ModifiedData);
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

        // --- Helpers ---

        private void UpdateObjectData(object obj, byte[] newData)
        {
            var field = obj.GetType().GetField("_data", BindingFlags.NonPublic | BindingFlags.Instance)
                        ?? obj.GetType().BaseType.GetField("_data", BindingFlags.NonPublic | BindingFlags.Instance);
            if (field != null) field.SetValue(obj, newData);

            var pubProp = obj.GetType().GetProperty("Data");
            if (pubProp != null && pubProp.PropertyType == typeof(byte[])) pubProp.SetValue(obj, newData);

            // Re-read structure if applicable
            using (var ms = new MemoryStream(newData))
            using (var bs = new BinaryStream(ms))
            {
                if (obj is BundleBlob blob)
                {
                    if (blob is MeshBlob mb) mb.VertexBuffers.Clear();
                    blob.ReadBlobData(bs);
                }
                else if (obj is BundleMetadata meta)
                {
                    meta.ReadMetadataData(bs);
                }
            }
            propertyGrid.Refresh();
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
    }
}