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
            // Fix: Capture the raw data into CustomBlobData before BundleBlob.Read clears it.
            // This ensures we have the data for extraction without keeping it for all blob types.
            if (Data != null && Data.Length > 0)
            {
                CustomBlobData = Data;
            }

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
                bs.Write(CustomBlobData);
            }
            else
            {
                // Fallback / Error string if no data provided
                string fallback = "scene/library/materials/error.materialbin";
                Write7BitEncodedString(bs, fallback);
            }
        }

        // Override GetContents to return our persisted CustomBlobData
        public override byte[] GetContents()
        {
            return CustomBlobData ?? base.GetContents();
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