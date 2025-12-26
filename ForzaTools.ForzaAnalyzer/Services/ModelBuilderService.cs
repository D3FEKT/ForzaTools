using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using ForzaTools.Bundles;
using ForzaTools.Bundles.Blobs;
using ForzaTools.Bundles.Metadata;
using ForzaTools.Shared;

namespace ForzaTools.ForzaAnalyzer.Services
{
    public class ModelBuilderService
    {
        // Container for the results of our data processing
        private class ProcessedGeometry
        {
            public byte[][] PositionData; // Buffer 0: PosX, PosY, PosZ, NormX (All Int16)
            public byte[][] NormalUVData; // Buffer 1: NormY, NormZ, U, V (All Int16)
            public byte[][] IndexData;    // Index Buffer

            // Scaling factors required by MeshBlob to decompress positions
            public Vector4 PositionScale;
            public Vector4 PositionTranslate;
        }

        public struct GeometryInput
        {
            public string Name;
            public Vector3[] Positions;
            public Vector3[] Normals;
            public Vector2[] UVs;
            public int[] Indices;
        }

        /// <summary>
        /// Main entry point for building a compatible FH4/FH5 ModelBin.
        /// </summary>
        public void BuildCompatibleModelBin(string outputPath, GeometryInput input)
        {
            var processed = ProcessGeometry(input);

            var bundle = new Bundle { VersionMajor = 1, VersionMinor = 9 };

            // --- IDs for Linking (Using List Indices) ---
            // 0: Skel, 1: Morph, 2-7: Meshes, 8: Material
            // 9: IndexBuffer, 10: LayoutPos, 11: LayoutNorm, 12: VB_Pos, 13: VB_Norm, 14: Model
            int idxIB = 9;
            int idxLayout = 10;
            int idxVB1 = 12;
            int idxVB2 = 13;

            // 0: Skeleton
            bundle.Blobs.Add(CreateDummyBlob<SkeletonBlob>(Bundle.TAG_BLOB_Skeleton));

            // 1: Morph
            bundle.Blobs.Add(CreateDummyBlob<MorphBlob>(Bundle.TAG_BLOB_Morph));

            // 2-7: Meshes (LOD0 - LOD5)
            for (int i = 0; i < 6; i++)
                bundle.Blobs.Add(CreateMeshBlob(processed, i, idxIB, idxVB1, idxVB2, idxLayout));

            // 8: Material
            var mat = new MaterialBlob
            {
                Tag = Bundle.TAG_BLOB_MaterialInstance, // "ItaM"
                VersionMajor = 1,
                VersionMinor = 9
            };
            mat.Metadatas.Add(new NameMetadata { Name = "material", Version = 0 });
            bundle.Blobs.Add(mat);

            // 9: Index Buffer
            var ib = CreateIndexBufferBlob(processed.IndexData);
            bundle.Blobs.Add(ib);

            // 10: Layout Pos
            bundle.Blobs.Add(CreateLayoutPosition());

            // 11: Layout Norm/UV
            bundle.Blobs.Add(CreateLayoutNormalUV());

            // 12: VB Pos (Format 13, Stride 8)
            var vb1 = CreateVertexBufferBlob(processed.PositionData, 8, 13);
            bundle.Blobs.Add(vb1);

            // 13: VB Norm/UV (Format 37, Stride 40)
            var vb2 = CreateVertexBufferBlob(processed.NormalUVData, 40, 37);
            bundle.Blobs.Add(vb2);

            // 14: Model
            bundle.Blobs.Add(CreateModelBlob());

            using (var fs = new FileStream(outputPath, FileMode.Create))
            {
                bundle.CreateModelBin(fs);
            }
        }

        // --- Helper Methods ---

        private T CreateDummyBlob<T>(uint tag) where T : BundleBlob, new()
        {
            return new T
            {
                Tag = tag, // Critical: Set the Tag so it writes correctly
                VersionMajor = 1,
                VersionMinor = 9
            };
        }

