using Syroot.BinaryData;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;

namespace ForzaTools.Bundles.Blobs;

public class MeshBlob : BundleBlob
{
    // v1.9 Material IDs
    public short[] MaterialIds { get; set; }
    public short MaterialId { get; set; }
    public short RigidBoneIndex { get; set; }
    public byte LODLevel1 { get; set; }
    public byte LODLevel2 { get; set; }

    public ushort LODFlags { get; set; }

    // Helpers for UI binding of LOD Flags
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
    public byte MorphWeightsCount { get; set; }
    public bool IsMorphDamage { get; set; }
    public bool Is32BitIndices { get; set; }
    public ushort Topology { get; set; }
    public int IndexBufferIndex { get; set; }
    public int IndexBufferOffset { get; set; }
    public int IndexBufferDrawOffset { get; set; }
    public int IndexedVertexOffset { get; set; }
    public int IndexCount { get; set; }
    public int PrimCount { get; set; }
    public float ACMR { get; set; }
    public uint ReferencedVertexCount { get; set; }
    public int VertexLayoutIndex { get; set; }
    public List<VertexBufferUsage> VertexBuffers { get; set; } = new();
    public int MorphDataBufferIndex { get; set; }
    public int SkinningDataBufferIndex { get; set; }
    public int[] ConstantBufferIndices { get; set; }
    public uint SourceMeshIndex { get; set; }
    public Vector4[] TexCoordTransforms { get; set; }
    public Vector4 PositionScale { get; set; }
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
        if (IsAtLeastVersion(1, 9)) MaterialIds = bs.ReadInt16s(4);
        else MaterialId = bs.ReadInt16();

        RigidBoneIndex = bs.ReadInt16();
        LODFlags = bs.ReadUInt16();
        LODLevel1 = bs.Read1Byte();
        LODLevel2 = bs.Read1Byte();

        ushort bucketFlagsRaw = bs.ReadUInt16();
        IsOpaque = (bucketFlagsRaw & 1) != 0;
        IsDecal = (bucketFlagsRaw & 2) != 0;
        IsTransparent = (bucketFlagsRaw & 4) != 0;
        IsShadow = (bucketFlagsRaw & 8) != 0;
        IsNotShadow = (bucketFlagsRaw & 16) != 0;
        IsAlphaToCoverage = (bucketFlagsRaw & 32) != 0;

        BucketOrder = bs.Read1Byte();

        if (IsAtLeastVersion(1, 2)) { SkinningElementsCount = bs.Read1Byte(); MorphWeightsCount = bs.Read1Byte(); }
        if (IsAtLeastVersion(1, 3)) IsMorphDamage = bs.ReadBoolean();

        Is32BitIndices = bs.ReadBoolean();
        Topology = bs.ReadUInt16();
        IndexBufferIndex = bs.ReadInt32();
        IndexBufferOffset = bs.ReadInt32();
        IndexBufferDrawOffset = bs.ReadInt32();
        IndexedVertexOffset = bs.ReadInt32();
        IndexCount = bs.ReadInt32();
        PrimCount = bs.ReadInt32();

        if (IsAtLeastVersion(1, 6)) { ACMR = bs.ReadSingle(); ReferencedVertexCount = bs.ReadUInt32(); }

        VertexLayoutIndex = bs.ReadInt32();

        int vbCount = bs.ReadInt32();
        for (int i = 0; i < vbCount; i++)
        {
            VertexBuffers.Add(new VertexBufferUsage
            {
                Index = bs.ReadInt32(),
                InputSlot = bs.ReadUInt32(),
                Stride = bs.ReadUInt32(),
                Offset = bs.ReadUInt32()
            });
        }

        if (IsAtLeastVersion(1, 4)) { MorphDataBufferIndex = bs.ReadInt32(); SkinningDataBufferIndex = bs.ReadInt32(); }

        int cbCount = bs.ReadInt32();
        if (cbCount > 0) ConstantBufferIndices = bs.ReadInt32s(cbCount);
        else ConstantBufferIndices = Array.Empty<int>();

        if (IsAtLeastVersion(1, 1)) SourceMeshIndex = bs.ReadUInt32();

        if (IsAtLeastVersion(1, 5)) TexCoordTransforms = MemoryMarshal.Cast<byte, Vector4>(bs.ReadBytes(0x10 * 5)).ToArray();

        if (IsAtLeastVersion(1, 8))
        {
            PositionScale = MemoryMarshal.Read<Vector4>(bs.ReadBytes(0x10));
            PositionTranslate = MemoryMarshal.Read<Vector4>(bs.ReadBytes(0x10));
        }
    }

    public override void SerializeBlobData(BinaryStream bs)
    {
        if (IsAtLeastVersion(1, 9)) bs.WriteInt16s(MaterialIds);
        else bs.WriteInt16(MaterialId);

        bs.WriteInt16(RigidBoneIndex);
        bs.WriteUInt16(LODFlags);
        bs.WriteByte(LODLevel1);
        bs.WriteByte(LODLevel2);

        ushort bucketFlagsRaw = 0;
        if (IsOpaque) bucketFlagsRaw |= 1;
        if (IsDecal) bucketFlagsRaw |= 2;
        if (IsTransparent) bucketFlagsRaw |= 4;
        if (IsShadow) bucketFlagsRaw |= 8;
        if (IsNotShadow) bucketFlagsRaw |= 16;
        if (IsAlphaToCoverage) bucketFlagsRaw |= 32;
        bs.WriteUInt16(bucketFlagsRaw);

        bs.WriteByte(BucketOrder);

        if (IsAtLeastVersion(1, 2)) { bs.WriteByte(SkinningElementsCount); bs.WriteByte(MorphWeightsCount); }
        if (IsAtLeastVersion(1, 3)) bs.WriteBoolean(IsMorphDamage);

        bs.WriteBoolean(Is32BitIndices);
        bs.WriteUInt16(Topology);
        bs.WriteInt32(IndexBufferIndex);
        bs.WriteInt32(IndexBufferOffset);
        bs.WriteInt32(IndexBufferDrawOffset);
        bs.WriteInt32(IndexedVertexOffset);
        bs.WriteInt32(IndexCount);
        bs.WriteInt32(PrimCount);

        if (IsAtLeastVersion(1, 6)) { bs.WriteSingle(ACMR); bs.WriteUInt32(ReferencedVertexCount); }

        bs.WriteInt32(VertexLayoutIndex);

        bs.WriteInt32(VertexBuffers.Count);
        foreach (var vb in VertexBuffers)
        {
            bs.WriteInt32(vb.Index);
            bs.WriteUInt32(vb.InputSlot);
            bs.WriteUInt32(vb.Stride);
            bs.WriteUInt32(vb.Offset);
        }

        if (IsAtLeastVersion(1, 4)) { bs.WriteInt32(MorphDataBufferIndex); bs.WriteInt32(SkinningDataBufferIndex); }

        bs.WriteInt32(ConstantBufferIndices.Length);
        bs.WriteInt32s(ConstantBufferIndices);

        if (IsAtLeastVersion(1, 1)) bs.WriteUInt32(SourceMeshIndex);
        if (IsAtLeastVersion(1, 5)) bs.Write(MemoryMarshal.Cast<Vector4, byte>(TexCoordTransforms));
        if (IsAtLeastVersion(1, 8)) { bs.WriteVector4(PositionScale); bs.WriteVector4(PositionTranslate); }
    }
}