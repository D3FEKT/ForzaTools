using ForzaTools.Bundles.Metadata;
using ForzaTools.Shared;
using Syroot.BinaryData;
using System;
using System.Collections.Generic;

namespace ForzaTools.Bundles.Blobs;

public class VertexLayoutBlob : BundleBlob
{
    public List<string> SemanticNames { get; set; } = new();
    public List<D3D12_INPUT_LAYOUT_DESC> Elements { get; set; } = new();
    public List<DXGI_FORMAT> PackedFormats { get; set; } = new();
    public uint Flags { get; set; }

    public override void ReadBlobData(BinaryStream bs)
    {
        // ... [Keep existing ReadBlobData] ...
        ushort semanticCount = bs.ReadUInt16();
        for (int i = 0; i < semanticCount; i++)
        {
            string name = bs.ReadString(StringCoding.Int32CharCount);
            SemanticNames.Add(name);
        }

        ushort elementCount = bs.ReadUInt16();
        for (int i = 0; i < elementCount; i++)
        {
            var desc = new D3D12_INPUT_LAYOUT_DESC();
            desc.Read(bs);
            Elements.Add(desc);
        }

        if (IsAtLeastVersion(1, 0))
        {
            for (int i = 0; i < elementCount; i++)
                PackedFormats.Add((DXGI_FORMAT)bs.ReadInt32());
        }

        if (IsAtLeastVersion(1, 1))
            Flags = bs.ReadUInt32();
    }

    public override void SerializeBlobData(BinaryStream bs)
    {
        // ... [Keep existing SerializeBlobData] ...
        bs.WriteUInt16((ushort)SemanticNames.Count);
        foreach (string semanticName in SemanticNames)
        {
            bs.WriteString(semanticName, StringCoding.Int32CharCount);
        }

        bs.WriteUInt16((ushort)Elements.Count);
        foreach (D3D12_INPUT_LAYOUT_DESC element in Elements)
        {
            element.Serialize(bs);
        }

        if (IsAtLeastVersion(1, 0))
        {
            for (int i = 0; i < PackedFormats.Count; i++)
                bs.WriteInt32((int)PackedFormats[i]);
        }

        if (IsAtLeastVersion(1, 1))
            bs.WriteUInt32(Flags);
    }

    public override void CreateModelBinBlobData(BinaryStream bs)
    {
        // 1. Semantic Names Length: 5
        bs.WriteUInt16(5);

        // 2. Semantic Names
        WriteLayoutString(bs, "POSITION");
        WriteLayoutString(bs, "NORMAL");
        WriteLayoutString(bs, "TEXCOORD");
        WriteLayoutString(bs, "TANGENT");
        WriteLayoutString(bs, "COLOR");

        // 3. Num Elements: 11
        bs.WriteUInt16(11);

        // 4. Element Descriptors
        WriteElement(bs, 0, 0, 0, 0, 13, -1, 0); // POSITION
        WriteElement(bs, 1, 0, 1, 0, 37, -1, 0); // NORMAL
        WriteElement(bs, 2, 0, 1, 0, 35, -1, 0); // TEXCOORD

        // TANGENTS & COLORS
        WriteElement(bs, 3, 0, 1, 0, 35, -1, 0);
        WriteElement(bs, 3, 1, 1, 0, 35, -1, 0);
        WriteElement(bs, 3, 2, 1, 0, 35, -1, 0);
        WriteElement(bs, 3, 3, 1, 0, 35, -1, 0);
        WriteElement(bs, 3, 4, 1, 0, 35, -1, 0);

        WriteElement(bs, 4, 0, 1, 0, 24, -1, 0);
        WriteElement(bs, 4, 1, 1, 0, 24, -1, 0);
        WriteElement(bs, 4, 2, 1, 0, 24, -1, 0);

        // 5. Element Formats
        bs.WriteInt32(49); bs.WriteInt32(52); bs.WriteInt32(46);
        bs.WriteInt32(46); bs.WriteInt32(46); bs.WriteInt32(46); bs.WriteInt32(46);
        bs.WriteInt32(48); bs.WriteInt32(48); bs.WriteInt32(48); bs.WriteInt32(22);

        // 6. Semantic Bitfield
        bs.WriteUInt32(0x000004FF);
    }

