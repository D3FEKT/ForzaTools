using Syroot.BinaryData;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ForzaTools.Bundles.Metadata
{
    public class BlendMetadata : BundleMetadata
    {
        public bool Unk1 { get; set; } // bool from ubyte
        public bool Unk2 { get; set; } // bool from ubyte

        public override void ReadMetadataData(BinaryStream bs)
        {
            // Read two ubyte values and convert them to bool
            Unk1 = bs.Read1Byte() != 0;
            Unk2 = bs.Read1Byte() != 0;
        }

        public override void SerializeMetadataData(BinaryStream bs)
        {
            // Write two bool values as ubytes
            bs.WriteByte((byte)(Unk1 ? 1 : 0));
            bs.WriteByte((byte)(Unk2 ? 1 : 0));
        }
    }
}
