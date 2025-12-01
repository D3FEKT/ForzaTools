using Syroot.BinaryData;
using System;
using System.Collections.Generic;

namespace ForzaTools.Bundles.Blobs;

public class ShaderParameterMappingBlob : BundleBlob
{
    public List<MappingEntry> Mappings { get; set; } = new();

    public override void ReadBlobData(BinaryStream bs)
    {
        ushort count = 0;
        if (IsAtLeastVersion(3, 1))
            count = bs.ReadUInt16();
        else
            count = bs.Read1Byte();

        for (int i = 0; i < count; i++)
        {
            var entry = new MappingEntry();

            if (IsAtLeastVersion(2, 0))
            {
                entry.NameHash = bs.ReadUInt32();
                entry.IdOrOffset = bs.ReadUInt16();

                if (IsAtLeastVersion(3, 0))
                    entry.Guid = new Guid(bs.ReadBytes(16));
            }
            else
            {
                entry.Name = bs.ReadString(StringCoding.VariableByteCount);
                entry.IdOrOffset = bs.Read1Byte();
            }

            Mappings.Add(entry);
        }
    }

    public override void SerializeBlobData(BinaryStream bs)
    {
        if (IsAtLeastVersion(3, 1))
            bs.WriteUInt16((ushort)Mappings.Count);
        else
            bs.WriteByte((byte)Mappings.Count);

        foreach (var entry in Mappings)
        {
            if (IsAtLeastVersion(2, 0))
            {
                bs.WriteUInt32(entry.NameHash);
                bs.WriteUInt16((ushort)entry.IdOrOffset);

                if (IsAtLeastVersion(3, 0))
                    bs.WriteBytes(entry.Guid.ToByteArray());
            }
            else
            {
                bs.WriteString(entry.Name, StringCoding.VariableByteCount);
                bs.WriteByte((byte)entry.IdOrOffset);
            }
        }
    }
}

public class MappingEntry
{
    public string Name { get; set; } // v1.0 only
    public uint NameHash { get; set; } // v2.0+
    public int IdOrOffset { get; set; }
    public Guid Guid { get; set; } // v3.0+
}