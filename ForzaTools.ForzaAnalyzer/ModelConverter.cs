using System;
using System.Linq;
using System.Collections.Generic;
using ForzaTools.Bundles;
using ForzaTools.Bundles.Blobs;
using ForzaTools.Bundles.Metadata;
using ForzaTools.Shared;
using Syroot.BinaryData;

namespace ForzaTools.ForzaAnalyzer
{
    public static class ModelConverter
    {
        public static bool MakeFH5Compatible(Bundle bundle, Action<string> log)
        {
            log?.Invoke("[ModelConverter] Starting FH5 Compatibility Check (Stride Fix)...");
            bool isModified = false;

            // --- A. MODEL BLOB CLEANUP ---
            ModelBlob modelBlob = (ModelBlob)bundle.GetBlobByIndex(Bundle.TAG_BLOB_Model, 0);
            if (modelBlob != null)
            {
                if (modelBlob.Metadatas.Count > 1)
                {
                    log?.Invoke($"[ModelConverter] Pruning ModelBlob Metadata...");
                    var bbox = modelBlob.Metadatas.OfType<BoundaryBoxMetadata>().FirstOrDefault();
                    modelBlob.Metadatas.Clear();
                    if (bbox != null) modelBlob.Metadatas.Add(bbox);
                    isModified = true;
                }
            }

            // --- B. MESH BLOB CONVERSION ---
            foreach (var blob in bundle.Blobs)
            {
                if (blob is MeshBlob mesh)
                {
                    if (mesh.VersionMajor == 1 && mesh.VersionMinor < 9)
                    {
                        mesh.MaterialIds = new short[] { -1, mesh.MaterialId, -1, -1 };
                        mesh.VersionMinor = 9;
                        isModified = true;
                    }
                }
            }

            // --- C. VERTEX LAYOUT UPDATES ---
            MeshBlob mainMesh = (MeshBlob)bundle.GetBlobByIndex(Bundle.TAG_BLOB_Mesh, 0);
            if (mainMesh == null)
            {
                log?.Invoke("[ModelConverter] No MeshBlobs found.");
                return isModified;
            }

            VertexLayoutBlob layout = (VertexLayoutBlob)bundle.GetBlobByIndex(Bundle.TAG_BLOB_VertexLayout, mainMesh.VertexLayoutIndex);
            if (layout == null) throw new Exception("Structure Error: VertexLayoutBlob not found.");

            // 1. Identify Target Buffer Slot
            int targetInputSlot = -1;
            foreach (var el in layout.Elements)
            {
                if (layout.SemanticNames[el.SemanticNameIndex] == "TANGENT")
                {
                    targetInputSlot = el.InputSlot;
                    break;
                }
            }
            if (targetInputSlot == -1) targetInputSlot = 0;

            bool layoutModified = false;

            // 2. Add Missing Tangent 3
            bool hasThirdTangent = layout.Elements.Any(e =>
                layout.SemanticNames[e.SemanticNameIndex] == "TANGENT" && e.SemanticIndex == 2);

            if (!hasThirdTangent)
            {
                log?.Invoke($"[ModelConverter] Adding Tangent 3...");

                int tangentNameIdx = GetSemanticIndex(layout, "TANGENT");

                // Find insertion index: after the last existing Tangent to keep grouping
                int lastTangentIndex = layout.Elements.FindLastIndex(e => e.SemanticNameIndex == tangentNameIdx);
                int insertionIndex = (lastTangentIndex != -1) ? lastTangentIndex + 1 : layout.Elements.Count;

                // Calculate Offset based on the PREVIOUS element's actual position
                // This prevents us from miscalculating if there were gaps/padding in the original file.
                int insertOffset = CalculateSafeOffset(layout, insertionIndex - 1, targetInputSlot);

                var newElement = new D3D12_INPUT_LAYOUT_DESC()
                {
                    SemanticNameIndex = (short)tangentNameIdx,
                    SemanticIndex = 2,
                    Format = DXGI_FORMAT.DXGI_FORMAT_R10G10B10A2_UNORM,
                    InputSlot = (short)targetInputSlot,
                    AlignedByteOffset = insertOffset, // Explicit offset helps debug
                    InstanceDataStepRate = 0,
                    InputSlotClass = 0
                };

                if (insertionIndex >= layout.Elements.Count)
                {
                    layout.Elements.Add(newElement);
                    layout.PackedFormats.Add(DXGI_FORMAT.DXGI_FORMAT_R8G8_TYPELESS);
                }
                else
                {
                    layout.Elements.Insert(insertionIndex, newElement);
                    layout.PackedFormats.Insert(insertionIndex, DXGI_FORMAT.DXGI_FORMAT_R8G8_TYPELESS);
                }
                layout.Flags |= 0x80;

                // Inject Data (White)
                InjectDataIntoBuffer(bundle, mainMesh, targetInputSlot, insertOffset, new byte[] { 0xFF, 0xFF, 0xFF, 0xFF }, log);
                layoutModified = true;
            }

            // 3. Add Missing Color
            bool hasColor = layout.Elements.Any(e =>
                layout.SemanticNames[e.SemanticNameIndex] == "COLOR" && e.SemanticIndex == 0);

            if (!hasColor)
            {
                log?.Invoke($"[ModelConverter] Adding COLOR...");

                int colorNameIdx = GetSemanticIndex(layout, "COLOR");

                // Append to end of slot elements
                int insertionIndex = layout.Elements.FindLastIndex(e => e.InputSlot == targetInputSlot) + 1;
                if (insertionIndex <= 0) insertionIndex = layout.Elements.Count;

                int insertOffset = CalculateSafeOffset(layout, insertionIndex - 1, targetInputSlot);

                var newElement = new D3D12_INPUT_LAYOUT_DESC()
                {
                    SemanticNameIndex = (short)colorNameIdx,
                    SemanticIndex = 0,
                    Format = DXGI_FORMAT.DXGI_FORMAT_R8G8B8A8_UNORM,
                    InputSlot = (short)targetInputSlot,
                    AlignedByteOffset = insertOffset,
                    InstanceDataStepRate = 0,
                    InputSlotClass = 0
                };

                if (insertionIndex >= layout.Elements.Count)
                {
                    layout.Elements.Add(newElement);
                    layout.PackedFormats.Add(DXGI_FORMAT.DXGI_FORMAT_R8G8B8A8_UNORM);
                }
                else
                {
                    layout.Elements.Insert(insertionIndex, newElement);
                    layout.PackedFormats.Insert(insertionIndex, DXGI_FORMAT.DXGI_FORMAT_R8G8B8A8_UNORM);
                }
                layout.Flags |= 0x80;

                InjectDataIntoBuffer(bundle, mainMesh, targetInputSlot, insertOffset, new byte[] { 255, 255, 255, 255 }, log);
                layoutModified = true;
            }

            if (layoutModified)
            {
                // Force reset AlignedByteOffset to -1 (Auto) for serialization to let the game recalc stride if it wants
                // OR keep explicit. FH5 usually prefers explicit or -1.
                // Let's set the new elements to -1 now that we used the offset for data injection.
                foreach (var el in layout.Elements)
                {
                    // el.AlignedByteOffset = -1; // Optional: Resetting might act cleaner for the engine
                }

                UpdateBufferStride(bundle, mainMesh, targetInputSlot);
                isModified = true;
            }

            return isModified;
        }

