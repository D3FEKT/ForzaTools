using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using ForzaTools.Bundles;
using ForzaTools.Bundles.Blobs;
using ForzaTools.Bundles.Metadata;

namespace ForzaTools.ForzaAnalyzer.Services
{
    public class MaterialEntry
    {
        [JsonPropertyName("MaterialMetaData")]
        public string MaterialMetaData { get; set; }

        [JsonPropertyName("MaterialBlob")]
        public string MaterialBlob { get; set; }
    }

    [JsonSerializable(typeof(Dictionary<string, MaterialEntry>))]
    public partial class MaterialJsonContext : JsonSerializerContext
    {
    }

    public class MaterialExtractionService
    {
        // "Name" in reversed bytes
        private static readonly byte[] nameTag = new byte[] { 0x65, 0x6D, 0x61, 0x4E };
        // "Id  " in reversed bytes
        private static readonly byte[] idTag = new byte[] { 0x20, 0x20, 0x64, 0x49 };

        public async Task<int> ExtractMaterialsAsync(IEnumerable<string> filePaths)
        {
            var materials = new Dictionary<string, MaterialEntry>();
            var outputDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Materials");
            Directory.CreateDirectory(outputDir);

            await Task.Run(() =>
            {
                foreach (var path in filePaths)
                {
                    try
                    {
                        var extension = Path.GetExtension(path).ToLower();
                        if (extension == ".zip")
                        {
                            ProcessZip(path, materials);
                        }
                        else if (extension == ".modelbin")
                        {
                            ProcessModelBin(path, materials);
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Failed to process file {path}: {ex.Message}");
                    }
                }

                var jsonOptions = new JsonSerializerOptions
                {
                    WriteIndented = true,
                    TypeInfoResolver = MaterialJsonContext.Default
                };

                string jsonString = JsonSerializer.Serialize(materials, typeof(Dictionary<string, MaterialEntry>), jsonOptions);
                File.WriteAllText(Path.Combine(outputDir, "materials.json"), jsonString);
            });

            return materials.Count;
        }

        private void ProcessZip(string zipPath, Dictionary<string, MaterialEntry> materials)
        {
            try
            {
                string tempPath = Path.Combine(Path.GetTempPath(), "ForzaMatExtract_" + Guid.NewGuid());
                Directory.CreateDirectory(tempPath);

                using (var zip = new CustomZipFile(zipPath))
                {
                    zip.ExtractToDirectory(tempPath);
                }

                var binFiles = Directory.GetFiles(tempPath, "*.modelbin", SearchOption.AllDirectories);
                foreach (var bin in binFiles)
                {
                    ProcessModelBin(bin, materials);
                }

                Directory.Delete(tempPath, true);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error processing zip {zipPath}: {ex.Message}");
            }
        }

        private void ProcessModelBin(string filePath, Dictionary<string, MaterialEntry> materials)
        {
            try
            {
                using var stream = File.OpenRead(filePath);
                var bundle = new Bundle();
                bundle.Load(stream);

                foreach (var blob in bundle.Blobs)
                {
                    // Filter for Material Blobs using Tag checking like Program.cs or Type checking
                    // Program.cs checks blob.Tag == Bundle.TAG_BLOB_Material, but we assume MaterialBlob type here for safety
                    if (blob is MaterialBlob materialBlob)
                    {
                        string materialName = GetMaterialName(materialBlob);

                        // Fallback if no name found, similar to Program.cs logic
                        if (string.IsNullOrEmpty(materialName))
                        {
                            materialName = $"unnamed_material_{Guid.NewGuid().ToString().Substring(0, 8)}";
                        }

                        // 1. Generate Metadata Hex (Replicating Program.cs logic)
                        string metadataHex = CreateFormattedMetadataHex(materialName);

                        // 2. Get Blob Data (Hex)
                        byte[] blobData = materialBlob.GetContents();
                        string blobHex = BitConverter.ToString(blobData).Replace("-", " ");

                        if (!materials.ContainsKey(materialName))
                        {
                            materials[materialName] = new MaterialEntry
                            {
                                MaterialMetaData = metadataHex,
                                MaterialBlob = blobHex
                            };
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error parsing {filePath}: {ex.Message}");
            }
        }

        private string GetMaterialName(MaterialBlob materialBlob)
        {
            // Try to find name in direct metadata
            var nameMeta = materialBlob.Metadatas.OfType<NameMetadata>().FirstOrDefault();
            if (nameMeta != null && !string.IsNullOrEmpty(nameMeta.Name))
            {
                return nameMeta.Name.TrimEnd('\0');
            }

            // Check nested bundles (as per Program.cs logic)
            if (materialBlob.Bundle != null)
            {
                foreach (var nestedBlob in materialBlob.Bundle.Blobs)
                {
                    var nestedNameMeta = nestedBlob.Metadatas.OfType<NameMetadata>().FirstOrDefault();
                    if (nestedNameMeta != null && !string.IsNullOrEmpty(nestedNameMeta.Name))
                    {
                        return nestedNameMeta.Name.TrimEnd('\0');
                    }
                }
            }

            return string.Empty;
        }

        // --- Logic ported directly from Program.cs ---

        private string CreateFormattedMetadataHex(string materialName)
        {
            byte[] nameBytes = Encoding.UTF8.GetBytes(materialName);
            byte stringLengthByte = (byte)(nameBytes.Length);
            // Program.cs adds 8 to the length for this offset byte
            byte sizeByteWithOffset = (byte)(nameBytes.Length + 8);

            byte firstByte, secondByte;
            CalculateSpecialSizeFormat(stringLengthByte, out firstByte, out secondByte);

            using (MemoryStream ms = new MemoryStream())
            {
                // Name Tag Block
                ms.Write(nameTag, 0, nameTag.Length);
                ms.WriteByte(firstByte);
                ms.WriteByte(secondByte);
                ms.WriteByte(0x10);
                ms.WriteByte(0x00);

                // ID Tag Block
                ms.Write(idTag, 0, idTag.Length);
                ms.WriteByte(0x40);
                ms.WriteByte(0x00);

                // Size/Data Block
                ms.WriteByte(sizeByteWithOffset);
                ms.WriteByte(0x00);

                ms.Write(nameBytes, 0, nameBytes.Length);

                // Padding
                ms.WriteByte(0x00);
                ms.WriteByte(0x00);
                ms.WriteByte(0x00);
                ms.WriteByte(0x00);

                return BitConverter.ToString(ms.ToArray()).Replace("-", " ");
            }
        }

        private void CalculateSpecialSizeFormat(byte size, out byte firstByte, out byte secondByte)
        {
            byte highNibble = (byte)((size & 0xF0) >> 4);
            byte lowNibble = (byte)(size & 0x0F);
            firstByte = (byte)(lowNibble << 4);
            secondByte = highNibble;
        }
    }
}