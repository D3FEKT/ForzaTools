using Syroot.BinaryData;

namespace ForzaTools.Bundles.Blobs;

public class STexBlob : BundleBlob
{
    public Bundle TextureBundle { get; set; }

    public override void ReadBlobData(BinaryStream bs)
    {
        TextureBundle = new Bundle();
        TextureBundle.Load(bs);
    }

    public override void SerializeBlobData(BinaryStream bs)
    {
        if (TextureBundle != null)
            TextureBundle.Serialize(bs);
    }
}