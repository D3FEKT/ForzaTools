using Syroot.BinaryData;

namespace ForzaTools.Bundles.Blobs;

public class MaterialBlob : BundleBlob
{
    public Bundle Bundle { get; set; }

    public override void ReadBlobData(BinaryStream bs)
    {
        // MatI contains a full nested bundle.
        // We pass the underlying stream to the new Bundle to load it.
        // The BinaryStream 'bs' is already positioned at the start of the blob data.

        Bundle = new Bundle();
        Bundle.Load(bs.BaseStream);
    }

    public override void SerializeBlobData(BinaryStream bs)
    {
        if (Bundle != null)
        {
            Bundle.Serialize(bs.BaseStream);
        }
    }
}