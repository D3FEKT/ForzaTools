using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using ForzaTools.Bundles;
using ForzaTools.Bundles.Blobs;
using ForzaTools.Bundles.Metadata;
using ForzaTools.Shared;

namespace ForzaTools.ForzaAnalyzer.Services
{
    public class ModelBuilderService
    {
        public void BuildTestCube(string outputPath)
        {
            // 1. Define Cube Geometry (24 Vertices for Flat Shading)
            var vertices = GetCubeVertices();
            var indices = GetCubeIndices();

            // 2. Initialize Bundle
            var bundle = new Bundle
            {
                VersionMajor = 1,
                VersionMinor = 9 // FH4/FH5 Standard
            };

            // IDs for linkage
            int indexBuffId = 1000;
            int vertexBuffId = 2000;
            // Layout is linked by index in blob list, not ID

            // 3. Create Blobs

            // A. Index Buffer
            var indexBlob = CreateIndexBuffer(indices);
            AddIdentifier(indexBlob, (uint)indexBuffId);
            bundle.Blobs.Add(indexBlob);

            // B. Vertex Layout (Position, Normal, TexCoord)
            var layoutBlob = CreateStandardLayout();
            AddIdentifier(layoutBlob, 3000); // ID optional for layout but good practice
            bundle.Blobs.Add(layoutBlob);

            // C. Vertex Buffer
            var vertexBlob = CreateVertexBuffer(vertices);
            AddIdentifier(vertexBlob, (uint)vertexBuffId);
            bundle.Blobs.Add(vertexBlob);

            // D. Mesh Blob
            var meshBlob = new MeshBlob
            {
                VersionMajor = 1,
                VersionMinor = 9,

                // Linkages
                VertexLayoutIndex = bundle.Blobs.IndexOf(layoutBlob),
                IndexBufferIndex = indexBuffId,
                IndexBufferDrawOffset = 0,
                IndexBufferOffset = 0,

                // Counts
                IndexCount = indices.Length,
                PrimCount = indices.Length / 3,

                // Vertex Info
                IndexedVertexOffset = 0,
                Is32BitIndices = true,
                Topology = 4, // TriangleList

                // Render Flags (Force Visible)
                LODFlags = 0xFFFF,
                IsOpaque = true,
                IsShadow = true,

                // Transform Defaults
                PositionScale = Vector4.One,
                PositionTranslate = Vector4.Zero
            };

            // Link Vertex Buffer
            meshBlob.VertexBuffers.Add(new MeshBlob.VertexBufferUsage
            {
                Index = vertexBuffId,
                InputSlot = 0,
                Offset = 0,
                Stride = 32 // 12+12+8
            });

            // Mesh Metadata
            meshBlob.Metadatas.Add(new NameMetadata { Name = "TestCube", Version = 0 });
            meshBlob.Metadatas.Add(CalculateBoundingBox(vertices));

            bundle.Blobs.Add(meshBlob);

            // E. Model Blob (Root)
            var modelBlob = new ModelBlob
            {
                VersionMajor = 1,
                VersionMinor = 9,
                MeshCount = 1,
                BuffersCount = 2,
                VertexLayoutCount = 1,
                MaterialCount = 0,
                HasLOD = false,
                LODFlags = 0xFFFF
            };
            // ModelBlob must be first usually
            bundle.Blobs.Insert(0, modelBlob);

            // 4. Write to File
            using (var fs = new FileStream(outputPath, FileMode.Create))
            {
                bundle.CreateModelBin(fs);
            }
        }

        // --- Helpers ---

        private IndexBufferBlob CreateIndexBuffer(int[] indices)
        {
            var blob = new IndexBufferBlob();
            // Create jagged array: 1 element per index (simplest for serializer)
            var data = new byte[indices.Length][];
            for (int i = 0; i < indices.Length; i++)
                data[i] = BitConverter.GetBytes(indices[i]);

            blob.Header = new BufferHeader
            {
                Length = indices.Length,
                Size = indices.Length * 4,
                Stride = 4,
                NumElements = 1,
                Format = DXGI_FORMAT.DXGI_FORMAT_R32_UINT,
                Data = data
            };
            return blob;
        }