        // --- Helpers ---

        private static int CalculateSafeOffset(VertexLayoutBlob layout, int previousElementIndex, int targetSlot)
        {
            // If there is no previous element, start at 0
            if (previousElementIndex < 0 || previousElementIndex >= layout.Elements.Count) return 0;

            var prev = layout.Elements[previousElementIndex];

            // If the previous element is in a DIFFERENT slot, we can't use it for offset calc.
            // We must find the last element in the TARGET slot.
            if (prev.InputSlot != targetSlot)
            {
                // Search backwards for an element in the correct slot
                for (int i = previousElementIndex; i >= 0; i--)
                {
                    if (layout.Elements[i].InputSlot == targetSlot)
                    {
                        prev = layout.Elements[i];
                        previousElementIndex = i; // Update index for format lookup
                        break;
                    }
                }
                // If still mismatch, implies this is the first element in this slot.
                if (prev.InputSlot != targetSlot) return 0;
            }

            // Get size of previous element
            int size = GetSizeOfElementFormat(layout.PackedFormats[previousElementIndex]);

            // If AlignedByteOffset is valid (>0), use it. Otherwise we have to rely on accumulation (risky).
            int baseOffset = prev.AlignedByteOffset;

            if (baseOffset == -1)
            {
                // Fallback: Sum from 0 (Classic method)
                return CalculateOffsetSum(layout, previousElementIndex + 1);
            }

            int nextOffset = baseOffset + size;

            // Alignment 4 bytes
            if (nextOffset % 4 != 0) nextOffset += (4 - (nextOffset % 4));

            return nextOffset;
        }

