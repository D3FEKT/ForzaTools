using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using ForzaTools.Bundles;
using ForzaTools.Bundles.Blobs;
using ForzaTools.Bundles.Metadata;

namespace ForzaTools.ForzaAnalyzer.Services
{
    public sealed class ForzaGeometryData
    {
        public string Name;

        // Render Data
        public Vector3[] Positions;
        public Vector3[] Normals;
        public Vector2[] UVs;
        public int[] Indices;

        // EDITING SUPPORT
        public MeshBlob SourceMesh;
        public Vector3[] RawPositions; // Normalized -1..1 values (before Scale/Translate)
    }

    public sealed class ImporterResult
    {
        public readonly List<ForzaGeometryData> Meshes = new();
        public readonly List<string> Logs = new();
    }

    public sealed class ModelImporter
    {
        private readonly List<string> _logs = new();
        private void Log(string s) => _logs.Add(s);

        private const int MODEL_BUFFER_HEADER_SIZE = 16;

        private class ElementInfo
        {
            public D3D12_INPUT_LAYOUT_DESC Desc;
            public int Offset;
        }

        public ImporterResult ExtractModels(Bundle bundle)
        {
            var result = new ImporterResult();
            _logs.Clear();

            var skeleton = bundle.Blobs.OfType<SkeletonBlob>().FirstOrDefault();

            // Map Layouts
            var layouts = bundle.Blobs.OfType<VertexLayoutBlob>().ToArray();

            var indexBuffers = bundle.Blobs.OfType<IndexBufferBlob>().ToArray();
            var meshes = bundle.Blobs.OfType<MeshBlob>().ToArray();

            // Build Vertex Buffer Map (Key: Metadata ID)
            var vertexBufferMap = new Dictionary<int, VertexBufferBlob>();
            foreach (var vb in bundle.Blobs.OfType<VertexBufferBlob>())
            {
                var idMeta = vb.Metadatas.OfType<IdentifierMetadata>().FirstOrDefault();
                if (idMeta != null)
                {
                    // Cast to int to match usage keys
                    vertexBufferMap[(int)idMeta.Id] = vb;
                }
            }

            if (indexBuffers.Length == 0) return result;

            // Use the first index buffer (Standard Forza logic)
            var globalIndexBuffer = indexBuffers[0];

            // Handle Index Buffer Data Source (In-Memory vs Loaded)
            byte[] globalIndexData;
            int indexBufferOffset = 0;

            if (globalIndexBuffer.Header?.Data != null && globalIndexBuffer.Header.Data.Length > 0)
            {
                // In-Memory (No Header in Data)
                globalIndexData = globalIndexBuffer.Header.Data.SelectMany(x => x).ToArray();
                indexBufferOffset = 0;
            }
            else
            {
                // Loaded (Header included in Data)
                globalIndexData = globalIndexBuffer.GetContents();
                indexBufferOffset = MODEL_BUFFER_HEADER_SIZE;
            }

            Matrix4x4[] boneMatrices = null;
            if (skeleton != null)
            {
                boneMatrices = new Matrix4x4[skeleton.Bones.Count];
                for (int i = 0; i < skeleton.Bones.Count; i++)
                {
                    var b = skeleton.Bones[i];
                    var m = b.Matrix;
                    if (b.ParentId >= 0 && b.ParentId < i) m = m * boneMatrices[b.ParentId];
                    boneMatrices[i] = m;
                }
            }

            foreach (var mesh in meshes)
            {
                // Filter LODs if necessary
              //  if ((mesh.LODFlags & 0b1111) == 0 && !mesh.LOD_LOD0) continue;

                try
                {
                    // Pass 'bundle' and correct offsets
                    var geo = ExtractMesh(bundle, mesh, layouts, vertexBufferMap, globalIndexData, indexBufferOffset, boneMatrices);
                    if (geo != null) result.Meshes.Add(geo);
                }
                catch (Exception ex) { Log($"Mesh failed: {ex.Message}"); throw; }
            }

            result.Logs.AddRange(_logs);
            return result;
        }

