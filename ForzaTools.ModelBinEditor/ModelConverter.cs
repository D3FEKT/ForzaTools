using System;
using System.Linq;
using System.Collections.Generic;
using ForzaTools.Bundles;
using ForzaTools.Bundles.Blobs;
using ForzaTools.Bundles.Metadata;
using ForzaTools.Shared;
using Syroot.BinaryData;

namespace ForzaTools.ModelBinEditor
{
    public static class ModelConverter
    {
        public static bool MakeFH5Compatible(Bundle bundle)
        {
            // 1. Get Mesh and Layout
            MeshBlob meshBlob = (MeshBlob)bundle.GetBlobByIndex(Bundle.TAG_BLOB_Mesh, 0);
            if (meshBlob == null) throw new Exception("Structure Error: No MeshBlob found.");

            VertexLayoutBlob layout = (VertexLayoutBlob)bundle.GetBlobByIndex(Bundle.TAG_BLOB_VertexLayout, meshBlob.VertexLayoutIndex);
            if (layout == null) throw new Exception("Structure Error: VertexLayoutBlob not found.");

            // 2. Check Compatibility
            // FH5 requires a 3rd Tangent (SemanticIndex 2). 
            if (layout.Elements.Any(e => layout.SemanticNames[e.SemanticNameIndex] == "TANGENT" && e.SemanticIndex == 2))
                return false; // Already Compatible

            // 3. Identify Target Buffer and Slot
            // We want to insert the new data into the same buffer that holds the existing Tangents.
            // We look for the LAST tangent to append after it.
            int insertionLayoutIndex = -1;
            int targetInputSlot = -1;

            for (int i = 0; i < layout.Elements.Count; i++)
            {
                var el = layout.Elements[i];
                if (layout.SemanticNames[el.SemanticNameIndex] == "TANGENT")
                {
                    targetInputSlot = el.InputSlot;
                    insertionLayoutIndex = i + 1; // Insert after this one
                }
            }

            if (targetInputSlot == -1)
                throw new Exception("Error: No existing TANGENT found to clone.");

            // Find the VertexBuffer usage for this slot
            var vbUsage = meshBlob.VertexBuffers.FirstOrDefault(vb => vb.InputSlot == targetInputSlot);
            if (vbUsage == null) throw new Exception($"Error: Mesh does not use InputSlot {targetInputSlot}.");

            // Find the actual Blob using the ID from usage
            VertexBufferBlob targetBuffer = FindBufferById(bundle, vbUsage.Index);
            if (targetBuffer == null) throw new Exception($"Error: Could not find VertexBufferBlob with ID {vbUsage.Index}.");

            // 4. Calculate Data Offset (Crucial Step)
            // We must calculate the offset relative ONLY to this specific buffer (InputSlot).
            int insertByteOffset = 0;

            // Sum size of all elements in this slot that come BEFORE our insertion point
            for (int i = 0; i < insertionLayoutIndex; i++)
            {
                var el = layout.Elements[i];
                if (el.InputSlot == targetInputSlot)
                {
                    insertByteOffset += GetSizeOfElementFormat(layout.PackedFormats[i]);
                }
            }

            // 5. Create New Element Definition
            var newElement = new D3D12_INPUT_LAYOUT_DESC()
            {
                SemanticNameIndex = (short)layout.SemanticNames.IndexOf("TANGENT"),
                SemanticIndex = 2,
                Format = DXGI_FORMAT.DXGI_FORMAT_R10G10B10A2_UNORM, // FH5 Format (4 bytes)
                InputSlot = (short)targetInputSlot,
                AlignedByteOffset = -1,
                InstanceDataStepRate = 0,
                InputSlotClass = 0 // Per Vertex
            };

            // 6. Apply Changes to Layout
            if (insertionLayoutIndex >= layout.Elements.Count)
            {
                layout.Elements.Add(newElement);
                layout.PackedFormats.Add(DXGI_FORMAT.DXGI_FORMAT_R8G8_TYPELESS);
            }
            else
            {
                layout.Elements.Insert(insertionLayoutIndex, newElement);
                layout.PackedFormats.Insert(insertionLayoutIndex, DXGI_FORMAT.DXGI_FORMAT_R8G8_TYPELESS);
            }
            layout.Flags |= 0x80; // FH5 Requirement

            // 7. Apply Changes to Buffer Data
            if (targetBuffer.Header.Data == null) throw new Exception("Buffer has no data.");

            // The data we are inserting is 4 bytes (R10G10B10A2 = 32 bits)
            byte[] placeholderData = new byte[] { 0xFF, 0xFF, 0xFF, 0xFF };

            for (int i = 0; i < targetBuffer.Header.Data.Length; i++)
            {
                var row = new List<byte>(targetBuffer.Header.Data[i]);

                // Safety: If offset equals count, we append. If less, we insert.
                // If greater, the calculated offset is wrong for this buffer.
                if (insertByteOffset > row.Count)
                {
                    // Fallback strategy: If calculation failed (e.g. padding mismatch), 
                    // assume we are appending to the end of the semantic block.
                    // This is risky but fixes "Data Mismatch" crashes on weird models.
                    insertByteOffset = row.Count;
                }

                row.InsertRange(insertByteOffset, placeholderData);
                targetBuffer.Header.Data[i] = row.ToArray();
            }

            // 8. Update Buffer Header Stride
            // Instead of recalculating, we just ADD the size of the new element (4 bytes).
            // This preserves existing padding/alignment which is critical for rendering.
            targetBuffer.Header.Stride += 4;

            // Ensure element count is correct
            targetBuffer.Header.NumElements = (byte)targetBuffer.Header.Data.Length;

            return true;
        }

        // --- Helpers ---

        private static VertexBufferBlob FindBufferById(Bundle bundle, int id)
        {
            foreach (var blob in bundle.Blobs)
            {
                if (blob is VertexBufferBlob vb)
                {
                    // Retrieve ID from Metadata
                    var idMeta = blob.GetMetadataByTag<IdentifierMetadata>(BundleMetadata.TAG_METADATA_Identifier);
                    if (idMeta != null)
                    {
                        if (idMeta.Id == id) return vb;
                    }
                }
            }
            return null;
        }

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
    }
}