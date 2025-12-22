using Syroot.BinaryData;

namespace ForzaTools.Bundles.Blobs;

public class TextureContentBlob : BundleBlob
{
    public byte[] Data { get; set; }

    public override void ReadBlobData(BinaryStream bs)
    {
        // Data size is determined by the BundleBlob header's DataSize
        // We need to calculate remaining bytes or rely on the stream position if strict sizing isn't available in context
        // However, BundleBlob.Read already reads the raw data into _data field via base.Read.
        // So we can just expose it or copy it.
        Data = GetContents();
    }

    public override void SerializeBlobData(BinaryStream bs)
    {
        if (Data != null)
            bs.WriteBytes(Data);
    }

    public override void CreateModelBinBlobData(BinaryStream bs)
    {
        if (Data != null)
            bs.WriteBytes(Data);
    }


}