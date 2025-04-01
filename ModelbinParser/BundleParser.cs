using System;
using System.Collections.Generic;
using System.IO;
using ForzaTools.Bundles;
using ForzaTools.Bundles.Blobs;
using ForzaTools.Bundles.Metadata;

namespace ModelbinParser
{
    public class BundleParser
    {
        public Bundle BundleData { get; private set; }
        private byte[] fileData;
        private string filePath;

        public BundleParser(string path)
        {
            filePath = path;
        }

        public void LoadFile()
        {
            fileData = File.ReadAllBytes(filePath);
            using (MemoryStream ms = new MemoryStream(fileData))
            {
                BundleData = new Bundle();
                BundleData.Load(ms); // ✅ FIXED: Changed `Read()` to `Load()`
            }
        }

        public List<BundleBlob> GetBlobs()
        {
            return BundleData?.Blobs ?? new List<BundleBlob>();
        }

        public void SaveFile(string savePath)
        {
            File.WriteAllBytes(savePath, fileData);
        }
    }
}
