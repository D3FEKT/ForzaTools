using Syroot.BinaryData;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.InteropServices;

namespace ForzaTools.Bundles.Blobs;

public class SkeletonBlob : BundleBlob
{
    public List<Bone> Bones { get; set; } = new List<Bone>();
    public byte[] UnkV1_0 { get; set; }

    public override void ReadBlobData(BinaryStream bs)
    {
        ushort numBones = bs.ReadUInt16();
        for (int i = 0; i < numBones; i++)
        {
            Bone bone = new Bone();
            bone.Name = bs.ReadString(StringCoding.Int32CharCount);
            bone.ParentId = bs.ReadInt16();
            bone.FirstChildIndex = bs.ReadInt16();
            bone.NextIndex = bs.ReadInt16();
            bone.Matrix = MemoryMarshal.Read<Matrix4x4>(bs.ReadBytes((sizeof(float) * 4) * 4));
            Bones.Add(bone);
        }

        if (IsAtLeastVersion(1, 0))
        {
            uint unkLength = bs.ReadUInt32();
            if (unkLength > 0)
            {
                UnkV1_0 = bs.ReadBytes((int)unkLength);
            }
        }
    }

    public override void SerializeBlobData(BinaryStream bs)
    {
        bs.WriteUInt16((ushort)Bones.Count);
        for (int i = 0; i < Bones.Count; i++)
        {
            Bone bone = Bones[i];
            bs.WriteString(bone.Name, StringCoding.Int32CharCount);
            bs.WriteInt16(bone.ParentId);
            bs.WriteInt16(bone.FirstChildIndex);
            bs.WriteInt16(bone.NextIndex);
            bs.WriteMatrix4x4(bone.Matrix);
        }

        if (IsAtLeastVersion(1, 0))
        {
            bs.WriteUInt32((uint)(UnkV1_0?.Length ?? 0));
            if (UnkV1_0 != null && UnkV1_0.Length > 0)
            {
                bs.WriteBytes(UnkV1_0);
            }
        }
    }

    public override void CreateModelBinBlobData(BinaryStream bs)
    {
        var safeBones = Bones ?? new List<Bone>();
        bs.WriteUInt16((ushort)safeBones.Count);

        foreach (var bone in safeBones)
        {
            bs.WriteString(bone.Name ?? "", StringCoding.Int32CharCount);
            bs.WriteInt16(bone.ParentId);
            bs.WriteInt16(bone.FirstChildIndex);
            bs.WriteInt16(bone.NextIndex);
            bs.WriteMatrix4x4(bone.Matrix);
        }

        if (IsAtLeastVersion(1, 0))
        {
            bs.WriteUInt32((uint)(UnkV1_0?.Length ?? 0));
            if (UnkV1_0 != null && UnkV1_0.Length > 0)
            {
                bs.WriteBytes(UnkV1_0);
            }
        }
    }
}

public class Bone
{
    public string Name { get; set; }
    public short ParentId { get; set; }
    public short FirstChildIndex { get; set; }
    public short NextIndex { get; set; }
    public Matrix4x4 Matrix { get; set; }

    public override string ToString() => $"{Name} (Parent: {ParentId})";
}