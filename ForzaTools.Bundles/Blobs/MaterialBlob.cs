using ForzaTools.Bundles.Metadata;
using Syroot.BinaryData;
using System.Text;

namespace ForzaTools.Bundles.Blobs
{
    public class MaterialBlob : BundleBlob
    {
        public Bundle Bundle { get; set; }

        // New property to hold the raw extracted bytes from materials.json
        public byte[] CustomBlobData { get; set; }

        public override void ReadBlobData(BinaryStream bs)
        {
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
            // If we have custom data injected from the library, use it.
            if (CustomBlobData != null && CustomBlobData.Length > 0)
            {
                // The data in materials.json usually includes the full Bundle structure
                // We need to strip the Bundle Header (24 bytes) and Blob Header (16 bytes) 
                // to get the inner content, OR if the blob expects inner content only.
                // However, 'GetContents()' usually returns the raw blob data (minus bundle/blob headers).
                // Based on ExtractionService logic: "byte[] blobData = materialBlob.GetContents();"
                // So the data in JSON is the RAW CONTENT. We write it directly.

                bs.Write(CustomBlobData);
            }
            else
            {
                // Fallback / Error string if no data provided
                string fallback = "scene/library/materials/error.materialbin";
                Write7BitEncodedString(bs, fallback);
            }
        }

        // Standard 7-bit string writer for fallback paths
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
}