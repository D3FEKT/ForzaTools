using System;
using System.Collections.Generic;
using Syroot.BinaryData;
using ForzaTools.Shared;

namespace ForzaTools.Bundles.Blobs;

public class BufferHeader
{
    public int Length { get; set; }
    public int Size { get; set; }
    public ushort Stride { get; set; }

    // v1.0+ fields
    public byte NumElements { get; set; }
    public DXGI_FORMAT Format { get; set; }

    public byte[][] Data { get; set; }

    public void Read(BinaryStream bs, byte versionMajor, byte versionMinor)
    {
        Length = bs.ReadInt32();
        Size = bs.ReadInt32();
        Stride = bs.ReadUInt16();

        bool isV1 = versionMajor > 1 || (versionMajor == 1 && versionMinor >= 0);

        if (isV1)
        {
            NumElements = bs.Read1Byte();
            bs.Read1Byte(); // Padding
            Format = (DXGI_FORMAT)bs.ReadInt32();
        }
        else
        {
            // Default values for pre-v1.0
            NumElements = 1;
            Format = DXGI_FORMAT.DXGI_FORMAT_UNKNOWN;
            bs.ReadBytes(2); // Padding
        }

        // Read the actual data buffer
        // Note: The template treats this as a raw byte blob 'ubyte data[descriptor.size]'.
        // The previous C# implementation split it into arrays of Stride.
        // We will maintain the array-of-arrays structure for easier usage, 
        // but rely on 'Length' (item count) and 'Stride'.

        Data = new byte[Length][];
        for (int i = 0; i < Length; i++)
        {
            Data[i] = bs.ReadBytes(Stride);
        }
    }

    public void Serialize(BinaryStream bs, byte versionMajor, byte versionMinor)
    {
        bs.WriteInt32(Data.Length);
        bs.WriteInt32(Data.Length * Stride); // Recalculate size
        bs.WriteUInt16(Stride);

        bool isV1 = versionMajor > 1 || (versionMajor == 1 && versionMinor >= 0);

        if (isV1)
        {
            bs.WriteByte(NumElements);
            bs.WriteByte(0);
            bs.WriteInt32((int)Format);
        }
        else
        {
            bs.WriteBytes(new byte[2]);
        }

        for (int i = 0; i < Data.Length; i++)
            bs.WriteBytes(Data[i]);
    }

    public void CreateModelBin(BinaryStream bs, byte versionMajor, byte versionMinor)
    {
        // Safe access to Data
        int count = Data?.Length ?? 0;
        bs.WriteInt32(count);
        bs.WriteInt32(count * Stride);
        bs.WriteUInt16(Stride);

        bool isV1 = versionMajor > 1 || (versionMajor == 1 && versionMinor >= 0);

        if (isV1)
        {
            bs.WriteByte(NumElements);
            bs.WriteByte(0); // Padding
            bs.WriteInt32((int)Format);
        }
        else
        {
            bs.WriteBytes(new byte[2]);
        }

        if (Data != null)
        {
            for (int i = 0; i < Data.Length; i++)
                bs.WriteBytes(Data[i] ?? new byte[Stride]); // Safety check for null inner arrays
        }
    }
}