        private ForzaGeometryData ExtractMesh(
            Bundle bundle,
            MeshBlob mesh,
            VertexLayoutBlob[] layouts,
            Dictionary<int, VertexBufferBlob> vbMap,
            byte[] globalIndexData,
            int globalIndexBaseOffset,
            Matrix4x4[] boneMatrices)
        {
            // Resolve Layout
            VertexLayoutBlob layout = null;

            // 1. Try Metadata ID
            var layoutById = bundle.Blobs.OfType<VertexLayoutBlob>()
                .FirstOrDefault(l => l.Metadatas.OfType<IdentifierMetadata>().Any(m => (int)m.Id == mesh.VertexLayoutIndex));

            if (layoutById != null)
                layout = layoutById;
            // 2. Fallback to Array Index
            else if (mesh.VertexLayoutIndex >= 0 && mesh.VertexLayoutIndex < layouts.Length)
                layout = layouts[mesh.VertexLayoutIndex];
            // 3. Last Resort
            else if (layouts.Length > 0)
                layout = layouts[0];

            if (layout == null) throw new Exception("Vertex Layout not found.");

            string name = mesh.Metadatas.OfType<NameMetadata>().FirstOrDefault()?.Name ?? "Mesh";

            // 1. INDICES
            int indexStride = mesh.Is32BitIndices ? 4 : 2;
            int indexCount = mesh.IndexCount;
            long startIndexOffset = globalIndexBaseOffset + ((long)mesh.IndexBufferDrawOffset * indexStride);

            if (globalIndexData == null || startIndexOffset + (indexCount * indexStride) > globalIndexData.Length)
                throw new Exception("Index buffer data out of bounds or missing.");

            var indices = new int[indexCount];
            int minIndex = int.MaxValue;
            int maxIndex = int.MinValue;

            using (var ms = new MemoryStream(globalIndexData))
            using (var br = new BinaryReader(ms))
            {
                ms.Position = startIndexOffset;
                for (int i = 0; i < indexCount; i++)
                {
                    int idx = (indexStride == 4) ? br.ReadInt32() : br.ReadUInt16();
                    indices[i] = idx;
                    if (idx < minIndex) minIndex = idx;
                    if (idx > maxIndex) maxIndex = idx;
                }
            }

            // 2. VERTICES
            int vertexCount = (maxIndex - minIndex) + 1;
            if (vertexCount <= 0) return null;

            var rawPos = new Vector3[vertexCount];
            var finalPos = new Vector3[vertexCount];
            var posW = new Vector4[vertexCount];
            var normals = new Vector3[vertexCount];
            var uvs = new Vector2[vertexCount];

            var elementsToRead = new List<ElementInfo>();
            var slotOffsets = new Dictionary<int, int>();

            foreach (var element in layout.Elements)
            {
                int slot = element.InputSlot;
                if (!slotOffsets.ContainsKey(slot)) slotOffsets[slot] = 0;
                elementsToRead.Add(new ElementInfo { Desc = element, Offset = slotOffsets[slot] });
                slotOffsets[slot] += GetFormatSize((int)element.Format);
            }

            // PASS 1: Position
            foreach (var item in elementsToRead)
            {
                if (GetSemanticName(layout, item.Desc) != "POSITION") continue;

                ReadBuffer(bundle, mesh, vbMap, item.Desc, item.Offset, minIndex, vertexCount, (br, i) =>
                {
                    var (raw, decompressed, w) = ReadPositionFull(br, (int)item.Desc.Format, mesh);
                    rawPos[i] = raw;
                    posW[i] = new Vector4(decompressed, w);
                });
            }

            // PASS 2: Normals/UV
            foreach (var item in elementsToRead)
            {
                string semantic = GetSemanticName(layout, item.Desc);
                if (semantic == "POSITION") continue;

                ReadBuffer(bundle, mesh, vbMap, item.Desc, item.Offset, minIndex, vertexCount, (br, i) =>
                {
                    if (semantic == "NORMAL")
                        normals[i] = ReadNormal(br, (int)item.Desc.Format, posW[i].W);
                    else if (semantic == "TEXCOORD" && item.Desc.SemanticIndex == 0)
                        uvs[i] = ReadUV(br, (int)item.Desc.Format);
                });
            }

            // TRANSFORM
            bool hasBone = boneMatrices != null && mesh.RigidBoneIndex >= 0 && mesh.RigidBoneIndex < boneMatrices.Length;
            var transform = hasBone ? boneMatrices[mesh.RigidBoneIndex] : Matrix4x4.Identity;

            for (int i = 0; i < vertexCount; i++)
            {
                var p = new Vector3(posW[i].X, posW[i].Y, posW[i].Z);
                var n = normals[i];

                if (hasBone)
                {
                    p = Vector3.Transform(p, transform);
                    n = Vector3.TransformNormal(n, transform);
                }

                finalPos[i] = p;
                normals[i] = n;
            }

            // Re-index
            var finalIndices = new int[indices.Length];
            for (int i = 0; i < indices.Length; i++) finalIndices[i] = indices[i] - minIndex;

            return new ForzaGeometryData
            {
                Name = name,
                Positions = finalPos,
                Normals = normals,
                UVs = uvs,
                Indices = finalIndices,
                SourceMesh = mesh,
                RawPositions = rawPos
            };
        }

