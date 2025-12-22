using Syroot.BinaryData;
using System.Collections.Generic;

namespace ForzaTools.Bundles.Blobs;

public class LightScenarioBlob : BundleBlob
{
    public bool IsInline { get; set; }
    public List<LightScenario> LightScenarios { get; set; } = new();

    public override void ReadBlobData(BinaryStream bs)
    {
        if (IsAtLeastVersion(1, 1))
            IsInline = bs.ReadBoolean();

        byte count = bs.Read1Byte();
        for (int i = 0; i < count; i++)
        {
            var ls = new LightScenario();
            ls.Name = bs.ReadString(StringCoding.VariableByteCount);
            ls.Version = bs.ReadUInt32();

            bool hasInstancedDataV1_4 = false;
            if (IsAtLeastVersion(1, 4))
                hasInstancedDataV1_4 = bs.ReadBoolean();

            uint vertexShadersCount = 1;
            if (IsAtLeastVersion(1, 2))
                vertexShadersCount = bs.ReadUInt32();

            // Handle Horizon specific unk0_length logic if needed (assumed 1 for now or standard flow)
            // Implementation simplifies nested struct 'unk0' which often has length 1

            for (int v = 0; v < vertexShadersCount; v++)
            {
                var vs = new VertexShaderEntry();
                if (IsAtLeastVersion(1, 2))
                    vs.UnkV1_2 = bs.Read1Byte();

                vs.Path = bs.ReadString(StringCoding.VariableByteCount);

                // Hashes parsing logic (simplified for generic support)
                if (IsAtLeastVersion(1, 6))
                {
                    byte platformCount = bs.Read1Byte();
                    for (int p = 0; p < platformCount; p++)
                    {
                        bs.Read1Byte(); // Platform
                        bs.ReadBytes(32); // Hash
                    }
                }
                else if (IsAtLeastVersion(1, 5))
                {
                    bs.ReadBytes(32); // Hash 0
                    bs.ReadBytes(32); // Hash 1
                }

                if (hasInstancedDataV1_4)
                    vs.PathV1_4 = bs.ReadString(StringCoding.VariableByteCount);

                ls.VertexShaders.Add(vs);
            }

            // Unk bitfields v1.3
            if (IsAtLeastVersion(1, 3))
                bs.ReadInt32(); // Bitfield

            ls.GeometryPixelShader = bs.ReadString(StringCoding.VariableByteCount);

            if (IsAtLeastVersion(1, 5) && hasInstancedDataV1_4) // And unk__v1_4 exists? Logic is complex here
                ls.PathV1_5 = bs.ReadString(StringCoding.VariableByteCount);

            // Shader library check would go here (v1.6+)

            LightScenarios.Add(ls);
        }
    }

    public override void SerializeBlobData(BinaryStream bs)
    {
        // Serialization requires reconstructing exact logic, 
        // strictly following ReadBlobData structure
        if (IsAtLeastVersion(1, 1))
            bs.WriteBoolean(IsInline);

        bs.WriteByte((byte)LightScenarios.Count);
        foreach (var ls in LightScenarios)
        {
            bs.WriteString(ls.Name, StringCoding.VariableByteCount);
            bs.WriteUInt32(ls.Version);

            bool hasInstancedData = !string.IsNullOrEmpty(ls.VertexShaders[0].PathV1_4); // heuristic
            if (IsAtLeastVersion(1, 4))
                bs.WriteBoolean(hasInstancedData);

            if (IsAtLeastVersion(1, 2))
                bs.WriteUInt32((uint)ls.VertexShaders.Count);

            foreach (var vs in ls.VertexShaders)
            {
                if (IsAtLeastVersion(1, 2))
                    bs.WriteByte(vs.UnkV1_2);

                bs.WriteString(vs.Path, StringCoding.VariableByteCount);

                // Write placeholder hashes for now
                if (IsAtLeastVersion(1, 6))
                {
                    bs.WriteByte(0); // 0 platforms
                }
                else if (IsAtLeastVersion(1, 5))
                {
                    bs.WriteBytes(new byte[64]);
                }

                if (hasInstancedData)
                    bs.WriteString(vs.PathV1_4, StringCoding.VariableByteCount);
            }

            if (IsAtLeastVersion(1, 3))
                bs.WriteInt32(0);

            bs.WriteString(ls.GeometryPixelShader, StringCoding.VariableByteCount);

            if (IsAtLeastVersion(1, 5) && hasInstancedData)
                bs.WriteString(ls.PathV1_5 ?? "", StringCoding.VariableByteCount);
        }
    }

    public override void CreateModelBinBlobData(BinaryStream bs)
    {
        //not needed for modelbin
    }
}

public class LightScenario
{
    public string Name { get; set; }
    public uint Version { get; set; }
    public List<VertexShaderEntry> VertexShaders { get; set; } = new();
    public string GeometryPixelShader { get; set; }
    public string PathV1_5 { get; set; }
}

public class VertexShaderEntry
{
    public byte UnkV1_2 { get; set; }
    public string Path { get; set; }
    public string PathV1_4 { get; set; }
}