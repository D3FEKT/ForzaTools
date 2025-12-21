using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using Syroot.BinaryData;

namespace ForzaTools.ForzaAnalyzer
{
    public class CustomZipFile : IDisposable
    {
        private Stream _stream;
        private BinaryStream _bs;

        public CustomZipFile(string path)
        {
            _stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            _bs = new BinaryStream(_stream);
        }

        public void ExtractToDirectory(string destinationDir)
        {
            // Iterate through Local File Headers sequentially
            while (_stream.Position < _stream.Length)
            {
                long headerStart = _stream.Position;
                uint signature = _bs.ReadUInt32();

                // 0x04034b50 = Local File Header Signature
                if (signature != 0x04034b50)
                    break; // Likely hit Central Directory or end of file

                // Read Header
                _bs.ReadUInt16(); // Version needed
                ushort flags = _bs.ReadUInt16();
                ushort method = _bs.ReadUInt16();
                _bs.ReadUInt16(); // ModTime
                _bs.ReadUInt16(); // ModDate
                uint crc32 = _bs.ReadUInt32();
                uint compressedSize = _bs.ReadUInt32();
                uint uncompressedSize = _bs.ReadUInt32();
                ushort fileNameLen = _bs.ReadUInt16();
                ushort extraLen = _bs.ReadUInt16();

                string fileName = _bs.ReadString(fileNameLen, Encoding.UTF8);
                _bs.Position += extraLen; // Skip extra fields

                // Prepare Output Path
                string fullPath = Path.Combine(destinationDir, fileName);
                Directory.CreateDirectory(Path.GetDirectoryName(fullPath));

                // Extract Data
                if (isDirectory(fileName))
                {
                    // It's just a folder entry, skip data logic
                }
                else
                {
                    byte[] fileData = ReadEntryData(method, compressedSize, uncompressedSize);
                    File.WriteAllBytes(fullPath, fileData);
                }

                // If bit 3 of flags is set, Data Descriptor exists after data (CRC + Sizes)
                // We must skip it to find next header. 
                // However, parsing raw zips with Bit 3 is complex without Central Directory.
                // Forza zips usually have sizes in header. If sizes are 0 and flag bit 3 is set, this parser needs enhancement.
                if ((flags & 0x0008) != 0 && compressedSize == 0)
                {
                    throw new Exception("Streamed ZIPs with Data Descriptors (Bit 3) are not supported in this simple parser.");
                }
            }
        }

        private byte[] ReadEntryData(ushort method, uint compressedSize, uint uncompressedSize)
        {
            byte[] input = _bs.ReadBytes((int)compressedSize);

            switch (method)
            {
                case 0: // Store
                    return input;

                case 8: // Deflate
                    using (var ms = new MemoryStream(input))
                    using (var ds = new DeflateStream(ms, CompressionMode.Decompress))
                    using (var outMs = new MemoryStream())
                    {
                        ds.CopyTo(outMs);
                        return outMs.ToArray();
                    }

                case 21: // LZX (Forza Method 21) using XMemCompress
                         // Calls the new XMemCompress class we just wrote
                    return XMemCompress.Decompress(input, (int)uncompressedSize);

                default:
                    throw new NotSupportedException($"Compression method {method} is not supported.");
            }
        }

        private bool isDirectory(string path)
        {
            return path.EndsWith("/") || path.EndsWith("\\");
        }

        public void Dispose()
        {
            _stream?.Dispose();
        }
    }
}