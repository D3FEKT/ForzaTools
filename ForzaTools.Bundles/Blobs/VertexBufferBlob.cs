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
        // FIX: Delegate to Header.CreateModelBin.
        // This ensures we use the parsed/edited data (Header.Data) instead of the raw _data.
        // This prevents header duplication and ensures "Live Edits" are actually saved.
        Header.CreateModelBin(bs, VersionMajor, VersionMinor);
    }
}