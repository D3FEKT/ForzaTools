using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using ForzaTools.Bundles;
using ForzaTools.Bundles.Blobs;
using ForzaTools.Bundles.Metadata;

namespace ForzaTools.ForzaAnalyzer.Services
{
    public class ForzaGeometryData
    {
        public string Name { get; set; }
        public Vector3[] Positions { get; set; }
        public Vector3[] Normals { get; set; }
        public Vector2[] UVs { get; set; }
        public int[] Indices { get; set; }
        public bool IsValid => Positions != null && Positions.Length > 0;
    }

    public class ImporterResult
    {
        public List<ForzaGeometryData> Meshes { get; set; } = new();
        public List<string> Logs { get; set; } = new();
    }

    public class ModelImporter
    {
        private List<string> _logs = new();
        private void Log(string msg) => _logs.Add(msg);

        public ImporterResult ExtractModels(Bundle bundle)
        {
            var result = new ImporterResult();
            _logs.Clear();

            var skeleton = bundle.Blobs.OfType<SkeletonBlob>().FirstOrDefault();
            var vertexLayouts = bundle.Blobs.OfType<VertexLayoutBlob>().ToList();
            var indexBuffers = bundle.Blobs.OfType<IndexBufferBlob>().ToList();
            var vertexBuffers = bundle.Blobs.OfType<VertexBufferBlob>().ToList();
            var meshes = bundle.Blobs.OfType<MeshBlob>().ToList();

            if (vertexBuffers.Count == 0 || indexBuffers.Count == 0)
            {
                Log("Error: Missing Vertex or Index buffers.");
                result.Logs = _logs;
                return result;
            }

            // 1. Compute Bone Transforms
            Matrix4x4[] boneTransforms = null;
            if (skeleton != null)
            {
                boneTransforms = new Matrix4x4[skeleton.Bones.Count];
                for (int i = 0; i < skeleton.Bones.Count; i++)
                {
                    var bone = skeleton.Bones[i];
                    var mat = bone.Matrix;
                    if (bone.ParentId >= 0 && bone.ParentId < i)
                        mat = mat * boneTransforms[bone.ParentId];
                    boneTransforms[i] = mat;
                }
            }

            var indexBufferBlob = indexBuffers[0];
            byte[] indexData = indexBufferBlob.GetContents();

            int meshIndex = 0;
            foreach (var mesh in meshes)
            {
                meshIndex++;
                // Skip low LODs
                if ((mesh.LODFlags & 1) == 0 && (mesh.LODFlags & 2) == 0) continue;

                string meshName = "Mesh_" + meshIndex;
                var nameMeta = mesh.Metadatas.FirstOrDefault(m => m.Tag == BundleMetadata.TAG_METADATA_Name) as NameMetadata;
                if (nameMeta != null) meshName = nameMeta.Name;

                try
                {
                    // --- 1. READ INDICES ---
                    int startIdx = mesh.IndexBufferDrawOffset;
                    int indexCount = mesh.IndexCount;
                    var meshIndices = new List<int>(indexCount);
                    int minIndex = int.MaxValue;
                    int maxIndex = int.MinValue;
                    int indexStride = mesh.Is32BitIndices ? 4 : 2;

                    using (var reader = new BinaryReader(new MemoryStream(indexData)))
                    {
                        long idxOffset = startIdx * indexStride;
                        if (idxOffset < indexData.Length)
                        {
                            reader.BaseStream.Position = idxOffset;
                            for (int i = 0; i < indexCount; i++)
                            {
                                if (reader.BaseStream.Position >= reader.BaseStream.Length) break;
                                int idx = mesh.Is32BitIndices ? reader.ReadInt32() : reader.ReadUInt16();
                                meshIndices.Add(idx);
                                if (idx < minIndex) minIndex = idx;
                                if (idx > maxIndex) maxIndex = idx;
                            }
                        }
                    }

                    if (meshIndices.Count == 0) continue;

                    int vertexCount = (maxIndex - minIndex) + 1;

                    var posNum = new Vector3[vertexCount];
                    var normNum = new Vector3[vertexCount];
                    var uvNum = new Vector2[vertexCount];
                    bool hasPositions = false;

                    var layout = vertexLayouts[mesh.VertexLayoutIndex];

                    // --- 2. READ VERTICES ---
                    foreach (var element in layout.Elements)
                    {
                        var vbUsage = mesh.VertexBuffers.FirstOrDefault(v => v.InputSlot == element.InputSlot);

                        int targetVbIndex = -1;

                        // Strategy: Explicit Link -> Fallback to InputSlot -> Fallback to Stride Match
                        if (vbUsage != null && vbUsage.Index >= 0 && vbUsage.Index < vertexBuffers.Count)
                        {
                            targetVbIndex = vbUsage.Index;
                        }
                        else if (element.InputSlot < vertexBuffers.Count)
                        {
                            targetVbIndex = (int)element.InputSlot;
                        }

                        if (targetVbIndex == -1) continue;

                        var vbBlob = vertexBuffers[targetVbIndex];
                        byte[] vbData = vbBlob.GetContents();

                        // Offset/Stride Logic
                        long bufferStart = vbUsage?.Offset ?? 0;
                        long stride = vbUsage?.Stride ?? 0;

                        // IMPROVEMENT: If stride is 0 or suspicious, try to guess or skip
                        if (stride == 0 && targetVbIndex == 0) stride = 28; // Common position stride guess
                        if (stride == 0) continue;

                        // IMPROVEMENT: Basic Stride Check to avoid reading wrong buffers
                        // Positions are usually 12+ bytes. UVs are 4-8 bytes.
                        string semantic = "";
                        if (element.SemanticNameIndex >= 0 && element.SemanticNameIndex < layout.SemanticNames.Count)
                            semantic = layout.SemanticNames[element.SemanticNameIndex];

                        if (semantic == "POSITION" && stride < 6) continue; // Unlikely to be a position buffer

                        using (var reader = new BinaryReader(new MemoryStream(vbData)))
                        {
                            for (int i = 0; i < vertexCount; i++)
                            {
                                int originalVertexId = minIndex + i;

                                // ADAPTIVE ADDRESSING (Try to find the valid data stream)
                                long readAddr = -1;

                                // 1. Standard (Index + BaseVertex)
                                long addr1 = bufferStart + (originalVertexId + mesh.IndexedVertexOffset) * stride;
                                if (element.AlignedByteOffset != -1) addr1 += element.AlignedByteOffset;

                                // 2. Raw Index
                                long addr2 = bufferStart + (originalVertexId) * stride;
                                if (element.AlignedByteOffset != -1) addr2 += element.AlignedByteOffset;

                                // 3. Local Index (0-based)
                                long addr3 = bufferStart + (i) * stride;
                                if (element.AlignedByteOffset != -1) addr3 += element.AlignedByteOffset;

                                // Prioritize based on validity
                                if (IsValidAddr(addr1, vbData.Length)) readAddr = addr1;
                                else if (IsValidAddr(addr2, vbData.Length)) readAddr = addr2;
                                else if (IsValidAddr(addr3, vbData.Length)) readAddr = addr3;

                                if (readAddr == -1) continue;

                                reader.BaseStream.Position = readAddr;
                                int fmt = (int)element.Format;

                                if (semantic == "POSITION")
                                {
                                    posNum[i] = ReadPosition(reader, fmt, mesh);
                                    hasPositions = true;
                                }
                                else if (semantic == "NORMAL")
                                    normNum[i] = ReadNormal(reader, fmt);
                                else if (semantic == "TEXCOORD" && element.SemanticIndex == 0)
                                    uvNum[i] = ReadUV(reader, fmt);
                            }
                        }
                    }

                    if (!hasPositions) continue;

                    // --- 3. TRANSFORM ---
                    bool hasTransform = boneTransforms != null && mesh.RigidBoneIndex >= 0 && mesh.RigidBoneIndex < boneTransforms.Length;
                    var transform = hasTransform ? boneTransforms[mesh.RigidBoneIndex] : Matrix4x4.Identity;

                    if (hasTransform)
                    {
                        for (int i = 0; i < vertexCount; i++)
                        {
                            posNum[i] = Vector3.Transform(posNum[i], transform);
                            normNum[i] = Vector3.TransformNormal(normNum[i], transform);
                        }
                    }

                    // --- 4. RE-INDEX ---
                    var finalIndices = meshIndices.Select(idx => idx - minIndex).ToArray();

                    result.Meshes.Add(new ForzaGeometryData
                    {
                        Name = meshName,
                        Positions = posNum,
                        Normals = normNum,
                        UVs = uvNum,
                        Indices = finalIndices
                    });
                }
                catch (Exception ex)
                {
                    Log($"Error {meshName}: {ex.Message}");
                }
            }

            result.Logs = _logs;
            return result;
        }

