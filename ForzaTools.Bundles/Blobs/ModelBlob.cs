using Syroot.BinaryData;
using System;
using System.Collections.Generic;

namespace ForzaTools.Bundles.Blobs;

public class ModelBlob : BundleBlob
{
    public ushort MeshCount { get; set; }
    public ushort BuffersCount { get; set; }
    public ushort VertexLayoutCount { get; set; }
    public ushort MaterialCount { get; set; }

    // ModelStats breakdown
    public bool HasLOD { get; set; }
    public sbyte MinLOD { get; set; }
    public sbyte MaxLOD { get; set; }
    public ushort LODFlags { get; set; }

    // v1.2
    public byte DecompressFlags { get; set; }

    // v1.3
    public byte UnkV1_3 { get; set; }

    public override void ReadBlobData(BinaryStream bs)
    {
        MeshCount = bs.ReadUInt16();
        BuffersCount = bs.ReadUInt16();
        VertexLayoutCount = bs.ReadUInt16();
        MaterialCount = bs.ReadUInt16();

        HasLOD = bs.Read1Byte() != 0;
        MinLOD = bs.ReadSByte();
        MaxLOD = bs.ReadSByte();
        bs.Read1Byte(); // Padding

        LODFlags = bs.ReadUInt16();

        if (IsAtLeastVersion(1, 2))
        {
            DecompressFlags = bs.Read1Byte();
            bs.Read1Byte(); // Padding
        }

        if (IsAtLeastVersion(1, 3))
        {
            UnkV1_3 = bs.Read1Byte();
            bs.Read1Byte(); // Padding
        }
    }

    public override void SerializeBlobData(BinaryStream bs)
    {
        bs.WriteUInt16(MeshCount);
        bs.WriteUInt16(BuffersCount);
        bs.WriteUInt16(VertexLayoutCount);
        bs.WriteUInt16(MaterialCount);

        bs.WriteByte((byte)(HasLOD ? 1 : 0));
        bs.WriteSByte(MinLOD);
        bs.WriteSByte(MaxLOD);
        bs.WriteByte(0); // Padding

        bs.WriteUInt16(LODFlags);

        if (IsAtLeastVersion(1, 2))
        {
            bs.WriteByte(DecompressFlags);
            bs.WriteByte(0); // Padding
        }

        if (IsAtLeastVersion(1, 3))
        {
            bs.WriteByte(UnkV1_3);
            bs.WriteByte(0); // Padding
        }
    }
}