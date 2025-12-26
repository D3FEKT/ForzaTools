using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace ForzaTools.ForzaAnalyzer.Services
{
    public static class MaterialLibrary
    {
        private static Dictionary<string, MaterialEntry> _cache;
        private static readonly string _jsonPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Materials", "materials.json");

        public static void Initialize()
        {
            if (_cache != null) return;

            try
            {
                if (File.Exists(_jsonPath))
                {
                    string json = File.ReadAllText(_jsonPath);
                    _cache = JsonSerializer.Deserialize(json, MaterialJsonContext.Default.DictionaryStringMaterialEntry);
                }
                else
                {
                    _cache = new Dictionary<string, MaterialEntry>();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"MaterialLibrary Load Error: {ex.Message}");
                _cache = new Dictionary<string, MaterialEntry>();
            }
        }

        public static List<string> GetMaterialNames()
        {
            Initialize();
            return _cache.Keys.OrderBy(k => k).ToList();
        }

        public static byte[] GetMaterialData(string name)
        {
            Initialize();

            if (_cache.TryGetValue(name, out var entry) && !string.IsNullOrEmpty(entry.MaterialBlob))
            {
                return HexToBytes(entry.MaterialBlob);
            }

            throw new Exception($"Material data for '{name}' could not be found or is empty.");
        }

        private static byte[] HexToBytes(string hex)
        {
            // Remove any spaces or dashes just in case
            hex = hex.Replace(" ", "").Replace("-", "");
            return Enumerable.Range(0, hex.Length)
                             .Where(x => x % 2 == 0)
                             .Select(x => Convert.ToByte(hex.Substring(x, 2), 16))
                             .ToArray();
        }
    }
}