using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using Syroot.BinaryData;

namespace ForzaTools.CarScene
{
    public enum GameSeries
    {
        Auto = 0,
        Motorsport = 1,
        Horizon = 2
    }

    public class CarbinFile
    {
        public Scene Scene { get; set; }

        public void Load(Stream stream)
        {
            using var bs = new BinaryStream(stream);
            bs.ByteConverter = ByteConverter.Little;
            Scene = new Scene();
            Scene.Read(bs);
        }
    }

    public class Scene
    {
        public ushort Version { get; set; }
        public GameSeries Series { get; set; } = GameSeries.Auto;
        public bool SeriesIsWeak { get; set; } = false;

        public Guid BuildGuid { get; set; }
        public bool BuildStrict { get; set; }
        public uint Ordinal { get; set; }
        public string MediaName { get; set; }
        public string SkeletonPath { get; set; }
        public LODFlags LODDetails { get; set; }
        public List<PartEntry> NonUpgradableParts { get; set; } = new();
        public List<UpgradablePart> UpgradableParts { get; set; } = new();
        public bool UnkV6 { get; set; } // Horizon v6+

        public void Read(BinaryStream bs)
        {
            Version = bs.ReadUInt16();

            // Heuristic from template
            if (Series == GameSeries.Auto && (Version == 10 || Version == 11))
            {
                Series = GameSeries.Motorsport;
                SeriesIsWeak = true;
            }

            if (Version >= 3) BuildGuid = new Guid(bs.ReadBytes(16));
            if (Version >= 5) BuildStrict = bs.ReadBoolean();

            Ordinal = bs.ReadUInt32();
            MediaName = bs.ReadString(StringCoding.Int32CharCount);
            SkeletonPath = bs.ReadString(StringCoding.Int32CharCount);

            if (Version >= 2) LODDetails = new LODFlags(bs.ReadUInt16());

            uint nonUpgradableCount = bs.ReadUInt32();
            for (int i = 0; i < nonUpgradableCount; i++)
            {
                var entry = new PartEntry();
                entry.Read(bs, this);
                NonUpgradableParts.Add(entry);
            }

            uint upgradableCount = bs.ReadUInt32();
            for (int i = 0; i < upgradableCount; i++)
            {
                var part = new UpgradablePart();
                part.Read(bs, this);
                UpgradableParts.Add(part);
            }

            if (Series == GameSeries.Horizon && Version >= 6)
                UnkV6 = bs.ReadBoolean();
        }
    }

    public class PartEntry
    {
        public CCarParts Type { get; set; }
        public Part Part { get; set; }

        public void Read(BinaryStream bs, Scene scene)
        {
            if (scene.Version >= 4)
            {
                if (scene.Series == GameSeries.Motorsport && scene.Version >= 6)
                {
                    Type = (CCarParts)bs.Read1Byte();
                }
                else
                {
                    Type = CCarPartsHelper.FromV1((CCarParts)bs.Read1Byte());
                }
                Part = new Part();
                Part.Read(bs, scene);
            }
            else
            {
                Part = new Part();
                Part.Read(bs, scene);
                Type = Part.Type;
            }
        }
    }

    public class Part
    {
        public ushort Version { get; set; }
        public CCarParts Type { get; set; }
        public List<CarRenderModel> Models { get; set; } = new();
        public AABB Bounds { get; set; }

        public void Read(BinaryStream bs, Scene scene)
        {
            Version = bs.ReadUInt16();

            if (scene.Series == GameSeries.Motorsport && Version >= 3)
                Type = (CCarParts)bs.ReadUInt32();
            else
                Type = CCarPartsHelper.FromV1((CCarParts)bs.ReadUInt32());

            uint modelCount = bs.ReadUInt32();
            for (int i = 0; i < modelCount; i++)
            {
                var model = new CarRenderModel();
                model.Read(bs, scene);
                Models.Add(model);
            }

            if (Version >= 2) Bounds = AABB.Read(bs);
        }
    }

    public class UpgradablePart
    {
        public ushort Version { get; set; }
        public CCarParts Type { get; set; }
        public List<Upgrade> Upgrades { get; set; } = new();
        public List<SharedCarModel> SharedModels { get; set; } = new();

