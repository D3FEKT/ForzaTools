using Syroot.BinaryData;
using System;
using System.Collections.Generic;
using System.Numerics;
using ForzaTools.Bundles.Metadata;

namespace ForzaTools.Bundles.Blobs;

public class MaterialShaderParameterBlob : BundleBlob
{
    public List<ShaderParameter> Parameters { get; set; } = new();

    // Extra data at end of v2.0+
    public uint Unk1 { get; set; }
    public uint Unk2 { get; set; }
    public uint Unk3 { get; set; }

    public override void ReadBlobData(BinaryStream bs)
    {
        ushort count = 0;
        if (IsAtLeastVersion(2, 1))
            count = bs.ReadUInt16();
        else
            count = bs.Read1Byte();

        for (int i = 0; i < count; i++)
        {
            var param = new ShaderParameter();
            param.Read(bs, this);
            Parameters.Add(param);
        }

        if (IsAtLeastVersion(2, 0) && Tag == Bundle.TAG_BLOB_MaterialShaderParameter)
        {
            Unk1 = bs.ReadUInt32();
            Unk2 = bs.ReadUInt32();
            Unk3 = bs.ReadUInt32();
        }
    }

    public override void SerializeBlobData(BinaryStream bs)
    {
        if (IsAtLeastVersion(2, 1))
            bs.WriteUInt16((ushort)Parameters.Count);
        else
            bs.WriteByte((byte)Parameters.Count);

        foreach (var param in Parameters)
            param.Serialize(bs, this);

        if (IsAtLeastVersion(2, 0) && Tag == Bundle.TAG_BLOB_MaterialShaderParameter)
        {
            bs.WriteUInt32(Unk1);
            bs.WriteUInt32(Unk2);
            bs.WriteUInt32(Unk3);
        }
    }
}

public class ShaderParameter
{
    public byte VersionMajor { get; set; }
    public byte VersionMinor { get; set; }
    public uint NameHash { get; set; }
    public uint UnkV3_1 { get; set; }
    public ShaderParameterType Type { get; set; }
    public Guid Guid { get; set; }

    public object Value { get; set; }

    public void Read(BinaryStream bs, BundleBlob blob)
    {
        VersionMajor = bs.Read1Byte();
        VersionMinor = bs.Read1Byte();
        NameHash = bs.ReadUInt32();

        if (VersionMajor > 3 || (VersionMajor == 3 && VersionMinor >= 1))
        {
            bool hasUnk = bs.ReadBoolean();
            if (hasUnk) UnkV3_1 = bs.ReadUInt32();
        }

        Type = (ShaderParameterType)bs.Read1Byte();

        if (VersionMajor >= 3)
            Guid = new Guid(bs.ReadBytes(16));

        switch (Type)
        {
            case ShaderParameterType.Vector:
            case ShaderParameterType.Color:
            case ShaderParameterType.Swizzle:
            case ShaderParameterType.FunctionRange:
                // Read 4 floats
                Value = new Vector4(bs.ReadSingle(), bs.ReadSingle(), bs.ReadSingle(), bs.ReadSingle());
                break;
            case ShaderParameterType.Float:
                Value = bs.ReadSingle();
                break;
            case ShaderParameterType.Bool:
                Value = bs.ReadInt32() != 0;
                break;
            case ShaderParameterType.Int:
                Value = bs.ReadInt32();
                break;
            case ShaderParameterType.Texture2D:
                Value = ReadTextureParam(bs);
                break;
            case ShaderParameterType.Sampler:
                Value = ReadSamplerParam(bs);
                break;
            case ShaderParameterType.Vector2:
                Value = new Vector2(bs.ReadSingle(), bs.ReadSingle());
                if (VersionMajor < 2) bs.ReadBytes(8); // Legacy padding
                break;
            default:
                throw new NotImplementedException($"Unsupported shader parameter type {Type}");
        }
    }

    private TextureParameter ReadTextureParam(BinaryStream bs)
    {
        var tp = new TextureParameter();
        tp.Path = bs.ReadString(StringCoding.Int32CharCount); // 7BitString in spec? Usually StringCoding.Int32CharCount for Forza
        if (VersionMajor >= 2)
            tp.PathHash = bs.ReadUInt32();
        return tp;
    }

    private SamplerParameter ReadSamplerParam(BinaryStream bs)
    {
        var sp = new SamplerParameter();
        sp.AddressU = bs.ReadInt32();
        sp.AddressV = bs.ReadInt32();
        if (VersionMajor >= 1 && VersionMinor >= 1)
            sp.UnkType = bs.ReadInt32();
        return sp;
    }

    public void Serialize(BinaryStream bs, BundleBlob blob)
    {
        bs.WriteByte(VersionMajor);
        bs.WriteByte(VersionMinor);
        bs.WriteUInt32(NameHash);

        if (VersionMajor > 3 || (VersionMajor == 3 && VersionMinor >= 1))
        {
            bs.WriteBoolean(UnkV3_1 != 0);
            if (UnkV3_1 != 0) bs.WriteUInt32(UnkV3_1);
        }

        bs.WriteByte((byte)Type);

        if (VersionMajor >= 3)
            bs.WriteBytes(Guid.ToByteArray());

        switch (Type)
        {
            case ShaderParameterType.Vector:
            case ShaderParameterType.Color:
            case ShaderParameterType.Swizzle:
            case ShaderParameterType.FunctionRange:
                Vector4 v = (Vector4)Value;
                bs.WriteSingle(v.X); bs.WriteSingle(v.Y); bs.WriteSingle(v.Z); bs.WriteSingle(v.W);
                break;
            case ShaderParameterType.Float:
                bs.WriteSingle((float)Value);
                break;
            case ShaderParameterType.Bool:
                bs.WriteInt32((bool)Value ? 1 : 0);
                break;
            case ShaderParameterType.Int:
                bs.WriteInt32((int)Value);
                break;
            case ShaderParameterType.Texture2D:
                var tp = (TextureParameter)Value;
                bs.WriteString(tp.Path, StringCoding.Int32CharCount);
                if (VersionMajor >= 2) bs.WriteUInt32(tp.PathHash);
                break;
            case ShaderParameterType.Sampler:
                var sp = (SamplerParameter)Value;
                bs.WriteInt32(sp.AddressU);
                bs.WriteInt32(sp.AddressV);
                if (VersionMajor >= 1 && VersionMinor >= 1) bs.WriteInt32(sp.UnkType);
                break;
            case ShaderParameterType.Vector2:
                Vector2 v2 = (Vector2)Value;
                bs.WriteSingle(v2.X); bs.WriteSingle(v2.Y);
                if (VersionMajor < 2) bs.WriteBytes(new byte[8]);
                break;
        }
    }
}

public class TextureParameter
{
    public string Path { get; set; }
    public uint PathHash { get; set; }
}

public class SamplerParameter
{
    public int AddressU { get; set; }
    public int AddressV { get; set; }
    public int UnkType { get; set; }
}

public enum ShaderParameterType : byte
{
    Vector = 0,
    Color = 1,
    Float = 2,
    Bool = 3,
    Int = 4,
    Swizzle = 5,
    Texture2D = 6,
    Sampler = 7,
    ColorGradient = 8,
    FunctionRange = 9,
    Vector2 = 11
}