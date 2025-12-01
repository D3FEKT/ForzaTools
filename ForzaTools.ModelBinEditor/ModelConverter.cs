using System;
using System.Linq;
using System.Collections.Generic;
using ForzaTools.Bundles;
using ForzaTools.Bundles.Blobs;
using ForzaTools.Bundles.Metadata; // <--- Added this to fix IdentifierMetadata error
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
            // We must insert the new data into the SAME buffer that holds the existing Tangents.
            var lastTangent = layout.Elements.LastOrDefault(e => layout.SemanticNames[e.SemanticNameIndex] == "TANGENT");
            if (lastTangent == null) throw new Exception("Error: No existing TANGENT found to clone.");

            int targetInputSlot = lastTangent.InputSlot;

            // Find the VertexBuffer usage for this slot
            var vbUsage = meshBlob.VertexBuffers.FirstOrDefault(vb => vb.InputSlot == targetInputSlot);
            if (vbUsage == null) throw new Exception($"Error: Mesh does not use InputSlot {targetInputSlot}.");

            // Find the actual Blob using the ID from usage
            VertexBufferBlob targetBuffer = FindBufferById(bundle, vbUsage.Index);
            if (targetBuffer == null) throw new Exception($"Error: Could not find VertexBufferBlob with ID {vbUsage.Index}.");

            // 4. Calculate Offset WITHIN the specific slot
            int insertOffset = 0;
            int insertIndex = -1;

            // Iterate layout to find insertion index and calculate offset
            for (int i = 0; i < layout.Elements.Count; i++)
            {
                var el = layout.Elements[i];

                // We insert after the last tangent
                if (el == lastTangent)
                    insertIndex = i + 1;

                // Only sum size if it belongs to the SAME buffer (InputSlot)
                if (el.InputSlot == targetInputSlot)
                {
                    // If we haven't passed the insertion point yet, add to offset
                    if (insertIndex == -1 || i < insertIndex)
                    {
                        insertOffset += GetSizeOfElementFormat(layout.PackedFormats[i]);

                        // Handle alignment padding (only for elements in this slot)
                        // Note: This simple check assumes elements in layout are grouped by slot or strictly ordered.
                        if (i + 1 < layout.Elements.Count && layout.Elements[i + 1].InputSlot == targetInputSlot)
                        {
                            if (insertOffset % 4 != 0 && GetSizeOfElementFormat(layout.PackedFormats[i + 1]) >= 4)
                                insertOffset += (insertOffset % 4);
                        }
                    }
                }
            }

            if (insertIndex == -1) insertIndex = layout.Elements.Count; // Append if last

            // 5. Create New Element
            var newElement = new D3D12_INPUT_LAYOUT_DESC()
            {
                SemanticNameIndex = (short)layout.SemanticNames.IndexOf("TANGENT"),
                SemanticIndex = 2,
                Format = DXGI_FORMAT.DXGI_FORMAT_R10G10B10A2_UNORM, // FH5 Format
                InputSlot = (short)targetInputSlot,
                AlignedByteOffset = -1,
                InstanceDataStepRate = 0,
                InputSlotClass = 0 // Per Vertex
            };

            // 6. Apply Changes to Layout
            layout.Elements.Insert(insertIndex, newElement);
            layout.PackedFormats.Insert(insertIndex, DXGI_FORMAT.DXGI_FORMAT_R8G8_TYPELESS); // Packed format matches FH5 structure
            layout.Flags |= 0x80; // FH5 Requirement

            // 7. Apply Changes to Buffer Data
            if (targetBuffer.Header.Data == null) throw new Exception("Buffer has no data.");

            // Verify offset isn't crazy
            if (insertOffset > targetBuffer.Header.Stride)
                throw new Exception($"Calc Error: Offset {insertOffset} > Stride {targetBuffer.Header.Stride}");

            for (int i = 0; i < targetBuffer.Header.Data.Length; i++)
            {
                var row = new List<byte>(targetBuffer.Header.Data[i]);

                // Safety check for this specific row
                if (insertOffset > row.Count)
                    throw new Exception($"Data Error: Row {i} length {row.Count} is smaller than offset {insertOffset}");

                // Insert 4 bytes (0xFF placeholder)
                row.InsertRange(insertOffset, new byte[] { 0xFF, 0xFF, 0xFF, 0xFF });
                targetBuffer.Header.Data[i] = row.ToArray();
            }

            // 8. Update Buffer Header Stride
            // Recalculate stride only for this slot
            byte newStride = 0;
            for (int i = 0; i < layout.Elements.Count; i++)
            {
                if (layout.Elements[i].InputSlot == targetInputSlot)
                {
                    newStride += GetSizeOfElementFormat(layout.PackedFormats[i]);
                    // Alignment padding logic... simplified here as we just added 4 bytes to an aligned structure
                }
            }
            // Fallback: simpler stride update if calculation fails or is complex
            if (newStride == 0) targetBuffer.Header.Stride += 4;
            else targetBuffer.Header.Stride = (ushort)newStride;

            // Update Element Count
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
                        // IdentifierMetadata stores ID as uint or int in first 4 bytes
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