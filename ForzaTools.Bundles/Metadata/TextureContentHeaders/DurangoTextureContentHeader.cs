using System;
using System.IO;
using Syroot.BinaryData;
using DurangoTypes; // Assumes XG_TILE_MODE is here

namespace ForzaTools.Bundles.Metadata.TextureContentHeaders;

public class DurangoTextureContentHeader
{
    public Guid Id { get; set; }
    public ushort Width { get; set; }
    public ushort Height { get; set; }
    public ushort Depth { get; set; }

    public ushort TileRelativeWidth { get; set; }
    public ushort TileRelativeHeight { get; set; }
    public ushort TileRelativeDepth { get; set; }

    public byte NumMips { get; set; }
    public byte TileRelativeMipLevels { get; set; }
    public byte TileRelativeMipOffset { get; set; }

    // Bitfields
    public XG_TILE_MODE TileMode { get; set; }
    public byte Encoding { get; set; } // TextureEncoding enum
    public byte Transcoding { get; set; } // TextureTranscoding enum
    public byte EncodedColorProfile { get; set; } // ColorProfile enum
    public byte TargetColorProfile { get; set; } // ColorProfile enum
    public byte Domain { get; set; } // TextureDomain enum

    public bool IsCubeMap { get; set; }
    public bool Is3DTexture { get; set; }
    public bool IsPremultipliedAlpha { get; set; }
    public byte LogPitchOrLinearSize { get; set; }

    public void Read(byte[] data)
    {
        using var ms = new MemoryStream(data);
        using var bs = new BinaryStream(ms);

        Id = new Guid(bs.ReadBytes(16));
        Width = bs.ReadUInt16();
        Height = bs.ReadUInt16();
        Depth = bs.ReadUInt16();

        TileRelativeWidth = bs.ReadUInt16();
        TileRelativeHeight = bs.ReadUInt16();
        TileRelativeDepth = bs.ReadUInt16();

        NumMips = bs.Read1Byte();
        TileRelativeMipLevels = bs.Read1Byte();
        TileRelativeMipOffset = bs.Read1Byte();

        uint flags = bs.ReadUInt32();

        // 14236BD28; x & 0x1F
        TileMode = (XG_TILE_MODE)(flags & 0x1F);

        // 6 bits
        Encoding = (byte)((flags >> 5) & 0x3F);

        // 6 bits
        Transcoding = (byte)((flags >> 11) & 0x3F);

        // 3 bits
        EncodedColorProfile = (byte)((flags >> 17) & 0x7);

        // 3 bits
        TargetColorProfile = (byte)((flags >> 20) & 0x7);

        // 2 bits
        Domain = (byte)((flags >> 23) & 0x3);

        // Single bits
        IsCubeMap = ((flags >> 25) & 1) != 0;
        Is3DTexture = ((flags >> 26) & 1) != 0;
        IsPremultipliedAlpha = ((flags >> 27) & 1) != 0;

        // 4 bits
        LogPitchOrLinearSize = (byte)((flags >> 28) & 0xF);
    }
}