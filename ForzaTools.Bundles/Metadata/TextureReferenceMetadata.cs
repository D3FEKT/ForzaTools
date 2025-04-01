using Syroot.BinaryData;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ForzaTools.Bundles.Metadata
{
    public class TextureReferenceMetadata : BundleMetadata
    {
        public int UnkBufferLength { get; private set; }
        public byte[] UnkBuffer { get; private set; }

        public override void ReadMetadataData(BinaryStream bs)
        {
            if (Version == 1)
            {
                // Read the UnkBufferLength first
                UnkBufferLength = bs.ReadInt32();

                // Read the buffer with hex format
                UnkBuffer = new byte[UnkBufferLength * sizeof(uint)];
                for (int i = 0; i < UnkBufferLength; i++)
                {
                    uint value = bs.ReadUInt32();
                    Buffer.BlockCopy(BitConverter.GetBytes(value), 0, UnkBuffer, i * sizeof(uint), sizeof(uint));
                }
            }
            else
            {
                // Default case: read the remaining bytes as UnkBuffer
                UnkBuffer = bs.ReadBytes(Size);
                UnkBufferLength = UnkBuffer.Length;
            }
        }

        public override void SerializeMetadataData(BinaryStream bs)
        {
            if (Version == 1)
            {
                // Write the UnkBufferLength
                bs.WriteInt32(UnkBufferLength);

                // Write the buffer in hex format
                for (int i = 0; i < UnkBufferLength; i++)
                {
                    if (i * sizeof(uint) + sizeof(uint) <= UnkBuffer.Length)
                    {
                        uint value = BitConverter.ToUInt32(UnkBuffer, i * sizeof(uint));
                        bs.WriteUInt32(value);
                    }
                }
            }
            else
            {
                // Default case: write all bytes in UnkBuffer
                bs.Write(UnkBuffer);
            }
        }
    }
}
