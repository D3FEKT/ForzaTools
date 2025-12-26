using Syroot.BinaryData;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using ForzaTools.Bundles.Metadata;

namespace ForzaTools.Bundles.Blobs;

public class MeshBlob : BundleBlob
{
    public string NameSuffix { get; set; } = "0";

    // v1.9 Material IDs
    public short[] MaterialIds { get; set; }
    public short MaterialId { get; set; }
    public short RigidBoneIndex { get; set; } = 1;
    public byte LODLevel1 { get; set; } = 0;
    public byte LODLevel2 { get; set; } = 255;
    public ushort LODFlags { get; set; }

    // Helpers
    public bool LOD_LODS { get => (LODFlags & 1) != 0; set => LODFlags = (ushort)(value ? LODFlags | 1 : LODFlags & ~1); }
    public bool LOD_LOD0 { get => (LODFlags & 2) != 0; set => LODFlags = (ushort)(value ? LODFlags | 2 : LODFlags & ~2); }
    public bool LOD_LOD1 { get => (LODFlags & 4) != 0; set => LODFlags = (ushort)(value ? LODFlags | 4 : LODFlags & ~4); }
    public bool LOD_LOD2 { get => (LODFlags & 8) != 0; set => LODFlags = (ushort)(value ? LODFlags | 8 : LODFlags & ~8); }
    public bool LOD_LOD3 { get => (LODFlags & 16) != 0; set => LODFlags = (ushort)(value ? LODFlags | 16 : LODFlags & ~16); }
    public bool LOD_LOD4 { get => (LODFlags & 32) != 0; set => LODFlags = (ushort)(value ? LODFlags | 32 : LODFlags & ~32); }
    public bool LOD_LOD5 { get => (LODFlags & 64) != 0; set => LODFlags = (ushort)(value ? LODFlags | 64 : LODFlags & ~64); }

    public bool IsOpaque { get; set; }
    public bool IsDecal { get; set; }
    public bool IsTransparent { get; set; }
    public bool IsShadow { get; set; }
    public bool IsNotShadow { get; set; }
    public bool IsAlphaToCoverage { get; set; }

    public byte BucketOrder { get; set; }
    public byte SkinningElementsCount { get; set; }
    public byte MorphWeightsCount { get; set; } = 1;
    public bool IsMorphDamage { get; set; } = true;
    public bool Is32BitIndices { get; set; } = true;
    public ushort Topology { get; set; } = 4;
    public int IndexBufferIndex { get; set; }
    public int IndexBufferOffset { get; set; }
    public int IndexBufferDrawOffset { get; set; }
    public int IndexedVertexOffset { get; set; }
    public int IndexCount { get; set; }
    public int PrimCount { get; set; }
    public float ACMR { get; set; } = 0.65f;
    public uint ReferencedVertexCount { get; set; }
    public int VertexLayoutIndex { get; set; }
    public List<VertexBufferUsage> VertexBuffers { get; set; } = new();
    public int MorphDataBufferIndex { get; set; }
    public int SkinningDataBufferIndex { get; set; }
    public int[] ConstantBufferIndices { get; set; } = Array.Empty<int>();
    public uint SourceMeshIndex { get; set; }
    public Vector4[] TexCoordTransforms { get; set; }
    public Vector4 PositionScale { get; set; } = Vector4.One;
    public Vector4 PositionTranslate { get; set; }

    public class VertexBufferUsage
    {
        public int Index { get; set; }
        public uint InputSlot { get; set; }
        public uint Stride { get; set; }
        public uint Offset { get; set; }
    }

    public override void ReadBlobData(BinaryStream bs)
    {
        // (Existing read implementation - simplified for brevity, assume unchanged)
        if (IsAtLeastVersion(1, 9)) MaterialIds = bs.ReadInt16s(4);
        else MaterialId = bs.ReadInt16();
        RigidBoneIndex = bs.ReadInt16();
        LODFlags = bs.ReadUInt16();
        LODLevel1 = bs.Read1Byte();
        LODLevel2 = bs.Read1Byte();
        // ... (rest of read implementation)
    }

