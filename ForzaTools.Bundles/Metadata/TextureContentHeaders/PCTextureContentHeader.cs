using System;
using System.Collections.Generic;
using System.IO;
using Syroot.BinaryData;
using ForzaTools.Shared;

namespace ForzaTools.Bundles.Metadata.TextureContentHeaders;

public class PCTextureContentHeader
{
    public uint MetaDataFixupOffset { get; set; }
    public uint BlobDataFixupOffset { get; set; }

    public Guid Id { get; set; }
    public uint Width { get; set; }
    public uint Height { get; set; }
    public uint Depth { get; set; }

    // Bitfields/Packed
    public ushort NumSlices { get; set; }
    public byte Platform { get; set; }

    public byte NumMips { get; set; }

    public bool IsCubeMap { get; set; }
    public bool IsPremultipliedAlpha { get; set; }

    public TextureTranscoding Transcoding { get; set; }
    public ColorProfile EncodedColorProfile { get; set; }
    public ColorProfile TargetColorProfile { get; set; }
    public TextureDomain Domain { get; set; }

    public List<TextureContentSlice> Slices { get; set; } = new();

    public void Read(byte[] data)
    {
        using var ms = new MemoryStream(data);
        using var bs = new BinaryStream(ms);
        long basePos = 0;

        MetaDataFixupOffset = bs.ReadUInt32();
        BlobDataFixupOffset = bs.ReadUInt32();

        Id = new Guid(bs.ReadBytes(16));
        Width = bs.ReadUInt32();
        Height = bs.ReadUInt32();
        Depth = bs.ReadUInt32();

        ushort packedSlices = bs.ReadUInt16();
        NumSlices = (ushort)(packedSlices & 0x3FFF); // 14 bits
        Platform = (byte)(packedSlices >> 14);       // 2 bits

        NumMips = bs.Read1Byte();

        byte flags = bs.Read1Byte();
        IsCubeMap = (flags & 1) != 0;
        IsPremultipliedAlpha = (flags & 2) != 0;

        Transcoding = (TextureTranscoding)bs.ReadInt32();
        EncodedColorProfile = (ColorProfile)bs.ReadInt32();
        TargetColorProfile = (ColorProfile)bs.ReadInt32();
        Domain = (TextureDomain)bs.ReadInt32();

        // Pointer Fixup for Slices
        uint slicesOffset = bs.ReadUInt32();
        uint slicesNext = bs.ReadUInt32();

        if (slicesOffset != 0)
        {
            bs.Position = basePos + slicesOffset;
            for (int i = 0; i < NumSlices; i++)
            {
                var slice = new TextureContentSlice();
                slice.Encoding = (TextureEncoding)bs.ReadInt32();

                uint mipsOffset = bs.ReadUInt32();
                uint mipsNext = bs.ReadUInt32(); // Unused

                long savePos = bs.Position;

                bs.Position = basePos + mipsOffset;
                for (int j = 0; j < NumMips; j++)
                {
                    var mip = new TextureContentMip();
                    mip.BlobSize = bs.ReadUInt32();
                    mip.BlobOffset = bs.ReadUInt32();
                    bs.ReadUInt32(); // Next pointer (unused)
                    slice.Mips.Add(mip);
                }

                bs.Position = savePos;
                Slices.Add(slice);
            }
        }
    }
}

public class TextureContentSlice
{
    public TextureEncoding Encoding { get; set; }
    public List<TextureContentMip> Mips { get; set; } = new();
}

public class TextureContentMip
{
    public uint BlobSize { get; set; }
    public uint BlobOffset { get; set; }
}

// Enums based on template
public enum TextureEncoding : int
{
    Bc1 = 0, Bc2 = 1, Bc3 = 2, UnsignedBc4 = 3, SignedBc4 = 4,
    UnsignedBc5 = 5, SignedBc5 = 6, UnsignedBc6H = 7, SignedBc6H = 8,
    Bc7 = 9, R32G32B32A32Float = 10, R16G16B16A16 = 11, R16G16B16A16Float = 12,
    R8G8B8A8 = 13, B5G6R5 = 14, B5G5R5A1 = 15, Dct = 16, IntegerDct = 17,
    Procedural = 18, R8 = 19, A8 = 20, R8G8 = 21, Bc7_HighQuality = 22
}

public enum TextureTranscoding : int
{
    None = 0, BcBlockRle = 1, Bc1 = 2, Bc2 = 3, Bc3 = 4,
    UnsignedBc4 = 5, SignedBc4 = 6, UnsignedBc5 = 7, SignedBc5 = 8,
    UnsignedBc6H = 9, SignedBc6H = 10, Bc7 = 11
}

public enum ColorProfile : int
{
    Rec709Linear = 0, Rec709SRgb = 1, Rec709Gamma2 = 2, XvYccLinear = 3
}

public enum TextureDomain : int
{
    Wrap = 0, Clamp = 1, Mirror = 2
}