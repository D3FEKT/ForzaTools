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
        private readonly GeometryProcessingService _geometryService;

        public ModelBuilderService()
        {
            _geometryService = new GeometryProcessingService();
        }

        // --- 1. Process Geometry ---
        public ProcessedGeometry ProcessGeometry(GeometryInput input)
        {
            return _geometryService.ProcessGeometry(input);
        }

        // --- 2. Create Bundle In Memory ---
        public Bundle CreateBundleInMemory(ProcessedGeometry processed, string materialName)
        {
            var bundle = new Bundle
            {
                VersionMajor = 1,
                VersionMinor = 9
            };

            // --- Define Metadata IDs for Linking ---
            int idLink_LayoutFull = 0;
            int idLink_LayoutPos = -1;
            int idLink_IB = 0;
            int idLink_VB_Pos = -1;
            int idLink_VB_Norm = 0;

            // --- 0. Skeleton ---
            var skeletonBlob = new SkeletonBlob { Tag = Bundle.TAG_BLOB_Skeleton, VersionMajor = 1, VersionMinor = 0 };
            skeletonBlob.Bones.Add(new Bone { Name = "root", ParentId = -1, Matrix = Matrix4x4.Identity });
            bundle.Blobs.Add(skeletonBlob);

            // --- 1. Morph ---
            bundle.Blobs.Add(new MorphBlob { Tag = Bundle.TAG_BLOB_Morph, VersionMajor = 0, VersionMinor = 0 });

            // --- 2-7. Mesh Blobs ---
            for (int i = 0; i < 6; i++)
            {
                var meshBlob = new MeshBlob
                {
                    Tag = Bundle.TAG_BLOB_Mesh,
                    VersionMajor = 1,
                    VersionMinor = 9,
                    IndexBufferIndex = idLink_IB,
                    VertexLayoutIndex = idLink_LayoutFull,
                    IndexCount = processed.IndexData.Length,
                    PrimCount = processed.IndexData.Length / 3,
                    ReferencedVertexCount = (uint)processed.PositionData.Length,
                    IsOpaque = true,
                    IsNotShadow = true,
                    Is32BitIndices = true,
                    Topology = 4,
                    PositionScale = processed.PositionScale,
                    PositionTranslate = processed.PositionTranslate,
                    NameSuffix = i.ToString()
                };

                // Set LOD flags
                switch (i)
                {
                    case 0: meshBlob.LOD_LOD0 = true; break;
                    case 1: meshBlob.LOD_LOD1 = true; break;
                    case 2: meshBlob.LOD_LOD2 = true; break;
                    case 3: meshBlob.LOD_LOD3 = true; break;
                    case 4: meshBlob.LOD_LOD4 = true; break;
                    case 5: meshBlob.LOD_LOD5 = true; break;
                }

                // Buffer Usage
                meshBlob.VertexBuffers.Add(new MeshBlob.VertexBufferUsage { Index = idLink_VB_Pos, InputSlot = 0, Stride = 8, Offset = 0 });
                meshBlob.VertexBuffers.Add(new MeshBlob.VertexBufferUsage { Index = idLink_VB_Norm, InputSlot = 1, Stride = 40, Offset = 0 });

                meshBlob.Metadatas.Add(new NameMetadata { Tag = BundleMetadata.TAG_METADATA_Name, Name = $"custommod_LOD{i}" });
                meshBlob.Metadatas.Add(new BoundaryBoxMetadata { Tag = BundleMetadata.TAG_METADATA_BBox, Min = processed.BoundingBoxMin, Max = processed.BoundingBoxMax });

                bundle.Blobs.Add(meshBlob);
            }

            // --- 8. Material ---
            byte[] matData = MaterialLibrary.GetMaterialData(materialName);
            var materialBlob = new MaterialBlob
            {
                Tag = Bundle.TAG_BLOB_MaterialInstance,
                VersionMajor = 1,
                VersionMinor = 0,
                CustomBlobData = matData
            };
            materialBlob.Metadatas.Add(new NameMetadata { Tag = BundleMetadata.TAG_METADATA_Name, Name = materialName });
            materialBlob.Metadatas.Add(new IdentifierMetadata { Tag = BundleMetadata.TAG_METADATA_Identifier, Id = 0 });
            bundle.Blobs.Add(materialBlob);

            // --- 9. Index Buffer ---
            byte[] flattenedIndices = processed.IndexData.SelectMany(b => b).ToArray();
            var ibBlob = new IndexBufferBlob
            {
                Tag = Bundle.TAG_BLOB_IndexBuffer,
                VersionMajor = 1,
                VersionMinor = 0,
                Data = flattenedIndices
            };
            ibBlob.Header = new BufferHeader
            {
                Length = processed.IndexData.Length,
                Stride = 4,
                NumElements = 1,
                Format = DXGI_FORMAT.DXGI_FORMAT_R32_UINT,
                Data = processed.IndexData
            };
            ibBlob.Metadatas.Add(new IdentifierMetadata { Tag = BundleMetadata.TAG_METADATA_Identifier, Id = unchecked((uint)idLink_IB) });
            bundle.Blobs.Add(ibBlob);

            // --- 10. VLay (Full) ---
            bundle.Blobs.Add(CreateLayoutFull(idLink_LayoutFull));

            // --- 11. VLay (Pos Only) ---
            bundle.Blobs.Add(CreateLayoutPosOnly(idLink_LayoutPos));

            // --- 12. VB Position ---
            byte[] flattenedPos = processed.PositionData.SelectMany(b => b).ToArray();
            var vbPos = new VertexBufferBlob
            {
                Tag = Bundle.TAG_BLOB_VertexBuffer,
                VersionMajor = 1,
                VersionMinor = 0,
                Data = flattenedPos
            };
            vbPos.Header = new BufferHeader
            {
                Length = processed.PositionData.Length,
                Stride = 8,
                NumElements = 1,
                Format = DXGI_FORMAT.DXGI_FORMAT_R16G16B16A16_SNORM,
                Data = processed.PositionData
            };
            vbPos.Metadatas.Add(new IdentifierMetadata { Tag = BundleMetadata.TAG_METADATA_Identifier, Id = unchecked((uint)idLink_VB_Pos) });
            bundle.Blobs.Add(vbPos);

            // --- 13. VB Norm/UV ---
            byte[] flattenedNorm = processed.NormalUVData.SelectMany(b => b).ToArray();
            var vbNorm = new VertexBufferBlob
            {
                Tag = Bundle.TAG_BLOB_VertexBuffer,
                VersionMajor = 1,
                VersionMinor = 0,
                Data = flattenedNorm
            };
            vbNorm.Header = new BufferHeader
            {
                Length = processed.NormalUVData.Length,
                Stride = 40,
                NumElements = 1,
                Format = (DXGI_FORMAT)37,
                Data = processed.NormalUVData
            };
            vbNorm.Metadatas.Add(new IdentifierMetadata { Tag = BundleMetadata.TAG_METADATA_Identifier, Id = unchecked((uint)idLink_VB_Norm) });
            bundle.Blobs.Add(vbNorm);

            // --- 14. Model ---
            var modelBlob = new ModelBlob
            {
                Tag = Bundle.TAG_BLOB_Model,
                VersionMajor = 1,
                VersionMinor = 2,
                MeshCount = 6,
                BuffersCount = 3,
                VertexLayoutCount = 2,
                MaterialCount = 1,
                HasLOD = true,
                MaxLOD = 5,
                LODFlags = 0xFFFF
            };
            modelBlob.Metadatas.Add(new BoundaryBoxMetadata { Tag = BundleMetadata.TAG_METADATA_BBox, Min = processed.BoundingBoxMin, Max = processed.BoundingBoxMax });
            bundle.Blobs.Add(modelBlob);

            return bundle;
        }

        // --- 3. Update Material In Bundle ---
        public void UpdateMaterialInBundle(Bundle bundle, string materialName)
        {
            var matBlob = bundle.Blobs.FirstOrDefault(b => b.Tag == Bundle.TAG_BLOB_MaterialInstance) as MaterialBlob;
            if (matBlob == null) return;

            // Update Blob Data
            matBlob.CustomBlobData = MaterialLibrary.GetMaterialData(materialName);

            // Update Name Metadata
            var nameMeta = matBlob.Metadatas.FirstOrDefault(m => m.Tag == BundleMetadata.TAG_METADATA_Name) as NameMetadata;
            if (nameMeta != null)
            {
                nameMeta.Name = materialName;
            }
            else
            {
                matBlob.Metadatas.Add(new NameMetadata { Tag = BundleMetadata.TAG_METADATA_Name, Name = materialName });
            }
        }

        // --- 4. Save Bundle ---
        public void SaveBundle(Bundle bundle, string outputPath)
        {
            using (var fs = new FileStream(outputPath, FileMode.Create))
            {
                bundle.CreateModelBin(fs);
            }
        }

        // Wrapper for backward compatibility (re-uses new methods)
        public void BuildCompatibleModelBin(string outputPath, GeometryInput input, string materialName)
        {
            var processed = ProcessGeometry(input);
            var bundle = CreateBundleInMemory(processed, materialName);
            SaveBundle(bundle, outputPath);
        }

        private VertexLayoutBlob CreateLayoutFull(int id)
        {
            var blob = new VertexLayoutBlob { Tag = Bundle.TAG_BLOB_VertexLayout, VersionMajor = 1, VersionMinor = 1 };

            // 1. Semantics
            blob.SemanticNames.AddRange(new[] { "POSITION", "NORMAL", "TEXCOORD", "TANGENT", "COLOR" });

            // 2. Elements (Updated to match hex dump requirement)
            blob.Elements.Add(CreateElement(0, 0, 0, 0, 13)); // Pos
            blob.Elements.Add(CreateElement(1, 0, 1, 0, 37)); // Normal

            // Note: Updated to match "Hex Dump" requirement from previous turn:
            // 5 TexCoords, 3 Tangents, 1 Color

            // 5 TexCoords (Slot 1, Fmt 35)
            for (int i = 0; i < 5; i++)
                blob.Elements.Add(CreateElement(2, (short)i, 1, 0, 35));

            // 3 Tangents (Slot 1, Fmt 24 - DXGI_FORMAT_R10G10B10A2_UNORM)
            for (int i = 0; i < 3; i++)
                blob.Elements.Add(CreateElement(3, (short)i, 1, 0, 24));

            // 1 Color (Slot 1, Fmt 28 - DXGI_FORMAT_R8G8B8A8_UNORM)
            blob.Elements.Add(CreateElement(4, 0, 1, 0, 28));

            // 3. Formats
            int[] formats = { 49, 52, 46, 46, 46, 46, 46, 48, 48, 48, 22 };
            foreach (var f in formats) blob.PackedFormats.Add((DXGI_FORMAT)f);

            // 4. Flags
            blob.Flags = 0x000004FF;

            // 5. Metadata ID
            blob.Metadatas.Add(new IdentifierMetadata { Tag = BundleMetadata.TAG_METADATA_Identifier, Id = unchecked((uint)id) });

            return blob;
        }

        private VertexLayoutBlob CreateLayoutPosOnly(int id)
        {
            var blob = new VertexLayoutBlob { Tag = Bundle.TAG_BLOB_VertexLayout, VersionMajor = 1, VersionMinor = 1 };
            blob.SemanticNames.Add("POSITION");
            blob.Elements.Add(CreateElement(0, 0, 0, 0, 13));
            blob.PackedFormats.Add((DXGI_FORMAT)49);
            blob.Flags = 0;
            blob.Metadatas.Add(new IdentifierMetadata { Tag = BundleMetadata.TAG_METADATA_Identifier, Id = unchecked((uint)id) });
            return blob;
        }

        private D3D12_INPUT_LAYOUT_DESC CreateElement(short nameIdx, short semIdx, short slot, short slotClass, int format, int offset = -1, int step = 0)
        {
            return new D3D12_INPUT_LAYOUT_DESC
            {
                SemanticNameIndex = nameIdx,
                SemanticIndex = semIdx,
                InputSlot = slot,
                InputSlotClass = slotClass,
                Format = (DXGI_FORMAT)format,
                AlignedByteOffset = offset,
                InstanceDataStepRate = step
            };
        }
    }
}