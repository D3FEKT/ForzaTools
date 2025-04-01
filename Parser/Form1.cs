using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.IO;
using System.Numerics;
using ForzaTools.Bundles.Blobs;
using ForzaTools.Bundles.Metadata;
using ForzaTools.Bundles;
using ForzaTools.Shared;

namespace ModelbinParser
{
    public partial class Form1 : Form
    {
        private BundleParser parser;
        private byte[] fileData;
        private string currentFilePath;
        private ModelData modelDataBuffer;

        public Form1()
        {
            InitializeComponent();

            // Register event handlers
            treeView.NodeMouseDoubleClick += TreeView_NodeMouseDoubleClick;
            vectorListView.DoubleClick += VectorListView_DoubleClick;
            replaceScaleButton.Click += ReplaceAllVertexScale;
            replacePositionButton.Click += ReplaceAllVertexPosition;
            replaceModelButton.Click += ReplaceModelButton_Click;

            // Initialize data
            modelDataBuffer = new ModelData();
        }

        #region Event Handlers

        private void OpenMenuItem_Click(object sender, EventArgs e)
        {
            OpenFileDialog dlg = new OpenFileDialog();
            dlg.Filter = "Modelbin Files (*.modelbin)|*.modelbin|All Files (*.*)|*.*";
            if (dlg.ShowDialog() == DialogResult.OK)
            {
                currentFilePath = dlg.FileName;
                fileData = File.ReadAllBytes(dlg.FileName);
                parser = new BundleParser(dlg.FileName);
                parser.LoadFile();
                PopulateTreeView();
            }
        }

        private void SaveMenuItem_Click(object sender, EventArgs e)
        {
            if (parser == null || parser.BundleData == null)
            {
                MessageBox.Show("No file loaded.");
                return;
            }

            SaveFileDialog dlg = new SaveFileDialog();
            dlg.Filter = "Modelbin Files (*.modelbin)|*.modelbin|All Files (*.*)|*.*";
            dlg.FileName = Path.GetFileName(currentFilePath);

            if (dlg.ShowDialog() == DialogResult.OK)
            {
                parser.SaveFile(dlg.FileName);
                currentFilePath = dlg.FileName; // Update the current file path
                MessageBox.Show("File saved successfully.");
            }
        }

        private void TreeView_NodeMouseDoubleClick(object sender, TreeNodeMouseClickEventArgs e)
        {
            if (e.Node.Tag is BoundaryBoxMetadata bbox)
            {
                float[] bboxValues = ExtractBoundingBoxValues(bbox);
                string input = Prompt.ShowDialog("Edit Bounding Box", "Enter MinX, MinY, MinZ, MaxX, MaxY, MaxZ",
                    $"{bboxValues[0]},{bboxValues[1]},{bboxValues[2]},{bboxValues[3]},{bboxValues[4]},{bboxValues[5]}");

                if (!string.IsNullOrEmpty(input))
                {
                    var values = input.Split(',').Select(v => float.TryParse(v, out var result) ? result : (float?)null).ToArray();
                    if (values.Length == 6 && values.All(v => v.HasValue))
                    {
                        byte[] newBytes = values.SelectMany(v => BitConverter.GetBytes(v.Value)).ToArray();
                        Array.Copy(newBytes, 0, bbox.GetContents(), 0, newBytes.Length);
                        e.Node.Text = $"Bounding Box: {values[0]:F2}, {values[1]:F2}, {values[2]:F2} - {values[3]:F2}, {values[4]:F2}, {values[5]:F2}";
                    }
                }
            }
            else if (e.Node.Tag is NameMetadata nameMd)
            {
                string currentName = Encoding.UTF8.GetString(nameMd.GetContents()).TrimEnd('\0');
                string newName = Prompt.ShowDialog("Edit Name", "Edit the Name metadata:", currentName);
                if (!string.IsNullOrEmpty(newName))
                {
                    byte[] newBytes = Encoding.UTF8.GetBytes(newName.PadRight(nameMd.GetContents().Length, '\0'));
                    Array.Copy(newBytes, 0, nameMd.GetContents(), 0, newBytes.Length);
                    e.Node.Text = $"Name: {newName}";
                }
            }
        }

