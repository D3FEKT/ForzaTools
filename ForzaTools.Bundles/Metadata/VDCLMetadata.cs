using Syroot.BinaryData;
using System.Collections.Generic;

namespace ForzaTools.Bundles.Metadata;

public class VDCLMetadata : BundleMetadata
{
    public List<VDCLEntry> Entries { get; set; } = new();

    public override void ReadMetadataData(BinaryStream bs)
    {
        if (Version >= 2)
        {
            int count = 1;
            if (Version >= 3)
                count = bs.ReadInt32();

            for (int i = 0; i < count; i++)
            {
                Entries.Add(new VDCLEntry
                {
                    NameHash = bs.ReadUInt32(),
                    Unk = bs.ReadUInt32()
                });
            }
        }
    }

    public override void SerializeMetadataData(BinaryStream bs)
    {
        if (Version >= 2)
        {
            if (Version >= 3)
                bs.WriteInt32(Entries.Count);

            foreach (var entry in Entries)
            {
                bs.WriteUInt32(entry.NameHash);
                bs.WriteUInt32(entry.Unk);
            }
        }
    }

    public override void CreateModelBinMetadataData(BinaryStream bs)
    {
        if (Version >= 2)
        {
            var safeEntries = Entries ?? new List<VDCLEntry>();
            if (Version >= 3)
                bs.WriteInt32(safeEntries.Count);

            foreach (var entry in safeEntries)
            {
                bs.WriteUInt32(entry.NameHash);
                bs.WriteUInt32(entry.Unk);
            }
        }
    }
}

public class VDCLEntry
{
    public uint NameHash { get; set; }
    public uint Unk { get; set; }
}