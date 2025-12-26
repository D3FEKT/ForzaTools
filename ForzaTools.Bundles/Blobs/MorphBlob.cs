using Syroot.BinaryData;
using System.Collections.Generic;

namespace ForzaTools.Bundles.Blobs;

public class MorphBlob : BundleBlob
{
    public List<string> Strings { get; set; } = new();

    public override void ReadBlobData(BinaryStream bs)
    {
        short count = bs.ReadInt16();
        for (int i = 0; i < count; i++)
        {
            Strings.Add(bs.ReadString(StringCoding.Int32CharCount));
        }
    }

    public override void SerializeBlobData(BinaryStream bs)
    {
        bs.WriteInt16((short)Strings.Count);
        foreach (var s in Strings)
        {
            bs.WriteString(s, StringCoding.Int32CharCount);
        }
    }

    public override void CreateModelBinBlobData(BinaryStream bs)
    {
        bs.WriteInt16((short)Strings.Count);
        foreach (var s in Strings)
        {
            bs.WriteString(s, StringCoding.Int32CharCount);
        }
    }
}