        private bool IsValidAddr(long addr, long len) => addr >= 0 && addr + 16 <= len;

        private Vector3 ReadPosition(BinaryReader br, int format, MeshBlob mesh)
        {
            try
            {
                float x = 0, y = 0, z = 0;
                // SNORM16
                if (format == 13)
                {
                    x = Math.Clamp(br.ReadInt16() / 32767f, -1f, 1f);
                    y = Math.Clamp(br.ReadInt16() / 32767f, -1f, 1f);
                    z = Math.Clamp(br.ReadInt16() / 32767f, -1f, 1f);
                    br.ReadInt16(); // Skip W

                    if (mesh.PositionScale != Vector4.Zero)
                    {
                        x = x * mesh.PositionScale.X + mesh.PositionTranslate.X;
                        y = y * mesh.PositionScale.Y + mesh.PositionTranslate.Y;
                        z = z * mesh.PositionScale.Z + mesh.PositionTranslate.Z;
                    }
                }
                // FLOAT32
                else if (format == 6) { x = br.ReadSingle(); y = br.ReadSingle(); z = br.ReadSingle(); }

                // FLIP Z for Helix Viewport
                return new Vector3(x, y, z);
            }
            catch { return Vector3.Zero; }
        }

        private Vector3 ReadNormal(BinaryReader br, int format)
        {
            try
            {
                // R16G16B16A16_FLOAT (Half) - Placeholder read
                if (format == 10) { br.ReadUInt64(); return Vector3.UnitY; }
                // R10G10B10A2_UNORM - Common normal format, usually requires bit manipulation
                if (format == 24) { br.ReadUInt32(); return Vector3.UnitY; }

                return Vector3.UnitY;
            }
            catch { return Vector3.UnitY; }
        }

        private Vector2 ReadUV(BinaryReader br, int format)
        {
            try
            {
                // R16G16_UNORM
                if (format == 35)
                {
                    float u = br.ReadUInt16() / 65535f;
                    float v = br.ReadUInt16() / 65535f;
                    return new Vector2(u, 1.0f - v);
                }
                // R32G32_FLOAT
                if (format == 16)
                {
                    float u = br.ReadSingle();
                    float v = br.ReadSingle();
                    return new Vector2(u, 1.0f - v);
                }
                return Vector2.Zero;
            }
            catch { return Vector2.Zero; }
        }
    }
}