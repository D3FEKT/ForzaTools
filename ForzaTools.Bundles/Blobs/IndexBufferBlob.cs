using Syroot.BinaryData;
using ForzaTools.Bundles.Metadata;

namespace ForzaTools.Bundles.Blobs;

public class IndexBufferBlob : BundleBlob
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
        // Previously, writing 'Data' would duplicate the header and ignore any 
        // changes made to indices in the tool.
        Header.CreateModelBin(bs, VersionMajor, VersionMinor);
    }
}