        private VertexBufferBlob CreateVertexBuffer(Vertex[] vertices)
        {
            var blob = new VertexBufferBlob();
            int stride = 32;
            var data = new byte[vertices.Length][];

            for (int i = 0; i < vertices.Length; i++)
            {
                var ms = new MemoryStream();
                using (var bw = new BinaryWriter(ms))
                {
                    var v = vertices[i];
                    bw.Write(v.Position.X); bw.Write(v.Position.Y); bw.Write(v.Position.Z);
                    bw.Write(v.Normal.X); bw.Write(v.Normal.Y); bw.Write(v.Normal.Z);
                    bw.Write(v.UV.X); bw.Write(v.UV.Y);
                }
                data[i] = ms.ToArray();
            }

            blob.Header = new BufferHeader
            {
                Length = vertices.Length,
                Size = vertices.Length * stride,
                Stride = (ushort)stride,
                NumElements = (byte)vertices.Length,
                Data = data
            };
            return blob;
        }

        private VertexLayoutBlob CreateStandardLayout()
        {
            var blob = new VertexLayoutBlob();
            blob.SemanticNames = new List<string> { "POSITION", "NORMAL", "TEXCOORD" };

            blob.Elements.Add(new D3D12_INPUT_LAYOUT_DESC
            {
                SemanticNameIndex = 0,
                Format = DXGI_FORMAT.DXGI_FORMAT_R32G32B32_FLOAT,
                InputSlot = 0,
                AlignedByteOffset = 0
            });
            blob.Elements.Add(new D3D12_INPUT_LAYOUT_DESC
            {
                SemanticNameIndex = 1,
                Format = DXGI_FORMAT.DXGI_FORMAT_R32G32B32_FLOAT,
                InputSlot = 0,
                AlignedByteOffset = 12
            });
            blob.Elements.Add(new D3D12_INPUT_LAYOUT_DESC
            {
                SemanticNameIndex = 2,
                Format = DXGI_FORMAT.DXGI_FORMAT_R32G32_FLOAT,
                InputSlot = 0,
                AlignedByteOffset = 24
            });

            // Packed Formats (Required for some games)
            blob.PackedFormats = new List<DXGI_FORMAT> {
                DXGI_FORMAT.DXGI_FORMAT_R32G32B32_FLOAT,
                DXGI_FORMAT.DXGI_FORMAT_R32G32B32_FLOAT,
                DXGI_FORMAT.DXGI_FORMAT_R32G32_FLOAT
            };

            return blob;
        }

        private void AddIdentifier(BundleBlob blob, uint id)
        {
            blob.Metadatas.Add(new IdentifierMetadata { Id = id, Version = 0 });
        }

        private BoundaryBoxMetadata CalculateBoundingBox(Vertex[] verts)
        {
            Vector3 min = new Vector3(float.MaxValue);
            Vector3 max = new Vector3(float.MinValue);
            foreach (var v in verts)
            {
                min = Vector3.Min(min, v.Position);
                max = Vector3.Max(max, v.Position);
            }
            return new BoundaryBoxMetadata { Min = min, Max = max };
        }

        // --- Geometry Data ---

        private struct Vertex { public Vector3 Position; public Vector3 Normal; public Vector2 UV; }

        private Vertex[] GetCubeVertices()
        {
            // 24 vertices for hard edges
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

            AddFace(Vector3.UnitZ, p4, p5, p6, p7); // Front
            AddFace(-Vector3.UnitZ, p1, p0, p3, p2); // Back
            AddFace(Vector3.UnitY, p6, p7, p2, p3); // Top
            AddFace(-Vector3.UnitY, p0, p1, p4, p5); // Bottom
            AddFace(Vector3.UnitX, p5, p1, p7, p3); // Right
            AddFace(-Vector3.UnitX, p0, p4, p2, p6); // Left

            return verts.ToArray();
        }

        private int[] GetCubeIndices()
        {
            // 6 faces, 2 triangles each, 4 verts per face (0,1,2, 2,1,3 pattern for strip-like or 0,1,2 1,3,2)
            // Using standard Quad -> Tri pattern (0,1,2, 1,3,2) relative to face start
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