using Syroot.BinaryData;

namespace ForzaTools.Bundles.Blobs;

public class RenderTargetBlob : BundleBlob
{
    public bool IsInline { get; set; }
    public byte UnkLength { get; set; }

    public override void ReadBlobData(BinaryStream bs)
    {
        if (IsAtLeastVersion(1, 1))
            IsInline = bs.ReadBoolean();

        UnkLength = bs.Read1Byte();
        // Template says "if unk_length > 0... not supported".
        // We'll just skip bytes if it happens, assuming it's data we don't know structure of.
        if (UnkLength > 0)
            bs.ReadBytes(UnkLength);
    }

    public override void SerializeBlobData(BinaryStream bs)
    {
        if (IsAtLeastVersion(1, 1))
            bs.WriteBoolean(IsInline);

        bs.WriteByte(UnkLength);
        if (UnkLength > 0)
            bs.WriteBytes(new byte[UnkLength]);
    }
}