        private void ReadBuffer(
            Bundle bundle,
            MeshBlob mesh,
            Dictionary<int, VertexBufferBlob> vbMap,
            D3D12_INPUT_LAYOUT_DESC element,
            int offsetInStride,
            int minIndex,
            int count,
            Action<BinaryReader, int> readAction)
        {
            var usage = mesh.VertexBuffers.FirstOrDefault(v => v.InputSlot == element.InputSlot);
            if (usage == null) return;

            // 1. Resolve Buffer (ID vs Index)
            VertexBufferBlob vb = null;
            if (vbMap.TryGetValue(usage.Index, out var vbById))
            {
                vb = vbById;
            }
            else if (usage.Index >= 0 && usage.Index < bundle.Blobs.Count)
            {
                vb = bundle.Blobs[usage.Index] as VertexBufferBlob;
            }

            if (vb == null) return;

            // 2. Determine Data Source (Header.Data vs Raw Blob Data)
            byte[] data;
            int baseOffset = 0;

            if (vb.Header?.Data != null && vb.Header.Data.Length > 0)
            {
                // In-Memory Created: Pure Vertex Data (No Header)
                data = vb.Header.Data.SelectMany(x => x).ToArray();
                baseOffset = 0;
            }
            else
            {
                // Loaded from File: Raw Blob Data (Includes 16-byte Header)
                data = vb.GetContents();
                baseOffset = MODEL_BUFFER_HEADER_SIZE;
            }

            if (data == null || data.Length == 0) return;

            long stride = vb.Header?.Stride > 0 ? vb.Header.Stride : usage.Stride;
            if (stride == 0) stride = 28; // Fallback

            using (var ms = new MemoryStream(data))
            using (var br = new BinaryReader(ms))
            {
                long usageOffset = usage.Offset;
                for (int i = 0; i < count; i++)
                {
                    long vertexId = minIndex + i + mesh.IndexedVertexOffset;
                    long addr = baseOffset + usageOffset + (vertexId * stride) + offsetInStride;

                    if (addr >= 0 && addr + 4 <= data.Length)
                    {
                        ms.Position = addr;
                        readAction(br, i);
                    }
                }
            }
        }

        // --- Helpers ---
        private string GetSemanticName(VertexLayoutBlob layout, D3D12_INPUT_LAYOUT_DESC element) => (element.SemanticNameIndex >= 0 && element.SemanticNameIndex < layout.SemanticNames.Count) ? layout.SemanticNames[element.SemanticNameIndex] : "UNKNOWN";
        private int GetFormatSize(int fmt) => fmt switch { 6 => 12, 10 => 8, 13 => 8, 16 => 8, 24 => 4, 28 => 4, 35 => 4, 37 => 4, _ => 4 };

        private static (Vector3 Raw, Vector3 Decompressed, float W) ReadPositionFull(BinaryReader br, int format, MeshBlob mesh)
        {
            if (format == 13) // SNORM16
            {
                float rx = Math.Clamp(br.ReadInt16() / 32767f, -1f, 1f);
                float ry = Math.Clamp(br.ReadInt16() / 32767f, -1f, 1f);
                float rz = Math.Clamp(br.ReadInt16() / 32767f, -1f, 1f);
                float w = Math.Clamp(br.ReadInt16() / 32767f, -1f, 1f);
                float x = rx, y = ry, z = rz;

                var scale = mesh.PositionScale;
                if (scale.X == 0 && scale.Y == 0 && scale.Z == 0) scale = Vector4.One;

                if (mesh.PositionScale != Vector4.Zero) // Change this check if you want, or rely on the fix above
                {
                    // Use the local 'scale' variable instead of 'mesh.PositionScale'
                    x = x * scale.X + mesh.PositionTranslate.X;
                    y = y * scale.Y + mesh.PositionTranslate.Y;
                    z = z * scale.Z + mesh.PositionTranslate.Z;
                }
                return (new Vector3(rx, ry, rz), new Vector3(x, y, z), w);
            }
            if (format == 6) // FLOAT32
            {
                float x = br.ReadSingle(); float y = br.ReadSingle(); float z = br.ReadSingle();
                return (new Vector3(x, y, z), new Vector3(x, y, z), 1f);
            }
            return (Vector3.Zero, Vector3.Zero, 0);
        }

        private static Vector3 ReadNormal(BinaryReader br, int format, float wFromPos)
        {
            if (format == 37) { float nx = wFromPos; float ny = Math.Clamp(br.ReadInt16() / 32767f, -1f, 1f); float nz = Math.Clamp(br.ReadInt16() / 32767f, -1f, 1f); return Vector3.Normalize(new Vector3(nx, ny, nz)); }
            if (format == 10) { float nx = (float)BitConverter.UInt16BitsToHalf(br.ReadUInt16()); float ny = (float)BitConverter.UInt16BitsToHalf(br.ReadUInt16()); float nz = (float)BitConverter.UInt16BitsToHalf(br.ReadUInt16()); br.ReadUInt16(); return Vector3.Normalize(new Vector3(nx, ny, nz)); }
            if (format == 24) { uint v = br.ReadUInt32(); float nx = ((v >> 0) & 0x3FF) / 1023f * 2f - 1f; float ny = ((v >> 10) & 0x3FF) / 1023f * 2f - 1f; float nz = ((v >> 20) & 0x3FF) / 1023f * 2f - 1f; return Vector3.Normalize(new Vector3(nx, ny, nz)); }
            return Vector3.UnitY;
        }

        private static Vector2 ReadUV(BinaryReader br, int format)
        {
            if (format == 35) return new Vector2(br.ReadUInt16() / 65535f, 1f - (br.ReadUInt16() / 65535f));
            if (format == 16) return new Vector2(br.ReadSingle(), 1f - br.ReadSingle());
            return Vector2.Zero;
        }
    }
}