using Syroot.BinaryData;
using ForzaTools.Bundles.Metadata;

namespace ForzaTools.Bundles.Blobs;

public class VertexBufferBlob : BundleBlob
{
    public BufferHeader Header { get; set; } = new();

    public override void ReadBlobData(BinaryStream bs)
    {
        Header.Read(bs, VersionMajor, VersionMinor);
    }

    public override void SerializeBlobData(BinaryStream bs)
    {
        Header.Serialize(bs, VersionMajor, VersionMinor);
    }

    public override void CreateModelBinBlobData(BinaryStream bs)
    {
        // Calculate count
        int count = 0;
        if (Header.Stride > 0)
            count = Data.Length / (int)Header.Stride;

        bs.WriteInt32(count);
        bs.WriteInt32(Data.Length);
        bs.WriteUInt16((ushort)Header.Stride);

        // v1.0 Header Fields
        bs.WriteByte(1); // NumElements
        bs.WriteByte(0); // Padding
        bs.WriteInt32((int)Header.Format);

        // Write the raw vertex data
        bs.Write(Data);
    }
}