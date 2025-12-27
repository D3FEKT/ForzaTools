using ForzaTools.Bundles.Metadata;
using ForzaTools.Shared;
using Syroot.BinaryData;
using System.Collections.Generic;

namespace ForzaTools.Bundles.Blobs;

public class VertexLayoutBlob : BundleBlob
{
    // FIX 1: Initialize lists to prevent "Object reference not set" crashes
    public List<string> SemanticNames { get; set; } = new();
    public List<D3D12_INPUT_LAYOUT_DESC> Elements { get; set; } = new();
    public List<DXGI_FORMAT> PackedFormats { get; set; } = new();
    public uint Flags { get; set; }

    public override void ReadBlobData(BinaryStream bs)
    {
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
        CreateModelBinBlobData(bs);
    }

    public override void CreateModelBinBlobData(BinaryStream bs)
    {
        // FIX 2: Restored Semantic Name writing logic
        // 1. Semantic Names
        bs.WriteUInt16((ushort)SemanticNames.Count);
        foreach (string semanticName in SemanticNames)
        {
            bs.WriteString(semanticName, StringCoding.Int32CharCount);
        }

        // 2. Elements
        bs.WriteUInt16((ushort)Elements.Count);
        foreach (D3D12_INPUT_LAYOUT_DESC element in Elements)
        {
            element.Serialize(bs);
        }

        // 3. Packed Formats
        // Note: We do not write a count here. The reader uses 'elementCount' (from step 2) 
        // to determine how many formats to read.
        foreach (DXGI_FORMAT format in PackedFormats)
        {
            bs.WriteInt32((int)format);
        }

        // 4. Flags
        bs.WriteUInt32(Flags);
    }
}

public class D3D12_INPUT_LAYOUT_DESC
{
    public short SemanticNameIndex;
    public short SemanticIndex;
    public short InputSlot;
    public short InputSlotClass;
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