        public void Read(BinaryStream bs, Scene scene)
        {
            Version = bs.ReadUInt16();

            if (scene.Series == GameSeries.Motorsport && Version >= 4)
                Type = (CCarParts)bs.ReadUInt32();
            else
                Type = CCarPartsHelper.FromV1((CCarParts)bs.ReadUInt32());

            uint upgradeCount = bs.ReadUInt32();
            for (int i = 0; i < upgradeCount; i++)
            {
                var upg = new Upgrade();
                upg.Read(bs, scene, Version);
                Upgrades.Add(upg);
            }

            if (Version >= 3)
            {
                uint sharedCount = bs.ReadUInt32();
                for (int i = 0; i < sharedCount; i++)
                {
                    var shared = new SharedCarModel();
                    shared.Read(bs, scene);
                    SharedModels.Add(shared);
                }
            }
        }
    }

    public class Upgrade
    {
        public ushort Version { get; set; }
        public byte Level { get; set; }
        public bool IsStock { get; set; }
        public int Id { get; set; }
        public int CarBodyId { get; set; }
        public bool ParentIsStock { get; set; }
        public List<CarRenderModel> Models { get; set; } = new();
        public AABB Bounds { get; set; }

        public void Read(BinaryStream bs, Scene scene, ushort parentPartVersion)
        {
            Version = bs.ReadUInt16();
            Level = bs.Read1Byte();
            IsStock = bs.ReadBoolean();
            Id = bs.ReadInt32();
            CarBodyId = bs.ReadInt32();
            ParentIsStock = bs.ReadBoolean();

            if (Version < 3)
            {
                uint modelCount = bs.ReadUInt32();
                for (int i = 0; i < modelCount; i++)
                {
                    var model = new CarRenderModel();
                    model.Read(bs, scene);
                    Models.Add(model);
                }
            }

            if (Version >= 2) Bounds = AABB.Read(bs);
        }
    }

    public class SharedCarModel
    {
        public List<int> UpgradeIds { get; set; } = new();
        public CarRenderModel Model { get; set; }

        public void Read(BinaryStream bs, Scene scene)
        {
            uint count = bs.ReadUInt32();
            for (int i = 0; i < count; i++) UpgradeIds.Add(bs.ReadInt32());

            Model = new CarRenderModel();
            Model.Read(bs, scene);
        }
    }

    public class CarRenderModel
    {
        public ushort Version { get; set; }
        public string Path { get; set; }
        public Matrix4x4 Transform { get; set; }
        public LODFlags LODDetails { get; set; }
        public string BoneName { get; set; }
        public short BoneId { get; set; }
        public bool SnapToParent { get; set; }
        public DrawGroups DrawGroups { get; set; }
        public string AOSwatchPath { get; set; } // v < 9

        public Dictionary<string, byte[]> MaterialOverrides { get; set; } = new();
        public List<MaterialIndexEntry> MaterialIndexes { get; set; } = new();

        // Droppable
        public bool IsDroppable { get; set; }
        public float DropValue { get; set; }
        public uint DropPartId { get; set; }

        public float BreakAmount { get; set; }
        public List<AOMapInfo> AOMapInfos { get; set; } = new();

        public bool IsInteriorWindshield { get; set; }
        public bool ReceivesImpact { get; set; }
        public bool ReceivesSplatter { get; set; }
        public uint ReceivesDamage { get; set; }
        public uint ReceivesDirt { get; set; }
        public uint ReceivesOil { get; set; }
        public uint ReceivesRubber { get; set; }
        public string AssemblyName { get; set; }
        public Guid GuidV13 { get; set; }
        public Guid DropGuidV14 { get; set; }
        public uint AOMapInfoIdV14 { get; set; }
        public List<Guid> DamageGuids { get; set; } = new();

        // Extra Motorsport fields
        public bool IsInterior { get; set; }
        public uint IsLeftSideWindow { get; set; }
        public uint IsRightSideWindow { get; set; }
        public bool IsNascarWiper { get; set; }
        public bool IsLicensePlate { get; set; }

        // Extra Horizon fields
        public byte HorizonId { get; set; }
        public uint HorizonUnkV18 { get; set; }