        private static int CalculateOffsetSum(VertexLayoutBlob layout, int targetIndex)
        {
            int offset = 0;
            if (targetIndex >= layout.Elements.Count) targetIndex = layout.Elements.Count;

            // Find target Slot
            int targetSlot = 0;
            if (targetIndex < layout.Elements.Count) targetSlot = layout.Elements[targetIndex].InputSlot;
            else if (targetIndex > 0) targetSlot = layout.Elements[targetIndex - 1].InputSlot;

            for (int i = 0; i < targetIndex; i++)
            {
                if (layout.Elements[i].InputSlot == targetSlot)
                {
                    offset += GetSizeOfElementFormat(layout.PackedFormats[i]);
                    if (offset % 4 != 0) offset += (4 - (offset % 4));
                }
            }
            return offset;
        }

        private static int GetSemanticIndex(VertexLayoutBlob layout, string name)
        {
            if (!layout.SemanticNames.Contains(name))
                layout.SemanticNames.Add(name);
            return layout.SemanticNames.IndexOf(name);
        }

        private static void InjectDataIntoBuffer(Bundle bundle, MeshBlob mesh, int slot, int offset, byte[] data, Action<string> log)
        {
            var vbUsage = mesh.VertexBuffers.FirstOrDefault(vb => vb.InputSlot == slot);
            if (vbUsage == null) return;

            VertexBufferBlob buffer = FindBufferById(bundle, vbUsage.Index);
            if (buffer != null && buffer.Header.Data != null)
            {
                log?.Invoke($"   -> Injecting {data.Length} bytes at Offset {offset} into Buffer {vbUsage.Index}");
                for (int i = 0; i < buffer.Header.Data.Length; i++)
                {
                    var row = new List<byte>(buffer.Header.Data[i]);
                    int localOffset = offset;

                    if (localOffset > row.Count)
                    {
                        // Padding needed if offset is beyond current row length
                        int diff = localOffset - row.Count;
                        row.AddRange(new byte[diff]);
                        localOffset = row.Count;
                    }

                    row.InsertRange(localOffset, data);
                    buffer.Header.Data[i] = row.ToArray();
                }
            }
        }

        private static void UpdateBufferStride(Bundle bundle, MeshBlob mesh, int slot)
        {
            var vbUsage = mesh.VertexBuffers.FirstOrDefault(vb => vb.InputSlot == slot);
            if (vbUsage == null) return;

            VertexBufferBlob buffer = FindBufferById(bundle, vbUsage.Index);
            if (buffer != null && buffer.Header.Data.Length > 0)
            {
                // Set Stride equal to the actual length of the first vertex row
                // This ensures perfect consistency between Data and Header.
                buffer.Header.Stride = (ushort)buffer.Header.Data[0].Length;
                buffer.Header.NumElements = (byte)buffer.Header.Data.Length; // Update vertex count
            }
        }

        private static VertexBufferBlob FindBufferById(Bundle bundle, int id)
        {
            foreach (var blob in bundle.Blobs)
            {
                if (blob is VertexBufferBlob vb)
                {
                    var idMeta = blob.GetMetadataByTag<IdentifierMetadata>(BundleMetadata.TAG_METADATA_Identifier);
                    if (idMeta != null && idMeta.Id == id) return vb;
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
                DXGI_FORMAT.DXGI_FORMAT_R10G10B10A2_UNORM => 4,
                DXGI_FORMAT.DXGI_FORMAT_R11G11B10_FLOAT => 4,
                DXGI_FORMAT.DXGI_FORMAT_R8G8_TYPELESS => 4,
                _ => 4,
            };
        }
    }
}