        private MeshBlob CreateMeshBlob(ProcessedGeometry geo, int lodLevel, int idIB, int idVB1, int idVB2, int layoutIdx)
        {
            var mesh = new MeshBlob
            {
                Tag = Bundle.TAG_BLOB_Mesh, // "hseM"
                VersionMajor = 1,
                VersionMinor = 9,

                // Linkage
                VertexLayoutIndex = layoutIdx,
                IndexBufferIndex = idIB,
                IndexCount = geo.IndexData.Length,
                PrimCount = geo.IndexData.Length / 3,

                // Flags
                LODFlags = 0xFFFF,
                IsOpaque = true,
                IsShadow = true,
                Topology = 4, // TriangleList
                Is32BitIndices = true,

                // Compression Params
                PositionScale = geo.PositionScale,
                PositionTranslate = geo.PositionTranslate,
            };

            // Mesh Metadata
            mesh.Metadatas.Add(new NameMetadata { Name = $"custommod_LOD{lodLevel}", Version = 0 });

            // Bounding Box
            Vector3 min = new Vector3(geo.PositionTranslate.X, geo.PositionTranslate.Y, geo.PositionTranslate.Z) - (new Vector3(geo.PositionScale.X, geo.PositionScale.Y, geo.PositionScale.Z) * 0.5f);
            Vector3 max = new Vector3(geo.PositionTranslate.X, geo.PositionTranslate.Y, geo.PositionTranslate.Z) + (new Vector3(geo.PositionScale.X, geo.PositionScale.Y, geo.PositionScale.Z) * 0.5f);
            mesh.Metadatas.Add(new BoundaryBoxMetadata { Min = min, Max = max });

            // Vertex Buffers Usage
            // 1. Position Buffer
            mesh.VertexBuffers.Add(new MeshBlob.VertexBufferUsage
            {
                Index = idVB1,
                InputSlot = 0,
                Offset = 0,
                Stride = 8
            });

            // 2. Normal/UV Buffer
            mesh.VertexBuffers.Add(new MeshBlob.VertexBufferUsage
            {
                Index = idVB2,
                InputSlot = 1,
                Offset = 0,
                Stride = 40
            });

            return mesh;
        }

        private VertexBufferBlob CreateVertexBufferBlob(byte[][] data, int stride, int formatId)
        {
            var blob = new VertexBufferBlob();
            blob.Tag = Bundle.TAG_BLOB_VertexBuffer; // "BreV"
            blob.Header = new BufferHeader
            {
                Length = data.Length,
                Size = data.Length * stride,
                Stride = (ushort)stride,
                NumElements = 1,
                Format = (DXGI_FORMAT)formatId,
                Data = data
            };
            return blob;
        }

        private VertexLayoutBlob CreateLayoutPosition()
        {
            var blob = new VertexLayoutBlob();
            blob.Tag = Bundle.TAG_BLOB_VertexLayout; // "yaLV"
            blob.SemanticNames.Add("POSITION");

            blob.Elements.Add(new D3D12_INPUT_LAYOUT_DESC
            {
                SemanticNameIndex = 0,
                SemanticIndex = 0,
                Format = DXGI_FORMAT.DXGI_FORMAT_R16G16B16A16_SNORM,
                InputSlot = 0,
                AlignedByteOffset = 0,
                InputSlotClass = 0,
                InstanceDataStepRate = 0
            });

            blob.PackedFormats.Add(DXGI_FORMAT.DXGI_FORMAT_R16G16B16A16_SNORM);
            return blob;
        }

