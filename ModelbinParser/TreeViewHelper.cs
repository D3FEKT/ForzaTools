using System.Collections.Generic;
using System.Windows.Forms;
using ForzaTools.Bundles;
using ForzaTools.Bundles.Blobs;
using ForzaTools.Bundles.Metadata;

namespace ModelbinParser
{
    public static class TreeViewHelper
    {
        public static void PopulateTree(TreeView treeView, BundleParser parser)
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
    }
}