        public void Read(BinaryStream bs, Scene scene)
        {
            Version = bs.ReadUInt16();

            // Heuristic series detection based on CarRenderModel version
            if (scene.Series == GameSeries.Auto || scene.SeriesIsWeak)
            {
                if (Version == 18) { scene.Series = GameSeries.Horizon; scene.SeriesIsWeak = false; }
                else if (Version == 15 || Version == 16)
                {
                    if (scene.Version == 5) { scene.Series = GameSeries.Horizon; scene.SeriesIsWeak = false; }
                }
                else
                {
                    scene.Series = GameSeries.Motorsport;
                    if (Version == 21 && (scene.Version == 10 || scene.Version == 11)) scene.SeriesIsWeak = false;
                    else if ((Version == 14 || Version == 17) && scene.Version == 5) scene.SeriesIsWeak = false;
                }
            }

            Path = bs.ReadString(StringCoding.Int32CharCount);
            Transform = ReadMatrix(bs);

            if (Version >= 5) LODDetails = new LODFlags(bs.ReadUInt16());
            else bs.ReadUInt32();

            BoneName = bs.ReadString(StringCoding.Int32CharCount);
            BoneId = bs.ReadInt16();
            SnapToParent = bs.ReadBoolean();
            DrawGroups = new DrawGroups(bs.ReadInt32());

            if (Version < 9) AOSwatchPath = bs.ReadString(StringCoding.Int32CharCount);

            if (Version >= 2)
            {
                uint overrideCount = bs.ReadUInt32();
                for (int i = 0; i < overrideCount; i++)
                {
                    string key = bs.ReadString(StringCoding.Int32CharCount);
                    uint len = bs.ReadUInt32();
                    byte[] data = bs.ReadBytes((int)len);
                    MaterialOverrides[key] = data;
                }
            }

            if (Version >= 3)
            {
                uint indexCount = bs.ReadUInt32();
                for (int i = 0; i < indexCount; i++)
                {
                    string key = bs.ReadString(StringCoding.Int32CharCount);
                    ulong val = 0;
                    if (scene.Series == GameSeries.Motorsport && Version >= 21)
                        val = bs.ReadUInt64();
                    else
                        val = (ulong)bs.ReadInt32();
                    MaterialIndexes.Add(new MaterialIndexEntry { Key = key, Value = val });
                }
            }

            if (Version >= 6)
            {
                IsDroppable = bs.ReadBoolean();
                if (IsDroppable)
                {
                    DropValue = bs.ReadSingle();
                    DropPartId = bs.ReadUInt32();
                }
            }

            if (Version >= 8) BreakAmount = bs.ReadSingle();

            if (Version >= 9)
            {
                uint aoCount = bs.ReadUInt32();
                for (int i = 0; i < aoCount; i++)
                {
                    var ao = new AOMapInfo();
                    ao.Read(bs);
                    AOMapInfos.Add(ao);
                }
            }

            if (Version >= 10) IsInteriorWindshield = bs.ReadBoolean();

            if (Version >= 11)
            {
                ReceivesImpact = bs.ReadBoolean();
                ReceivesSplatter = bs.ReadBoolean();
                ReceivesDamage = bs.ReadUInt32();
                ReceivesDirt = bs.ReadUInt32();
                ReceivesOil = bs.ReadUInt32();
                ReceivesRubber = bs.ReadUInt32();
            }

            if (Version >= 12) AssemblyName = bs.ReadString(StringCoding.Int32CharCount);

            if (Version >= 13) GuidV13 = new Guid(bs.ReadBytes(16));

            if (Version >= 14)
            {
                DropGuidV14 = new Guid(bs.ReadBytes(16));
                AOMapInfoIdV14 = bs.ReadUInt32();
            }

            if (scene.Series == GameSeries.Horizon && Version >= 15) bs.ReadInt32(); // Unk v15

            if ((scene.Series == GameSeries.Motorsport && Version >= 15) || (scene.Series == GameSeries.Horizon && Version >= 16))
            {
                uint dmgCount = bs.ReadUInt32();
                for (int i = 0; i < dmgCount; i++) DamageGuids.Add(new Guid(bs.ReadBytes(16)));
            }

            // --- MISSING PART ADDED BELOW ---

            if (scene.Series == GameSeries.Motorsport)
            {
                if (Version >= 16) bs.ReadUInt32(); // ReceivesRain
                if (Version >= 17) bs.Read1Byte(); // ProxyLodID
                if (Version >= 18) bs.ReadString(StringCoding.Int32CharCount); // unk_v18 string
                if (Version >= 19) bs.ReadString(StringCoding.Int32CharCount); // unk_v19 string

                if (Version >= 20)
                {
                    IsInterior = bs.ReadBoolean();
                    IsLeftSideWindow = bs.ReadUInt32();
                    IsRightSideWindow = bs.ReadUInt32();
                    IsNascarWiper = bs.ReadBoolean();
                    IsLicensePlate = bs.ReadBoolean();
                }
            }
            else if (scene.Series == GameSeries.Horizon)
            {
                if (Version >= 17) HorizonId = bs.Read1Byte();
                if (Version >= 18) HorizonUnkV18 = bs.ReadUInt32(); // Boolean4 (4 bytes)
            }
        }