    private void WriteLayoutString(BinaryStream bs, string str)
    {
        // FIXED: Use Int32CharCount to write 4-byte length prefix
        bs.WriteString(str, StringCoding.Int32CharCount);
    }

    private void WriteElement(BinaryStream bs, ushort nameIdx, ushort semIdx, ushort slot, ushort slotClass, int format, int offset, int step)
    {
        bs.WriteUInt16(nameIdx);
        bs.WriteUInt16(semIdx);
        bs.WriteUInt16(slot);
        bs.WriteUInt16(slotClass);
        bs.WriteInt32(format);
        bs.WriteInt32(offset);
        bs.WriteInt32(step);
    }

    public override void CreateModelBinMetadatas(BinaryStream bs)
    {
        this.Metadatas.Clear();

        // FIXED: Add metadata to list instead of writing directly
        this.Metadatas.Add(new IdentifierMetadata
        {
            Tag = BundleMetadata.TAG_METADATA_Identifier,
            Id = this.Id
        });

        base.CreateModelBinMetadatas(bs);
    }

    public byte GetTotalVertexSize()
    {
        byte size = 0;
        for (int i = 0; i < Elements.Count; i++)
        {
            size += GetSizeOfElementFormat(PackedFormats[i]);
            if (i + 1 < Elements.Count && size % 4 != 0)
            {
                if (GetSizeOfElementFormat(PackedFormats[i + 1]) >= 4)
                    size += (byte)(size % 4);
            }
        }
        return size;
    }

    private static byte GetSizeOfElementFormat(DXGI_FORMAT format)
    {
        return format switch
        {
            DXGI_FORMAT.DXGI_FORMAT_R32G32B32A32_FLOAT => 16,
            DXGI_FORMAT.DXGI_FORMAT_R32G32B32_FLOAT => 12,
            DXGI_FORMAT.DXGI_FORMAT_R32G32_FLOAT => 8,
            DXGI_FORMAT.DXGI_FORMAT_R32_FLOAT => 4,
            DXGI_FORMAT.DXGI_FORMAT_R16G16_UNORM => 4,
            DXGI_FORMAT.DXGI_FORMAT_R16G16_SNORM => 4,
            DXGI_FORMAT.DXGI_FORMAT_R16G16_FLOAT => 4,
            DXGI_FORMAT.DXGI_FORMAT_R8G8B8A8_UNORM => 4,
            DXGI_FORMAT.DXGI_FORMAT_R8G8_UNORM => 2,
            DXGI_FORMAT.DXGI_FORMAT_R8G8_SINT => 2,
            DXGI_FORMAT.DXGI_FORMAT_R10G10B10A2_UNORM => 4,
            DXGI_FORMAT.DXGI_FORMAT_R11G11B10_FLOAT => 4,
            DXGI_FORMAT.DXGI_FORMAT_R24_UNORM_X8_TYPELESS => 4,
            DXGI_FORMAT.DXGI_FORMAT_R8G8_TYPELESS => 2,
            DXGI_FORMAT.DXGI_FORMAT_X32_TYPELESS_G8X24_UINT => 8,
            _ => 4,
        };
    }
}

// ... [Keep D3D12_INPUT_LAYOUT_DESC class] ...
public class D3D12_INPUT_LAYOUT_DESC
{
    public short SemanticNameIndex;
    public short SemanticIndex;
    public short InputSlot;
    public short InputSlotClass; // 0 = PerVertex, 1 = PerInstance
    public DXGI_FORMAT Format;
    public int AlignedByteOffset;
    public int InstanceDataStepRate;

    public void Read(BinaryStream bs)
    {
        SemanticNameIndex = bs.ReadInt16();
        SemanticIndex = bs.ReadInt16();
        InputSlot = bs.ReadInt16();
        InputSlotClass = bs.ReadInt16();
        Format = (DXGI_FORMAT)bs.ReadInt32();
        AlignedByteOffset = bs.ReadInt32();
        InstanceDataStepRate = bs.ReadInt32();
    }

    public void Serialize(BinaryStream bs)
    {
        bs.WriteInt16(SemanticNameIndex);
        bs.WriteInt16(SemanticIndex);
        bs.WriteInt16(InputSlot);
        bs.WriteInt16(InputSlotClass);
        bs.WriteInt32((int)Format);
        bs.WriteInt32(AlignedByteOffset);
        bs.WriteInt32(InstanceDataStepRate);
    }
}