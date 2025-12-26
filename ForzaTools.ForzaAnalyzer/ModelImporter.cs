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

        private string _debugPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "model_debug_dump.txt");
        private const int MODEL_BUFFER_HEADER_SIZE = 16;

        private class ElementInfo
        {
            public D3D12_INPUT_LAYOUT_DESC Desc;
            public int Offset;
        }

        public ImporterResult ExtractModels(Bundle bundle)
        {
            // (Debug file writing removed for brevity, keep if needed)
            var result = new ImporterResult();
            _logs.Clear();

            var skeleton = bundle.Blobs.OfType<SkeletonBlob>().FirstOrDefault();
            var vertexLayouts = bundle.Blobs.OfType<VertexLayoutBlob>().ToArray();
            var indexBuffers = bundle.Blobs.OfType<IndexBufferBlob>().ToArray();
            var meshes = bundle.Blobs.OfType<MeshBlob>().ToArray();

            var vertexBufferMap = new Dictionary<int, VertexBufferBlob>();
            foreach (var vb in bundle.Blobs.OfType<VertexBufferBlob>())
            {
                var idMeta = vb.Metadatas.OfType<IdentifierMetadata>().FirstOrDefault();
                if (idMeta != null) vertexBufferMap[(int)idMeta.Id] = vb;
            }

            if (indexBuffers.Length == 0) return result;

            var globalIndexBuffer = indexBuffers[0];
            byte[] globalIndexData = globalIndexBuffer.GetContents();

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
                if ((mesh.LODFlags & 0b1111) == 0 && !mesh.LOD_LOD0) continue;

                try
                {
                    var geo = ExtractMesh(mesh, vertexLayouts, vertexBufferMap, globalIndexData, boneMatrices);
                    if (geo != null) result.Meshes.Add(geo);
                }
                catch (Exception ex) { Log($"Mesh failed: {ex.Message}"); }
            }

            result.Logs.AddRange(_logs);
            return result;
        }

        private ForzaGeometryData ExtractMesh(
            MeshBlob mesh,
            VertexLayoutBlob[] layouts,
            Dictionary<int, VertexBufferBlob> vbMap,
            byte[] globalIndexData,
            Matrix4x4[] boneMatrices)
        {
            var layout = layouts[mesh.VertexLayoutIndex];
            string name = mesh.Metadatas.OfType<NameMetadata>().FirstOrDefault()?.Name ?? "Mesh";

            // 1. INDICES
            int indexStride = mesh.Is32BitIndices ? 4 : 2;
            int indexCount = mesh.IndexCount;
            long startIndexOffset = MODEL_BUFFER_HEADER_SIZE + ((long)mesh.IndexBufferDrawOffset * indexStride);

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

            // We store RawPositions (normalized) AND FinalPositions
            var rawPos = new Vector3[vertexCount];
            var finalPos = new Vector3[vertexCount];

            var posW = new Vector4[vertexCount]; // Temp storage for W component (Normal X)
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
                ReadBuffer(mesh, vbMap, item.Desc, item.Offset, minIndex, vertexCount, (br, i) =>
                {
                    // Read Raw (Normalized) and Decompressed values
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

                ReadBuffer(mesh, vbMap, item.Desc, item.Offset, minIndex, vertexCount, (br, i) =>
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
                SourceMesh = mesh,      // LINKED FOR SAVING
                RawPositions = rawPos   // SAVED FOR LIVE EDITING
            };
        }

        private void ReadBuffer(MeshBlob mesh, Dictionary<int, VertexBufferBlob> vbMap, D3D12_INPUT_LAYOUT_DESC element, int offsetInStride, int minIndex, int count, Action<BinaryReader, int> readAction)
        {
            var usage = mesh.VertexBuffers.FirstOrDefault(v => v.InputSlot == element.InputSlot);
            if (usage == null || !vbMap.TryGetValue(usage.Index, out var vb)) return;

            byte[] data = Flatten(vb);
            long stride = vb.Header?.Stride ?? usage.Stride;
            if (stride == 0) stride = 28;

            using (var ms = new MemoryStream(data))
            using (var br = new BinaryReader(ms))
            {
                long usageOffset = usage.Offset;
                for (int i = 0; i < count; i++)
                {
                    long vertexId = minIndex + i + mesh.IndexedVertexOffset;
                    long addr = usageOffset + (vertexId * stride) + offsetInStride;
                    if (addr >= 0 && addr + 4 <= data.Length)
                    {
                        ms.Position = addr;
                        readAction(br, i);
                    }
                }
            }
        }

        private byte[] Flatten(VertexBufferBlob vb) => vb.Header?.Data != null ? vb.Header.Data.SelectMany(x => x).ToArray() : Array.Empty<byte>();
        private string GetSemanticName(VertexLayoutBlob layout, D3D12_INPUT_LAYOUT_DESC element) => (element.SemanticNameIndex >= 0 && element.SemanticNameIndex < layout.SemanticNames.Count) ? layout.SemanticNames[element.SemanticNameIndex] : "UNKNOWN";
        private int GetFormatSize(int fmt) => fmt switch { 6 => 12, 10 => 8, 13 => 8, 16 => 8, 24 => 4, 28 => 4, 35 => 4, 37 => 4, _ => 4 };

        // UPDATED DECODER: Returns both Raw and Final values
        private static (Vector3 Raw, Vector3 Decompressed, float W) ReadPositionFull(BinaryReader br, int format, MeshBlob mesh)
        {
            if (format == 13) // SNORM16
            {
                float rx = Math.Clamp(br.ReadInt16() / 32767f, -1f, 1f);
                float ry = Math.Clamp(br.ReadInt16() / 32767f, -1f, 1f);
                float rz = Math.Clamp(br.ReadInt16() / 32767f, -1f, 1f);
                float w = Math.Clamp(br.ReadInt16() / 32767f, -1f, 1f);

                float x = rx, y = ry, z = rz;

                if (mesh.PositionScale != Vector4.Zero)
                {
                    x = x * mesh.PositionScale.X + mesh.PositionTranslate.X;
                    y = y * mesh.PositionScale.Y + mesh.PositionTranslate.Y;
                    z = z * mesh.PositionScale.Z + mesh.PositionTranslate.Z;
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