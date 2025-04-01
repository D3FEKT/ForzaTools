using System;
using System.IO;
using System.Text;
using System.Windows.Forms;
using ForzaTools.Bundles;
using ForzaTools.Bundles.Blobs;
using ForzaTools.Bundles.Metadata;

namespace ModelbinParser
{
    public partial class Form1 : Form
    {
        private TreeView treeView;
        private MenuStrip menuStrip;
        private ToolStripMenuItem fileMenu;
        private ToolStripMenuItem openMenuItem;
        private ToolStripMenuItem saveMenuItem;
        private BundleParser parser;
        private byte[] fileData;
        private string currentFilePath;

        public Form1()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            this.Text = "Forza Modelbin Parser";
            this.Width = 800;
            this.Height = 600;

            menuStrip = new MenuStrip();
            fileMenu = new ToolStripMenuItem("File");
            openMenuItem = new ToolStripMenuItem("Open");
            saveMenuItem = new ToolStripMenuItem("Save");

            openMenuItem.Click += OpenMenuItem_Click;
            saveMenuItem.Click += SaveMenuItem_Click;
            fileMenu.DropDownItems.Add(openMenuItem);
            fileMenu.DropDownItems.Add(saveMenuItem);
            menuStrip.Items.Add(fileMenu);
            this.MainMenuStrip = menuStrip;
            this.Controls.Add(menuStrip);

            treeView = new TreeView();
            treeView.Dock = DockStyle.Fill;
            treeView.NodeMouseDoubleClick += TreeView_NodeMouseDoubleClick;
            this.Controls.Add(treeView);
        }

        private void OpenMenuItem_Click(object sender, EventArgs e)
        {
            OpenFileDialog dlg = new OpenFileDialog();
            dlg.Filter = "Modelbin Files (*.modelbin)|*.modelbin|All Files (*.*)|*.*";
            if (dlg.ShowDialog() == DialogResult.OK)
            {
                currentFilePath = dlg.FileName;
                fileData = File.ReadAllBytes(currentFilePath);
                parser = new BundleParser(currentFilePath);
                parser.LoadFile();  // ✅ FIXED: Changed `Read()` to `LoadFile()`
                PopulateTreeView();
            }
        }

        private void SaveMenuItem_Click(object sender, EventArgs e)
        {
            if (fileData == null)
            {
                MessageBox.Show("No file loaded.");
                return;
            }
            SaveFileDialog dlg = new SaveFileDialog();
            dlg.Filter = "Modelbin Files (*.modelbin)|*.modelbin|All Files (*.*)|*.*";
            dlg.FileName = currentFilePath;
            if (dlg.ShowDialog() == DialogResult.OK)
            {
                parser.SaveFile(dlg.FileName);
                MessageBox.Show("File saved successfully.");
            }
        }

        private void PopulateTreeView()
        {
            treeView.Nodes.Clear();

            if (parser.BundleData == null) return;

            TreeNode root = new TreeNode($"Bundle: {parser.BundleData.Blobs.Count} Blobs"); // ✅ FIXED: Removed `Header`

            foreach (var blob in parser.GetBlobs())
            {
                TreeNode blobNode = new TreeNode(blob.ToString());

                if (blob is VertexLayoutBlob vLay)
                {
                    TreeNode vLayNode = new TreeNode($"Vertex Layout: {vLay.Elements.Count} Elements"); // ✅ FIXED: Used `Elements.Count`
                    foreach (var semantic in vLay.SemanticNames) // ✅ FIXED: Used `SemanticNames`
                    {
                        vLayNode.Nodes.Add(new TreeNode($"Semantic: {semantic}"));
                    }
                    vLayNode.Nodes.Add(new TreeNode($"Packed Formats: {string.Join(", ", vLay.PackedFormats)}"));
                    blobNode.Nodes.Add(vLayNode);
                }
                else if (blob is VertexBufferBlob vBuff)
                {
                    TreeNode vBuffNode = new TreeNode($"Vertex Buffer: Width={vBuff.Header.BufferWidth}"); // ✅ FIXED: Used `BufferWidth`
                    blobNode.Nodes.Add(vBuffNode);
                }
                else if (blob is IndexBufferBlob iBuff)
                {
                    TreeNode iBuffNode = new TreeNode($"Index Buffer: Width={iBuff.Header.BufferWidth}"); // ✅ FIXED: Used `BufferWidth`
                    blobNode.Nodes.Add(iBuffNode);
                }
                else
                {
                    foreach (var md in blob.Metadatas) // ✅ FIXED: Changed `MetaDatas` to `Metadatas`
                    {
                        TreeNode mdNode = new TreeNode(md.ToString());
                        mdNode.Tag = md;
                        blobNode.Nodes.Add(mdNode);
                    }
                }

                root.Nodes.Add(blobNode);
            }

            treeView.Nodes.Add(root);
            root.Expand();
        }

        private void TreeView_NodeMouseDoubleClick(object sender, TreeNodeMouseClickEventArgs e)
        {
            MetadataEditor.EditMetadata(e.Node, fileData);
        }
    }
}