        private Matrix4x4 ReadMatrix(BinaryStream bs)
        {
            float m11 = bs.ReadSingle(); float m12 = bs.ReadSingle(); float m13 = bs.ReadSingle(); float m14 = bs.ReadSingle();
            float m21 = bs.ReadSingle(); float m22 = bs.ReadSingle(); float m23 = bs.ReadSingle(); float m24 = bs.ReadSingle();
            float m31 = bs.ReadSingle(); float m32 = bs.ReadSingle(); float m33 = bs.ReadSingle(); float m34 = bs.ReadSingle();
            float m41 = bs.ReadSingle(); float m42 = bs.ReadSingle(); float m43 = bs.ReadSingle(); float m44 = bs.ReadSingle();
            return new Matrix4x4(
                m11, m12, m13, m14,
                m21, m22, m23, m24,
                m31, m32, m33, m34,
                m41, m42, m43, m44);
        }
    }

    public class AOMapInfo
    {
        public ushort Version { get; set; }
        public string Path { get; set; }
        public CCarParts PartType { get; set; }
        public int PartId { get; set; }
        public Guid DroppedModelInstanceGuid { get; set; }
        public bool IsDefault { get; set; }

        public void Read(BinaryStream bs)
        {
            Version = bs.ReadUInt16();
            Path = bs.ReadString(StringCoding.Int32CharCount);
            PartType = (CCarParts)bs.ReadUInt32();
            PartId = bs.ReadInt32();

            if (Version >= 2) DroppedModelInstanceGuid = new Guid(bs.ReadBytes(16));
            else { bs.ReadInt16(); bs.ReadBoolean(); }

            IsDefault = bs.ReadBoolean();

            if (Version >= 3)
            {
                bs.ReadSByte(); // lod_test
                bs.ReadSByte(); // lod_value
            }
        }
    }

    public class MaterialIndexEntry { public string Key; public ulong Value; }

    public struct LODFlags
    {
        public ushort Value;
        public LODFlags(ushort v) { Value = v; }
        public bool LODS => (Value & 1) != 0;
        public bool LOD0 => (Value & 2) != 0;
        public bool LOD1 => (Value & 4) != 0;
        public bool LOD2 => (Value & 8) != 0;
        public bool LOD3 => (Value & 16) != 0;
        public bool LOD4 => (Value & 32) != 0;
        public bool LOD5 => (Value & 64) != 0;
        public override string ToString() => $"0x{Value:X4}";
    }

    public struct DrawGroups
    {
        public int Value;
        public DrawGroups(int v) { Value = v; }
        public bool Exterior => (Value & 1) != 0;
        public bool Cockpit => (Value & 2) != 0;
        public override string ToString() => $"0x{Value:X8}";
    }

    public struct AABB
    {
        public Vector4 Min;
        public Vector4 Max;
        public static AABB Read(BinaryStream bs)
        {
            return new AABB
            {
                Min = new Vector4(bs.ReadSingle(), bs.ReadSingle(), bs.ReadSingle(), bs.ReadSingle()),
                Max = new Vector4(bs.ReadSingle(), bs.ReadSingle(), bs.ReadSingle(), bs.ReadSingle())
            };
        }
        public override string ToString() => $"Min:{Min} Max:{Max}";
    }

    public enum CCarParts : uint
    {
        Engine = 0, Drivetrain = 1, CarBody = 2, Motor = 3, Brakes = 4,
        SpringDamper = 5, AntiSwayFront = 6, AntiSwayRear = 7, TireCompound = 8,
        RearWing = 9, RimSizeFront = 10, RimSizeRear = 11, Hood = 36,
    }

    public static class CCarPartsHelper
    {
        public static CCarParts FromV1(CCarParts val)
        {
            if ((uint)val >= 42) return (CCarParts)((uint)val + 1);
            return val;
        }
    }
}