        private void ReplaceModelButton_Click(object sender, EventArgs e)
        {
            OpenFileDialog dlg = new OpenFileDialog();
            dlg.Filter = "Wavefront OBJ Files (*.obj)|*.obj|All Files (*.*)|*.*";
            if (dlg.ShowDialog() == DialogResult.OK)
            {
                string objFilePath = dlg.FileName;
                var modelData = ModelData.ParseObjFile(objFilePath);
                if (modelData.Vertices != null && modelData.Normals != null && modelData.UVs != null && modelData.Faces != null)
                {
                    modelDataBuffer.Vertices = modelData.Vertices;
                    modelDataBuffer.Normals = modelData.Normals;
                    modelDataBuffer.UVs = modelData.UVs;
                    modelDataBuffer.Faces = modelData.Faces;

                    if (ReplaceModelDataInModelbin())
                    {
                        MessageBox.Show("Model data replaced successfully in the modelbin.");
                    }
                    else
                    {
                        MessageBox.Show("Failed to replace model data in the modelbin. Please check if a modelbin file is loaded.");
                    }
                }
                else
                {
                    MessageBox.Show("Failed to parse the model data.");
                }
            }
        }

        private void VectorListView_DoubleClick(object sender, EventArgs e)
        {
            if (vectorListView.SelectedItems.Count == 0) return;

            ListViewItem selectedItem = vectorListView.SelectedItems[0];
            if (selectedItem.Tag is MeshBlob meshBlob)
            {
                string newScale = Prompt.ShowDialog("Edit VertexScale", "Enter new VertexScale (X, Y, Z, W):",
                    $"{meshBlob.VertexScale.X},{meshBlob.VertexScale.Y},{meshBlob.VertexScale.Z},{meshBlob.VertexScale.W}");

                string newPosition = Prompt.ShowDialog("Edit VertexPosition", "Enter new VertexPosition (X, Y, Z, W):",
                    $"{meshBlob.VertexPosition.X},{meshBlob.VertexPosition.Y},{meshBlob.VertexPosition.Z},{meshBlob.VertexPosition.W}");

                if (!string.IsNullOrEmpty(newScale))
                {
                    var values = newScale.Split(',').Select(float.Parse).ToArray();
                    meshBlob.VertexScale = new Vector4(values[0], values[1], values[2], values[3]);
                    selectedItem.SubItems[1].Text = $"{values[0]:F2}, {values[1]:F2}, {values[2]:F2}, {values[3]:F2}";
                }

                if (!string.IsNullOrEmpty(newPosition))
                {
                    var values = newPosition.Split(',').Select(float.Parse).ToArray();
                    meshBlob.VertexPosition = new Vector4(values[0], values[1], values[2], values[3]);
                    selectedItem.SubItems[2].Text = $"{values[0]:F2}, {values[1]:F2}, {values[2]:F2}, {values[3]:F2}";
                }
            }
        }

        private void ReplaceAllVertexScale(object sender, EventArgs e)
        {
            string newScale = Prompt.ShowDialog("Replace All VertexScale", "Enter new VertexScale (X, Y, Z, W):", "0,0,0,1");
            if (!string.IsNullOrEmpty(newScale))
            {
                var values = newScale.Split(',').Select(float.Parse).ToArray();
                if (values.Length == 4)
                {
                    foreach (var blob in parser.BundleData.Blobs)
                    {
                        if (blob is MeshBlob meshBlob)
                        {
                            meshBlob.VertexScale = new Vector4(values[0], values[1], values[2], values[3]);
                        }
                    }
                    PopulateVectorListView(); // Update list view
                }
            }
        }

        private void ReplaceAllVertexPosition(object sender, EventArgs e)
        {
            string newPosition = Prompt.ShowDialog("Replace All VertexPosition", "Enter new VertexPosition (X, Y, Z, W):", "0,0,0,1");
            if (!string.IsNullOrEmpty(newPosition))
            {
                var values = newPosition.Split(',').Select(float.Parse).ToArray();
                if (values.Length == 4)
                {
                    foreach (var blob in parser.BundleData.Blobs)
                    {
                        if (blob is MeshBlob meshBlob)
                        {
                            meshBlob.VertexPosition = new Vector4(values[0], values[1], values[2], values[3]);
                        }
                    }
                    PopulateVectorListView(); // Update list view
                }
            }
        }

        #endregion

        #region TreeView Methods

