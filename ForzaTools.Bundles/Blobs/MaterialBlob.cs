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

    public override void CreateModelBinBlobData(BinaryStream bs)
    {
        if (Bundle != null)
        {
            Bundle.CreateModelBin(bs.BaseStream);
        }
        else
        {
            // If no bundle exists, create a dummy one to satisfy file structure or throw?
            // Creating an empty bundle with 0 blobs
            var emptyBundle = new Bundle();
            emptyBundle.VersionMajor = this.VersionMajor;
            emptyBundle.VersionMinor = this.VersionMinor;
            emptyBundle.CreateModelBin(bs.BaseStream);
        }
    }
}