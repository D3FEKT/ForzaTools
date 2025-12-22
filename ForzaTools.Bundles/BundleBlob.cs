using System;
using System.Collections.Generic;
using System.Diagnostics;
using Syroot.BinaryData;
using ForzaTools.Bundles.Metadata;

namespace ForzaTools.Bundles;

public abstract class BundleBlob
{
    public const int InfoSize = 0x18;

    public uint Tag { get; set; }
    public byte VersionMajor { get; set; }
    public byte VersionMinor { get; set; }

    public uint CompressedSize { get; set; }
    public uint UncompressedSize { get; set; }

    // Added: Store the original file offset for hex viewing
    public long FileOffset { get; set; }

    public List<BundleMetadata> Metadatas { get; set; } = new List<BundleMetadata>();

    private byte[] _data { get; set; }

    public T GetMetadataByTag<T>(uint tag) where T : BundleMetadata
    {
        foreach (var metadata in Metadatas)
        {
            if (metadata.Tag == tag)
                return (T)metadata;
        }

        return null;
    }

    public virtual void Read(BinaryStream bs, long baseBundleOffset)
    {
        Tag = bs.ReadUInt32();
        VersionMajor = bs.Read1Byte();
        VersionMinor = bs.Read1Byte();

        uint metadataCount = bs.ReadUInt16();
        uint metadataOffset = bs.ReadUInt32();
        uint dataOffset = bs.ReadUInt32();

        CompressedSize = bs.ReadUInt32();
        UncompressedSize = bs.ReadUInt32();

        long basePos = bs.Position;

        // Read Metadata
        for (int i = 0; i < metadataCount; i++)
        {
            bs.Position = baseBundleOffset + metadataOffset + (i * BundleMetadata.InfoSize);
            uint metadataTag = bs.ReadUInt32();
            bs.Position -= 4;

            BundleMetadata metadata = GetMetadataObjectByTag(metadataTag);
            if (metadata != null)
            {
                metadata.Read(bs);
                Metadatas.Add(metadata);
            }
            else
            {
                // Skip unknown
                // throw new NotImplementedException($"Unimplemented metadata tag {metadataTag:X8}");
            }
        }

        // Read Blob Data
        bs.Position = baseBundleOffset + dataOffset;
        this.FileOffset = bs.Position; // Save offset

        uint sizeToRead = UncompressedSize > 0 ? UncompressedSize : CompressedSize;
        _data = bs.ReadBytes((int)sizeToRead);

        bs.Position = baseBundleOffset + dataOffset;
        ReadBlobData(bs);
    }

    public abstract void ReadBlobData(BinaryStream bs);

    public abstract void SerializeBlobData(BinaryStream bs);

    private BundleMetadata GetMetadataObjectByTag(uint tag)
    {
        return tag switch
        {
            BundleMetadata.TAG_METADATA_Name => new NameMetadata(),
            BundleMetadata.TAG_METADATA_Identifier => new IdentifierMetadata(),
            BundleMetadata.TAG_METADATA_Atlas => new AtlasMetadata(),
            BundleMetadata.TAG_METADATA_BBox => new BoundaryBoxMetadata(),
            BundleMetadata.TAG_METADATA_TextureContentHeader => new TextureContentHeaderMetadata(),
            BundleMetadata.TAG_METADATA_TRef => new TextureReferencesMetadata(),
            BundleMetadata.TAG_METADATA_ACMR => new ACMRMetadata(),
            BundleMetadata.TAG_METADATA_VDCL => new VDCLMetadata(),
            BundleMetadata.TAG_METADATA_BLEN => new BlendMetadata(),
            _ => null,
        };
    }

    public void SerializeMetadatas(BinaryStream bs)
    {
        long headersStartOffset = bs.Position;
        long lastDataPos = bs.Position + (BundleMetadata.InfoSize * Metadatas.Count);
        for (int j = 0; j < Metadatas.Count; j++)
        {
            bs.Position = lastDataPos;

            long headerOffset = headersStartOffset + (BundleMetadata.InfoSize * j);
            long dataStartOffset = lastDataPos;

            BundleMetadata metadata = Metadatas[j];
            metadata.SerializeMetadataData(bs);

            ulong relativeOffset = (ulong)(lastDataPos - headerOffset);

            lastDataPos = bs.Position;

            bs.Position = headerOffset;
            bs.WriteUInt32(metadata.Tag);

            ulong metadataSize = (ulong)(lastDataPos - dataStartOffset);
            Debug.Assert(metadataSize <= ushort.MaxValue);

            ushort flags = (ushort)(metadataSize << 4 | (ushort)(metadata.Version & 0b1111));
            bs.WriteUInt16(flags);

            Debug.Assert(relativeOffset <= ushort.MaxValue);
            bs.WriteUInt16((ushort)relativeOffset);
        }

        bs.Position = lastDataPos;
    }

    public void CreateModelBinMetadatas(BinaryStream bs)
    {
        long headersStartOffset = bs.Position;
        long lastDataPos = bs.Position + (BundleMetadata.InfoSize * Metadatas.Count);

        for (int j = 0; j < Metadatas.Count; j++)
        {
            bs.Position = lastDataPos;

            long headerOffset = headersStartOffset + (BundleMetadata.InfoSize * j);
            long dataStartOffset = lastDataPos;

            BundleMetadata metadata = Metadatas[j];
            metadata.CreateModelBinMetadataData(bs);

            ulong relativeOffset = (ulong)(lastDataPos - headerOffset);
            lastDataPos = bs.Position;

            // Write Header
            bs.Position = headerOffset;
            bs.WriteUInt32(metadata.Tag);

            ulong metadataSize = (ulong)(lastDataPos - dataStartOffset);
            // Flags: Size (12 bits) | Version (4 bits)
            ushort flags = (ushort)(metadataSize << 4 | (ushort)(metadata.Version & 0b1111));
            bs.WriteUInt16(flags);

            bs.WriteUInt16((ushort)relativeOffset);
        }

        bs.Position = lastDataPos;
    }

    // Abstract method to be implemented by all blobs
    public abstract void CreateModelBinBlobData(BinaryStream bs);

    public byte[] GetContents() => _data;

    public bool IsAtMostVersion(byte versionMajor, byte versionMinor)
    {
        return VersionMajor < versionMajor || (VersionMajor == versionMajor && VersionMinor <= versionMinor);
    }

    public bool IsAtLeastVersion(byte versionMajor, byte versionMinor)
    {
        return VersionMajor > versionMajor || (VersionMajor == versionMajor && VersionMinor >= versionMinor);
    }
}