        private void PopulateTreeView()
        {
            treeView.Nodes.Clear();

            if (parser.BundleData == null) return;

            TreeNode root = new TreeNode($"Bundle: {parser.BundleData.Blobs.Count} Blobs");

            foreach (var blob in parser.GetBlobs())
            {
                string blobTagName = GetTagName(blob.Tag);
                string blobName = blobTagName; // Default to tag name

                // If MaterialBlob, extract and display metadata name
                if (blob is MaterialBlob material)
                {
                    var materialNameMetadata = material.GetMetadataByTag<NameMetadata>(BundleMetadata.TAG_METADATA_Name);
                    string materialName = materialNameMetadata != null
                        ? Encoding.UTF8.GetString(materialNameMetadata.GetContents()).TrimEnd('\0')
                        : "Unnamed Material";

                    blobName = $"Material: {materialName}";
                }

                // If MeshBlob, extract and display metadata name & match MaterialID
                if (blob is MeshBlob meshBlob)
                {
                    var nameMetadata = meshBlob.GetMetadataByTag<NameMetadata>(BundleMetadata.TAG_METADATA_Name);
                    string meshName = nameMetadata != null
                        ? Encoding.UTF8.GetString(nameMetadata.GetContents()).TrimEnd('\0')
                        : "Unnamed Mesh";

                    // Find the matching MaterialBlob based on `UnkV9_MaterialID`
                    string materialName = "No Material";
                    var matchingMaterial = parser.BundleData.Blobs
                        .OfType<MaterialBlob>()
                        .FirstOrDefault(mat =>
                        {
                            var materialIdentifier = mat.GetMetadataByTag<IdentifierMetadata>(BundleMetadata.TAG_METADATA_Identifier);
                            if (materialIdentifier != null)
                            {
                                int materialID = BitConverter.ToInt32(materialIdentifier.GetContents(), 0);
                                return materialID == meshBlob.UnkV9_MaterialID;
                            }
                            return false;
                        });

                    if (matchingMaterial != null)
                    {
                        var materialNameMetadata = matchingMaterial.GetMetadataByTag<NameMetadata>(BundleMetadata.TAG_METADATA_Name);
                        if (materialNameMetadata != null)
                        {
                            materialName = Encoding.UTF8.GetString(materialNameMetadata.GetContents()).TrimEnd('\0');
                        }
                    }

                    blobName = $"{meshName} (Material: {materialName})";
                }

                TreeNode blobNode = new TreeNode(blobName);

                // Display Metadata for All Blobs
                TreeNode metadataNode = new TreeNode("Metadata");
                foreach (var md in blob.Metadatas)
                {
                    if (md is BoundaryBoxMetadata bbox)
                    {
                        float[] bboxValues = ExtractBoundingBoxValues(bbox);
                        string bboxData = $"{bboxValues[0]:F2}, {bboxValues[1]:F2}, {bboxValues[2]:F2} - {bboxValues[3]:F2}, {bboxValues[4]:F2}, {bboxValues[5]:F2}";
                        TreeNode bboxNode = new TreeNode($"Bounding Box: {bboxData}") { Tag = bbox };
                        metadataNode.Nodes.Add(bboxNode);
                    }
                    else if (md is IdentifierMetadata identifier)
                    {
                        int identifierValue = BitConverter.ToInt32(identifier.GetContents(), 0);
                        TreeNode idNode = new TreeNode($"Identifier: {identifierValue}") { Tag = identifier };
                        metadataNode.Nodes.Add(idNode);
                    }
                    else
                    {
                        metadataNode.Nodes.Add(new TreeNode($"{GetMetadataName(md.Tag)}: {Encoding.UTF8.GetString(md.GetContents())}") { Tag = md });
                    }
                }

                if (metadataNode.Nodes.Count > 0)
                    blobNode.Nodes.Add(metadataNode);

                // Display MeshBlob Data as Decimals
                if (blob is MeshBlob mesh)
                {
                    TreeNode meshDataNode = new TreeNode("Mesh Data");

                    meshDataNode.Nodes.Add(new TreeNode($"Material ID: {mesh.UnkV9_MaterialID}"));
                    meshDataNode.Nodes.Add(new TreeNode($"Face Start Index: {mesh.FaceStartIndex}"));
                    meshDataNode.Nodes.Add(new TreeNode($"Face Count: {mesh.FaceCount}"));
                    meshDataNode.Nodes.Add(new TreeNode($"Vertex Start Index: {mesh.VertexStartIndex}"));
                    meshDataNode.Nodes.Add(new TreeNode($"Num Verts: {mesh.NumVerts}"));
                    meshDataNode.Nodes.Add(new TreeNode($"Vertex Layout Index: {mesh.VertexLayoutIndex}"));

                    // Display all Vector4 properties and make them editable
                    TreeNode vector4Node = new TreeNode("Vector4 Properties");
                    vector4Node.Nodes.Add(CreateVectorNode("Vertex Scale", mesh.VertexScale));
                    vector4Node.Nodes.Add(CreateVectorNode("Vertex Position", mesh.VertexPosition));

                    if (vector4Node.Nodes.Count > 0)
                        meshDataNode.Nodes.Add(vector4Node);

                    blobNode.Nodes.Add(meshDataNode);
                }

                // Keep Buffer Headers for `VertexBufferBlob` & `IndexBufferBlob`
                if (blob is VertexBufferBlob vBuff)
                {
                    TreeNode bufferNode = new TreeNode("Vertex Buffer Header");
                    bufferNode.Nodes.Add(new TreeNode($"Buffer Width: {vBuff.Header.BufferWidth}"));
                    bufferNode.Nodes.Add(new TreeNode($"Num Elements: {vBuff.Header.NumElements}"));
                    bufferNode.Nodes.Add(new TreeNode($"Type: {vBuff.Header.Type}"));
                    blobNode.Nodes.Add(bufferNode);
                }
                else if (blob is IndexBufferBlob iBuff)
                {
                    TreeNode bufferNode = new TreeNode("Index Buffer Header");
                    bufferNode.Nodes.Add(new TreeNode($"Buffer Width: {iBuff.Header.BufferWidth}"));
                    bufferNode.Nodes.Add(new TreeNode($"Num Elements: {iBuff.Header.NumElements}"));
                    blobNode.Nodes.Add(bufferNode);
                }

                root.Nodes.Add(blobNode);
            }

            treeView.Nodes.Add(root);
            root.Expand();

            // Ensure the right-side ListView is updated
            PopulateVectorListView();
        }

