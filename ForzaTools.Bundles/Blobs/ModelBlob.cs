using Syroot.BinaryData;
using System.Numerics;
using ForzaTools.Bundles.Metadata;

namespace ForzaTools.Bundles.Blobs;

public class ModelBlob : BundleBlob
{
    public ushort MeshCount { get; set; }
    public ushort BuffersCount { get; set; }
    public ushort VertexLayoutCount { get; set; }
    public ushort MaterialCount { get; set; }

    public bool HasLOD { get; set; }
    public sbyte MinLOD { get; set; }
    public sbyte MaxLOD { get; set; }
    public ushort LODFlags { get; set; }
    public byte DecompressFlags { get; set; }
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
        bs.Read1Byte();

        LODFlags = bs.ReadUInt16();

        if (IsAtLeastVersion(1, 2))
        {
            DecompressFlags = bs.Read1Byte();
            bs.Read1Byte();
        }
        if (IsAtLeastVersion(1, 3))
        {
            UnkV1_3 = bs.Read1Byte();
            bs.Read1Byte();
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
        bs.WriteByte(0);
        bs.WriteUInt16(LODFlags);

        if (IsAtLeastVersion(1, 2)) { bs.WriteByte(DecompressFlags); bs.WriteByte(0); }
        if (IsAtLeastVersion(1, 3)) { bs.WriteByte(UnkV1_3); bs.WriteByte(0); }
    }

    public override void CreateModelBinBlobData(BinaryStream bs)
    {
        // Same as Serialize but ensures values are written for new files
        bs.WriteUInt16(MeshCount);
        bs.WriteUInt16(BuffersCount);
        bs.WriteUInt16(VertexLayoutCount);
        bs.WriteUInt16(MaterialCount);
        bs.WriteByte((byte)(HasLOD ? 1 : 0));
        bs.WriteSByte(MinLOD);
        bs.WriteSByte(MaxLOD);
        bs.WriteByte(0);
        bs.WriteUInt16(LODFlags);

        // Always write decompression flags for new files if version >= 1.2
        bs.WriteByte(DecompressFlags);
        bs.WriteByte(0);
    }

    public override void CreateModelBinMetadatas(BinaryStream bs)
    {
        this.Metadatas.Clear();
        // Add BBox Metadata
        this.Metadatas.Add(new BoundaryBoxMetadata
        {
            Tag = BundleMetadata.TAG_METADATA_BBox,
            Min = new Vector3(-45.98f, -0.83f, 2.05f),
            Max = new Vector3(45.98f, 0.77f, 2.20f)
        });

        base.CreateModelBinMetadatas(bs);
    }
}