        private VertexLayoutBlob CreateLayoutNormalUV()
        {
            var blob = new VertexLayoutBlob();
            blob.Tag = Bundle.TAG_BLOB_VertexLayout; // "yaLV"
            blob.SemanticNames.Add("NORMAL");
            blob.SemanticNames.Add("TEXCOORD");

            // Element 0: Normal/Base UV
            blob.Elements.Add(new D3D12_INPUT_LAYOUT_DESC
            {
                SemanticNameIndex = 1, // TEXCOORD (Accessing buffer 1)
                SemanticIndex = 0,
                Format = DXGI_FORMAT.DXGI_FORMAT_R16G16B16A16_SNORM,
                InputSlot = 1,
                AlignedByteOffset = 0
            });

            blob.PackedFormats.Add(DXGI_FORMAT.DXGI_FORMAT_R16G16B16A16_SNORM);
            return blob;
        }

        private IndexBufferBlob CreateIndexBufferBlob(byte[][] data)
        {
            var blob = new IndexBufferBlob();
            blob.Tag = Bundle.TAG_BLOB_IndexBuffer; // "BdnI"
            blob.Header = new BufferHeader
            {
                Length = data.Length,
                Size = data.Length * 4,
                Stride = 4,
                NumElements = 1,
                Format = DXGI_FORMAT.DXGI_FORMAT_R32_UINT,
                Data = data
            };
            return blob;
        }

        private ModelBlob CreateModelBlob()
        {
            return new ModelBlob
            {
                Tag = Bundle.TAG_BLOB_Model, // "ldoM"
                VersionMajor = 1,
                VersionMinor = 9,
                MeshCount = 6,
                BuffersCount = 3,
                VertexLayoutCount = 2,
                MaterialCount = 1,
                HasLOD = true,
                MaxLOD = 5,
                LODFlags = 0xFFFF
            };
        }

        private ProcessedGeometry ProcessGeometry(GeometryInput input)
        {
            var result = new ProcessedGeometry();

            // --- 1. Calculate Bounds ---
            Vector3 min = new Vector3(float.MaxValue);
            Vector3 max = new Vector3(float.MinValue);

            foreach (var p in input.Positions)
            {
                min = Vector3.Min(min, p);
                max = Vector3.Max(max, p);
            }

            Vector3 range = max - min;
            float maxRange = Math.Max(range.X, Math.Max(range.Y, range.Z));
            if (maxRange == 0) maxRange = 1.0f;

            float expansion = maxRange * 0.1f;
            Vector3 expandedMin = min - new Vector3(expansion / 2);
            Vector3 expandedMax = max + new Vector3(expansion / 2);

            Vector3 finalRange = expandedMax - expandedMin;
            Vector3 center = (expandedMin + expandedMax) / 2;

            result.PositionScale = new Vector4(finalRange.X, finalRange.Y, finalRange.Z, 1.0f);
            result.PositionTranslate = new Vector4(center, 0.0f);

            // --- 2. Create Buffer Arrays ---
            int vertexCount = input.Positions.Length;
            result.PositionData = new byte[vertexCount][];
            result.NormalUVData = new byte[vertexCount][];

            for (int i = 0; i < vertexCount; i++)
            {
                Vector3 pos = input.Positions[i];
                Vector3 norm = i < input.Normals.Length ? Vector3.Normalize(input.Normals[i]) : Vector3.UnitY;
                Vector2 uv = i < input.UVs.Length ? input.UVs[i] : Vector2.Zero;

                // --- Buffer 0: Position + Normal.X ---
                using (var ms = new MemoryStream())
                using (var bw = new BinaryWriter(ms))
                {
                    WriteQuantized(bw, pos.X, center.X, finalRange.X);
                    WriteQuantized(bw, pos.Y, center.Y, finalRange.Y);
                    WriteQuantized(bw, pos.Z, center.Z, finalRange.Z);
                    bw.Write((short)(norm.X * 32767));
                    result.PositionData[i] = ms.ToArray();
                }

                // --- Buffer 1: Normal.YZ + UVs ---
                using (var ms = new MemoryStream())
                using (var bw = new BinaryWriter(ms))
                {
                    bw.Write((short)(norm.Y * 32767));
                    bw.Write((short)(norm.Z * 32767));

                    ushort u = (ushort)(uv.X * 65535);
                    ushort v = (ushort)((1.0f - uv.Y) * 65535);

                    bw.Write(u);
                    bw.Write(v);

                    for (int j = 0; j < 8; j++)
                    {
                        bw.Write(u);
                        bw.Write(v);
                    }
                    result.NormalUVData[i] = ms.ToArray();
                }
            }

            // --- 3. Indices ---
            result.IndexData = new byte[input.Indices.Length][];
            for (int i = 0; i < input.Indices.Length; i++)
            {
                result.IndexData[i] = BitConverter.GetBytes(input.Indices[i]);
            }

            return result;
        }