        private TreeNode CreateVectorNode(string label, Vector4 vector)
        {
            return new TreeNode($"{label}: ({vector.X:F2}, {vector.Y:F2}, {vector.Z:F2}, {vector.W:F2})") { Tag = vector };
        }

        #endregion

        #region Model Replacement Logic

        private bool ReplaceModelDataInModelbin()
        {
            if (parser == null || parser.BundleData == null) return false;

            try
            {
                // Find the VertexLayoutBlob to determine which VertexBufferBlob contains which data
                VertexLayoutBlob vertexLayoutBlob = null;
                foreach (var blob in parser.BundleData.Blobs)
                {
                    if (blob.Tag == Bundle.TAG_BLOB_VertexLayout)
                    {
                        vertexLayoutBlob = (VertexLayoutBlob)blob;
                        break;
                    }
                }

                if (vertexLayoutBlob == null) return false;

                // Find positions, normals, and UVs in the vertex layout
                int positionIndex = -1;
                int normalIndex = -1;
                int uvIndex = -1;

                for (int i = 0; i < vertexLayoutBlob.SemanticNames.Count; i++)
                {
                    if (vertexLayoutBlob.SemanticNames[i] == "POSITION") positionIndex = i;
                    else if (vertexLayoutBlob.SemanticNames[i] == "NORMAL") normalIndex = i;
                    else if (vertexLayoutBlob.SemanticNames[i] == "TEXCOORD") uvIndex = i;
                }

                // Get vertex buffer blobs and index buffer blob
                List<VertexBufferBlob> vertexBufferBlobs = new List<VertexBufferBlob>();
                IndexBufferBlob indexBufferBlob = null;

                foreach (var blob in parser.BundleData.Blobs)
                {
                    if (blob.Tag == Bundle.TAG_BLOB_VertexBuffer)
                    {
                        vertexBufferBlobs.Add((VertexBufferBlob)blob);
                    }
                    else if (blob.Tag == Bundle.TAG_BLOB_IndexBuffer)
                    {
                        indexBufferBlob = (IndexBufferBlob)blob;
                    }
                }

                if (indexBufferBlob == null || vertexBufferBlobs.Count == 0) return false;

                // Replace index buffer data (faces)
                // Determine the correct buffer width
                ushort indexBufferWidth = indexBufferBlob.Header.BufferWidth;
                bool useUshort = indexBufferWidth == 2;

                int bytesPerIndex = useUshort ? sizeof(ushort) : sizeof(uint);
                byte[] indexData = new byte[modelDataBuffer.Faces.Count * 3 * bytesPerIndex];
                int indexOffset = 0;

                foreach (var face in modelDataBuffer.Faces)
                {
                    // Write the three indices of the face based on the detected format
                    if (useUshort)
                    {
                        byte[] index1 = BitConverter.GetBytes((ushort)face.Item1);
                        byte[] index2 = BitConverter.GetBytes((ushort)face.Item2);
                        byte[] index3 = BitConverter.GetBytes((ushort)face.Item3);

                        Buffer.BlockCopy(index1, 0, indexData, indexOffset, sizeof(ushort));
                        indexOffset += sizeof(ushort);
                        Buffer.BlockCopy(index2, 0, indexData, indexOffset, sizeof(ushort));
                        indexOffset += sizeof(ushort);
                        Buffer.BlockCopy(index3, 0, indexData, indexOffset, sizeof(ushort));
                        indexOffset += sizeof(ushort);
                    }
                    else
                    {
                        byte[] index1 = BitConverter.GetBytes((uint)face.Item1);
                        byte[] index2 = BitConverter.GetBytes((uint)face.Item2);
                        byte[] index3 = BitConverter.GetBytes((uint)face.Item3);

                        Buffer.BlockCopy(index1, 0, indexData, indexOffset, sizeof(uint));
                        indexOffset += sizeof(uint);
                        Buffer.BlockCopy(index2, 0, indexData, indexOffset, sizeof(uint));
                        indexOffset += sizeof(uint);
                        Buffer.BlockCopy(index3, 0, indexData, indexOffset, sizeof(uint));
                        indexOffset += sizeof(uint);
                    }
                }

                // Properly update the index buffer header
                indexBufferBlob.Header.NumElements = (byte)modelDataBuffer.Faces.Count;
                indexBufferBlob.Header.BufferWidth = (ushort)(3 * bytesPerIndex); // Each face has 3 indices
                indexBufferBlob.Header.Data = new byte[modelDataBuffer.Faces.Count][];

                for (int i = 0; i < modelDataBuffer.Faces.Count; i++)
                {
                    indexBufferBlob.Header.Data[i] = new byte[3 * bytesPerIndex];
                    Buffer.BlockCopy(indexData, i * 3 * bytesPerIndex, indexBufferBlob.Header.Data[i], 0, 3 * bytesPerIndex);
                }

                // Replace vertex buffer data
                if (vertexBufferBlobs.Count > 0 && positionIndex >= 0)
                {
                    // Determine the correct format for position data
                    ushort posBufferWidth = vertexBufferBlobs[0].Header.BufferWidth;
                    DXGI_FORMAT posFormat = vertexBufferBlobs[0].Header.Type;

                    // Prepare position data based on the format
                    byte[][] positionBufferData = new byte[modelDataBuffer.Vertices.Count][];

                    for (int i = 0; i < modelDataBuffer.Vertices.Count; i++)
                    {
                        var vertex = modelDataBuffer.Vertices[i];
                        positionBufferData[i] = new byte[posBufferWidth];

                        if (posFormat == DXGI_FORMAT.DXGI_FORMAT_R32G32B32_FLOAT ||
                            posFormat == DXGI_FORMAT.DXGI_FORMAT_R32G32B32A32_FLOAT)
                        {
                            // Float format
                            byte[] x = BitConverter.GetBytes(vertex.X);
                            byte[] y = BitConverter.GetBytes(vertex.Y);
                            byte[] z = BitConverter.GetBytes(vertex.Z);
                            byte[] w = BitConverter.GetBytes(1.0f); // Default W to 1

                            Buffer.BlockCopy(x, 0, positionBufferData[i], 0, sizeof(float));
                            Buffer.BlockCopy(y, 0, positionBufferData[i], sizeof(float), sizeof(float));
                            Buffer.BlockCopy(z, 0, positionBufferData[i], 2 * sizeof(float), sizeof(float));

                            if (posBufferWidth >= 16) // Has W component
                                Buffer.BlockCopy(w, 0, positionBufferData[i], 3 * sizeof(float), sizeof(float));
                        }
                        else
                        {
                            // Normalized short format (common in games)
                            short xShort = (short)(vertex.X * 32767);
                            short yShort = (short)(vertex.Y * 32767);
                            short zShort = (short)(vertex.Z * 32767);
                            short wShort = 32767; // 1.0 normalized

                            Buffer.BlockCopy(BitConverter.GetBytes(xShort), 0, positionBufferData[i], 0, sizeof(short));
                            Buffer.BlockCopy(BitConverter.GetBytes(yShort), 0, positionBufferData[i], sizeof(short), sizeof(short));
                            Buffer.BlockCopy(BitConverter.GetBytes(zShort), 0, positionBufferData[i], 2 * sizeof(short), sizeof(short));

                            if (posBufferWidth >= 8) // Has W component
                                Buffer.BlockCopy(BitConverter.GetBytes(wShort), 0, positionBufferData[i], 3 * sizeof(short), sizeof(short));
                        }
                    }

                    // Update vertex buffer header
                    vertexBufferBlobs[0].Header.NumElements = (byte)modelDataBuffer.Vertices.Count;
                    vertexBufferBlobs[0].Header.Data = positionBufferData;
                }

                // Replace normal data if present
                if (vertexBufferBlobs.Count > 1 && normalIndex >= 0 && modelDataBuffer.Normals.Count > 0)
                {
                    // Determine the correct format for normal data
                    ushort normBufferWidth = vertexBufferBlobs[1].Header.BufferWidth;
                    DXGI_FORMAT normFormat = vertexBufferBlobs[1].Header.Type;

                    // Prepare normal data
                    byte[][] normalBufferData = new byte[modelDataBuffer.Normals.Count][];

                    for (int i = 0; i < modelDataBuffer.Normals.Count; i++)
                    {
                        var normal = modelDataBuffer.Normals[i];
                        normalBufferData[i] = new byte[normBufferWidth];

                        if (normFormat == DXGI_FORMAT.DXGI_FORMAT_R32G32B32_FLOAT)
                        {
                            // Float format
                            byte[] x = BitConverter.GetBytes(normal.X);
                            byte[] y = BitConverter.GetBytes(normal.Y);
                            byte[] z = BitConverter.GetBytes(normal.Z);

                            Buffer.BlockCopy(x, 0, normalBufferData[i], 0, sizeof(float));
                            Buffer.BlockCopy(y, 0, normalBufferData[i], sizeof(float), sizeof(float));
                            Buffer.BlockCopy(z, 0, normalBufferData[i], 2 * sizeof(float), sizeof(float));
                        }
                        else
                        {
                            // Normalized short format
                            short xShort = (short)(normal.X * 32767);
                            short yShort = (short)(normal.Y * 32767);
                            short zShort = (short)(normal.Z * 32767);

                            Buffer.BlockCopy(BitConverter.GetBytes(xShort), 0, normalBufferData[i], 0, sizeof(short));
                            Buffer.BlockCopy(BitConverter.GetBytes(yShort), 0, normalBufferData[i], sizeof(short), sizeof(short));
                            Buffer.BlockCopy(BitConverter.GetBytes(zShort), 0, normalBufferData[i], 2 * sizeof(short), sizeof(short));
                        }
                    }

                    // Update normal buffer header
                    vertexBufferBlobs[1].Header.NumElements = (byte)modelDataBuffer.Normals.Count;
                    vertexBufferBlobs[1].Header.Data = normalBufferData;
                }

                // Replace UV data if present
                if (vertexBufferBlobs.Count > 2 && uvIndex >= 0 && modelDataBuffer.UVs.Count > 0)
                {
                    // Determine the correct format for UV data
                    ushort uvBufferWidth = vertexBufferBlobs[2].Header.BufferWidth;
                    DXGI_FORMAT uvFormat = vertexBufferBlobs[2].Header.Type;

                    // Prepare UV data
                    byte[][] uvBufferData = new byte[modelDataBuffer.UVs.Count][];

                    for (int i = 0; i < modelDataBuffer.UVs.Count; i++)
                    {
                        var uv = modelDataBuffer.UVs[i];
                        uvBufferData[i] = new byte[uvBufferWidth];

                        if (uvFormat == DXGI_FORMAT.DXGI_FORMAT_R32G32_FLOAT)
                        {
                            // Float format
                            byte[] u = BitConverter.GetBytes(uv.X);
                            byte[] v = BitConverter.GetBytes(uv.Y);

                            Buffer.BlockCopy(u, 0, uvBufferData[i], 0, sizeof(float));
                            Buffer.BlockCopy(v, 0, uvBufferData[i], sizeof(float), sizeof(float));
                        }
                        else
                        {
                            // Normalized short format
                            short uShort = (short)(uv.X * 32767);
                            short vShort = (short)(uv.Y * 32767);

                            Buffer.BlockCopy(BitConverter.GetBytes(uShort), 0, uvBufferData[i], 0, sizeof(short));
                            Buffer.BlockCopy(BitConverter.GetBytes(vShort), 0, uvBufferData[i], sizeof(short), sizeof(short));
                        }
                    }

                    // Update UV buffer header
                    vertexBufferBlobs[2].Header.NumElements = (byte)modelDataBuffer.UVs.Count;
                    vertexBufferBlobs[2].Header.Data = uvBufferData;
                }

                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error replacing model data: {ex.Message}");
                return false;
            }
        }