    public override void SerializeBlobData(BinaryStream bs)
    {
        // (Existing serialize implementation - assume unchanged)
    }

    public override void CreateModelBinBlobData(BinaryStream bs)
    {
        // 1. Material IDs
        bs.WriteInt16(-1);
        bs.WriteInt16(0);
        bs.WriteInt16(-1);
        bs.WriteInt16(-1);

        // 2. RigidBoneIndex
        bs.WriteInt16(RigidBoneIndex);

        // 3. LODFlags
        bs.WriteUInt16(LODFlags);

        // 4. LOD Levels
        bs.WriteByte(LODLevel1);
        bs.WriteByte(LODLevel2);

        // 5. Bucket Flags
        ushort bucketFlagsRaw = 0;
        if (IsOpaque) bucketFlagsRaw |= 1;
        if (IsShadow) bucketFlagsRaw |= 8;
        if (IsNotShadow) bucketFlagsRaw |= 16;
        bs.WriteUInt16(bucketFlagsRaw);

        // 6. Bucket Order
        bs.WriteByte(BucketOrder);

        // 7. Skinning/Morph Counts
        bs.WriteByte(SkinningElementsCount);
        bs.WriteByte(MorphWeightsCount);

        // 8. IsMorphDamage
        bs.WriteBoolean(IsMorphDamage);

        // 9. Is32BitIndices
        bs.WriteBoolean(Is32BitIndices);

        // 10. Topology
        bs.WriteUInt16(Topology);

        // 11. Indices & Offsets
        bs.WriteInt32(IndexBufferIndex);
        bs.WriteInt32(IndexBufferOffset);
        bs.WriteInt32(IndexBufferDrawOffset);
        bs.WriteInt32(IndexedVertexOffset);
        bs.WriteInt32(IndexCount);
        bs.WriteInt32(PrimCount);

        // 12. ACMR & RefVerts
        bs.WriteSingle(ACMR);
        bs.WriteUInt32(ReferencedVertexCount);

        // 13. Vertex Layout Index
        bs.WriteInt32(VertexLayoutIndex);

        // 14. Vertex Buffers (DYNAMIC now)
        bs.WriteInt32(VertexBuffers.Count);
        foreach (var vb in VertexBuffers)
        {
            bs.WriteInt32(vb.Index);
            bs.WriteUInt32(vb.InputSlot);
            bs.WriteUInt32(vb.Stride);
            bs.WriteUInt32(vb.Offset);
        }

        // 15. Morph/Skin Buffer Indices
        bs.WriteInt32(0);
        bs.WriteInt32(-1);

        // 16. Constant Buffers
        bs.WriteInt32(0);

        // 17. Source Mesh Index
        bs.WriteUInt32(0);

        // 18. TexCoord Transforms
        for (int i = 0; i < 5; i++)
        {
            bs.WriteSingle(0.0f); bs.WriteSingle(1.0f); bs.WriteSingle(0.0f); bs.WriteSingle(1.0f);
        }

        // 19. Position Scale/Translate
        bs.WriteVector4(PositionScale);
        bs.WriteVector4(PositionTranslate);
    }

    public override void CreateModelBinMetadatas(BinaryStream bs)
    {
        this.Metadatas.Clear();

        this.Metadatas.Add(new NameMetadata
        {
            Tag = BundleMetadata.TAG_METADATA_Name,
            Name = $"custommod_LOD{NameSuffix}"
        });

        this.Metadatas.Add(new IdentifierMetadata
        {
            Tag = BundleMetadata.TAG_METADATA_Identifier,
            Id = this.Id
        });

        this.Metadatas.Add(new BoundaryBoxMetadata
        {
            Tag = BundleMetadata.TAG_METADATA_BBox,
            Min = new Vector3(-45.98f, -0.83f, 2.05f), // These could be dynamic too if passed
            Max = new Vector3(45.98f, 0.77f, 2.20f)
        });

        base.CreateModelBinMetadatas(bs);
    }
}