        private void WriteQuantized(BinaryWriter bw, float value, float center, float range)
        {
            float scaled = 0.5f;
            if (range > 0)
                scaled = 0.5f + ((value - center) / range);

            if (scaled < 0) scaled = 0;
            if (scaled > 1) scaled = 1;

            short quantized = (short)((scaled * 65535f) - 32768f);
            bw.Write(quantized);
        }

        // --- RESTORED: Test Method for ViewModel ---
        public void BuildTestCube(string outputPath)
        {
            var geometry = new GeometryInput
            {
                Name = "TestCube",
                Positions = GetCubeVertices().Select(v => v.Position).ToArray(),
                Normals = GetCubeVertices().Select(v => v.Normal).ToArray(),
                UVs = GetCubeVertices().Select(v => v.UV).ToArray(),
                Indices = GetCubeIndices()
            };

            BuildCompatibleModelBin(outputPath, geometry);
        }

        // --- Geometry Helpers ---
        private struct Vertex { public Vector3 Position; public Vector3 Normal; public Vector2 UV; }

        private Vertex[] GetCubeVertices()
        {
            var verts = new List<Vertex>();

            void AddFace(Vector3 normal, Vector3 v1, Vector3 v2, Vector3 v3, Vector3 v4)
            {
                verts.Add(new Vertex { Position = v1, Normal = normal, UV = new Vector2(0, 0) });
                verts.Add(new Vertex { Position = v2, Normal = normal, UV = new Vector2(1, 0) });
                verts.Add(new Vertex { Position = v3, Normal = normal, UV = new Vector2(0, 1) });
                verts.Add(new Vertex { Position = v4, Normal = normal, UV = new Vector2(1, 1) });
            }

            Vector3 p0 = new Vector3(-1, -1, -1);
            Vector3 p1 = new Vector3(1, -1, -1);
            Vector3 p2 = new Vector3(-1, 1, -1);
            Vector3 p3 = new Vector3(1, 1, -1);
            Vector3 p4 = new Vector3(-1, -1, 1);
            Vector3 p5 = new Vector3(1, -1, 1);
            Vector3 p6 = new Vector3(-1, 1, 1);
            Vector3 p7 = new Vector3(1, 1, 1);

            AddFace(Vector3.UnitZ, p4, p5, p6, p7);
            AddFace(-Vector3.UnitZ, p1, p0, p3, p2);
            AddFace(Vector3.UnitY, p6, p7, p2, p3);
            AddFace(-Vector3.UnitY, p0, p1, p4, p5);
            AddFace(Vector3.UnitX, p5, p1, p7, p3);
            AddFace(-Vector3.UnitX, p0, p4, p2, p6);

            return verts.ToArray();
        }

        private int[] GetCubeIndices()
        {
            var indices = new List<int>();
            for (int i = 0; i < 6; i++)
            {
                int baseIdx = i * 4;
                indices.Add(baseIdx + 0); indices.Add(baseIdx + 1); indices.Add(baseIdx + 2);
                indices.Add(baseIdx + 1); indices.Add(baseIdx + 3); indices.Add(baseIdx + 2);
            }
            return indices.ToArray();
        }
    }
}