        #endregion

        #region ListView Methods

        private void PopulateVectorListView()
        {
            if (parser == null || parser.BundleData == null) return;

            vectorListView.Items.Clear();

            foreach (var blob in parser.BundleData.Blobs)
            {
                if (blob is MeshBlob meshBlob)
                {
                    var nameMetadata = meshBlob.GetMetadataByTag<NameMetadata>(BundleMetadata.TAG_METADATA_Name);
                    string meshName = nameMetadata != null
                        ? Encoding.UTF8.GetString(nameMetadata.GetContents()).TrimEnd('\0')
                        : "Unnamed Mesh";

                    string vertexScale = $"{meshBlob.VertexScale.X:F2}, {meshBlob.VertexScale.Y:F2}, {meshBlob.VertexScale.Z:F2}, {meshBlob.VertexScale.W:F2}";
                    string vertexPosition = $"{meshBlob.VertexPosition.X:F2}, {meshBlob.VertexPosition.Y:F2}, {meshBlob.VertexPosition.Z:F2}, {meshBlob.VertexPosition.W:F2}";

                    ListViewItem item = new ListViewItem(meshName);
                    item.SubItems.Add(vertexScale);
                    item.SubItems.Add(vertexPosition);
                    item.Tag = meshBlob;
                    vectorListView.Items.Add(item);
                }
            }
        }

