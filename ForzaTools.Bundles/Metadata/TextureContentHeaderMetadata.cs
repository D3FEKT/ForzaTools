using Syroot.BinaryData;
using ForzaTools.Bundles.Metadata.TextureContentHeaders;
using System.IO;

namespace ForzaTools.Bundles.Metadata;

public class TextureContentHeaderMetadata : BundleMetadata
{
    // Holds the raw data or parsed object
    public PCTextureContentHeader PCHeader { get; set; }
    public DurangoTextureContentHeader DurangoHeader { get; set; }

    public override void ReadMetadataData(BinaryStream bs)
    {
        // The base BundleMetadata.Read has already read the bytes into _data (GetContents())
        // We can parse them now if needed, or leave them as bytes.
        // To be useful for editing, we should parse them.

        byte[] data = GetContents();
        if (data == null || data.Length == 0) return;

        // Detection based on Version from the Metadata Header (set in BundleMetadata.Read)
        // Bundle_grub: if (IsVersion(blob_version, 2, 0)) -> Durango
        // Note: 'Version' property here is the METADATA version (4 bits). 
        // The BLOB version is on the TextureContentBlob.
        // However, usually Metadata Version 0 = PC, Version 2 = Durango/XboxOne.

        if (Version == 0)
        {
            PCHeader = new PCTextureContentHeader();
            PCHeader.Read(data);
        }
        else if (Version == 2)
        {
            DurangoHeader = new DurangoTextureContentHeader();
            DurangoHeader.Read(data);
        }
    }

    public override void SerializeMetadataData(BinaryStream bs)
    {
        // Currently read-only in this implementation unless we add Write() to headers
        // If we have modified objects, we should re-serialize them to bytes here.
        // For now, write back the original bytes to avoid corruption.
        bs.Write(GetContents());
    }

    public override void CreateModelBinMetadataData(BinaryStream bs)
    {
        // If we have content bytes, write them
        if (GetContents() != null)
            bs.Write(GetContents());
        else
            bs.WriteBytes(new byte[0]); // Safe default
    }
}