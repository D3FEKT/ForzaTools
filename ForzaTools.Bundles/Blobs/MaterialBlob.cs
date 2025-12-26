using ForzaTools.Bundles.Metadata;
using Syroot.BinaryData;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;

namespace ForzaTools.Bundles.Blobs;

public class MaterialBlob : BundleBlob
{
    public Bundle Bundle { get; set; }
    public string SelectedMaterialName { get; set; } = "chrome";

    public override void ReadBlobData(BinaryStream bs)
    {
        // MatI contains a full nested bundle.
        // We pass the underlying stream to the new Bundle to load it.
        // The BinaryStream 'bs' is already positioned at the start of the blob data.

        Bundle = new Bundle();
        Bundle.Load(bs.BaseStream);
    }

    public override void SerializeBlobData(BinaryStream bs)
    {
        if (Bundle != null)
        {
            Bundle.Serialize(bs.BaseStream);
        }
    }

    public override void CreateModelBinBlobData(BinaryStream bs)
    {
        // 1. Load from JSON helper
        var entry = MaterialLibrary.GetMaterial(SelectedMaterialName);
        byte[] rawBundleBytes = MaterialLibrary.HexToBytes(entry.MaterialBlob);

        const int BundleHeaderSize = 24;
        const int BlobHeaderSize = 16;
        int offsetToContent = BundleHeaderSize + BlobHeaderSize;

        if (rawBundleBytes.Length > offsetToContent)
        {
            int count = rawBundleBytes.Length - offsetToContent;
            bs.Write(rawBundleBytes, offsetToContent, count);
        }
        else
        {
            // Fallback: Write the path as a 7-bit encoded string
            string fallbackPath = "scene/library/materials/error.materialbin";
            Write7BitEncodedString(bs, fallbackPath);
        }
    }

    public override void CreateModelBinMetadatas(BinaryStream bs)
    {
        this.Metadatas.Clear();
        var entry = MaterialLibrary.GetMaterial(SelectedMaterialName);
        byte[] metaBytes = MaterialLibrary.HexToBytes(entry.MaterialMetaData);

        // Note: The JSON contains raw metadata bytes (including tags). 
        // We cannot use 'base.CreateModelBinMetadatas' because that method wraps 
        // our objects in tags again. We must write this raw blob directly 
        // BUT Bundle.cs expects us to populate 'Metadatas' list for offsets.
        // Since we are hacking in raw bytes from Python, we have to manually
        // reconstruct the metadata objects if we want to use the standard system.

        // Simpler fix: Just populate standard metadata with defaults for now
        this.Metadatas.Add(new NameMetadata { Tag = BundleMetadata.TAG_METADATA_Name, Name = SelectedMaterialName });
        this.Metadatas.Add(new IdentifierMetadata { Tag = BundleMetadata.TAG_METADATA_Identifier, Id = this.Id });

        base.CreateModelBinMetadatas(bs);
    }

    private void Write7BitEncodedString(BinaryStream bs, string value)
    {
        if (value == null) value = "";
        byte[] bytes = Encoding.UTF8.GetBytes(value);
        Write7BitEncodedInt(bs, bytes.Length);
        bs.Write(bytes);
    }

    private void Write7BitEncodedInt(BinaryStream bs, int value)
    {
        uint v = (uint)value;
        while (v >= 0x80)
        {
            bs.WriteByte((byte)(v | 0x80));
            v >>= 7;
        }
        bs.WriteByte((byte)v);
    }
}

public static class MaterialLibrary
    {
        private static Dictionary<string, MaterialEntry> _cache;

        public class MaterialEntry
        {
            public string MaterialMetaData { get; set; }
            public string MaterialBlob { get; set; }
        }

        public static MaterialEntry GetMaterial(string materialName)
        {
            if (_cache == null)
            {
                LoadMaterials();
            }

            if (_cache != null && _cache.TryGetValue(materialName, out var entry))
            {
                return entry;
            }

            throw new Exception($"Material '{materialName}' not found in materials.json");
        }

        private static void LoadMaterials()
        {
            // Assumes materials.json is in *root of exe*/materials/materials.json
            string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "materials", "materials.json");

            if (!File.Exists(path))
            {
                // Fallback or error handling
                Console.WriteLine($"Warning: materials.json not found at {path}");
                return;
            }

            try
            {
                string json = File.ReadAllText(path);
                _cache = JsonSerializer.Deserialize<Dictionary<string, MaterialEntry>>(json);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading materials.json: {ex.Message}");
            }
        }

        // Helper to convert Hex String to Byte Array
        public static byte[] HexToBytes(string hex)
        {
            if (string.IsNullOrWhiteSpace(hex)) return Array.Empty<byte>();

            hex = hex.Replace(" ", "");
            return Enumerable.Range(0, hex.Length)
                             .Where(x => x % 2 == 0)
                             .Select(x => Convert.ToByte(hex.Substring(x, 2), 16))
                             .ToArray();
        }
    }