        #endregion

        #region Helper Methods

        public float[] ExtractBoundingBoxValues(BoundaryBoxMetadata bbox)
        {
            byte[] bboxBytes = bbox.GetContents();
            if (bboxBytes.Length != 24) return new float[6];

            return new float[]
            {
                BitConverter.ToSingle(bboxBytes, 0),
                BitConverter.ToSingle(bboxBytes, 4),
                BitConverter.ToSingle(bboxBytes, 8),
                BitConverter.ToSingle(bboxBytes, 12),
                BitConverter.ToSingle(bboxBytes, 16),
                BitConverter.ToSingle(bboxBytes, 20)
            };
        }

        public string GetTagName(uint tag)
        {
            return tag switch
            {
                Bundle.TAG_BLOB_Skeleton => "Skeleton",
                Bundle.TAG_BLOB_Morph => "Morph",
                Bundle.TAG_BLOB_Material => "Material",
                Bundle.TAG_BLOB_Mesh => "Mesh",
                Bundle.TAG_BLOB_IndexBuffer => "IndexBuffer",
                Bundle.TAG_BLOB_VertexLayout => "VertexLayout",
                Bundle.TAG_BLOB_VertexBuffer => "VertexBuffer",
                Bundle.TAG_BLOB_MorphBuffer => "MorphBuffer",
                Bundle.TAG_BLOB_Skin => "Skin",
                Bundle.TAG_BLOB_Model => "Model",
                Bundle.TAG_BLOB_TextureContentBlob => "Texture",
                _ => $"Unknown ({tag:X8})"
            };
        }

        public string GetMetadataName(uint tag)
        {
            return tag switch
            {
                BundleMetadata.TAG_METADATA_Name => "Name",
                BundleMetadata.TAG_METADATA_TextureContentHeader => "Texture Header",
                BundleMetadata.TAG_METADATA_Identifier => "Identifier",
                BundleMetadata.TAG_METADATA_BBox => "Bounding Box",
                BundleMetadata.TAG_METADATA_Atlas => "Atlas",
                _ => $"Unknown Metadata ({tag:X8})"
            };
        }

        #endregion
    }
}
