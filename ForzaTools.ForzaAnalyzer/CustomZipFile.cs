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
            // 1. Locate Central Directory
            long eocdOffset = FindEOCD();
            if (eocdOffset == -1)
                throw new InvalidDataException("Invalid Zip: End of Central Directory signature not found.");

            // 2. Read EOCD
            _bs.Position = eocdOffset + 8; // Skip Sig(4) + Disk(2) + DiskStart(2)
            ushort numEntries = _bs.ReadUInt16();

            _bs.Position = eocdOffset + 12;
            if (numEntries == 0) numEntries = _bs.ReadUInt16(); // Handle overflow case

            _bs.Position = eocdOffset + 16;
            uint cdOffset = _bs.ReadUInt32();

            if (cdOffset >= _stream.Length)
                throw new InvalidDataException("Corrupted Zip: CD Offset out of bounds.");

            // 3. Iterate Central Directory
            _bs.Position = cdOffset;
            for (int i = 0; i < numEntries; i++)
            {
                // Safety check
                if (_bs.Position + 46 > _stream.Length) break;

                uint signature = _bs.ReadUInt32();
                if (signature != 0x02014b50)
                    throw new InvalidDataException($"Corrupted Central Directory at entry {i}");

                _bs.ReadUInt16(); // Version Made
                _bs.ReadUInt16(); // Version Needed
                ushort flags = _bs.ReadUInt16();
                ushort method = _bs.ReadUInt16();
                _bs.ReadUInt16(); // Time
                _bs.ReadUInt16(); // Date
                uint crc = _bs.ReadUInt32();
                uint compressedSize = _bs.ReadUInt32();
                uint uncompressedSize = _bs.ReadUInt32();
                ushort fileNameLen = _bs.ReadUInt16();
                ushort extraLen = _bs.ReadUInt16();
                ushort commentLen = _bs.ReadUInt16();
                _bs.ReadUInt16(); // Disk Start
                _bs.ReadUInt16(); // Internal Attr
                _bs.ReadUInt32(); // External Attr
                uint localHeaderOffset = _bs.ReadUInt32();

                // Read Filename Bytes directly to preserve alignment
                byte[] nameBytes = _bs.ReadBytes(fileNameLen);

                // Decode Filename (Bit 11 = UTF8, else CP437)
                string fileName;
                if ((flags & 0x0800) != 0)
                    fileName = Encoding.UTF8.GetString(nameBytes);
                else
                {
                    // Fallback to CP437 if possible, or ASCII
                    try { fileName = Encoding.GetEncoding(437).GetString(nameBytes); }
                    catch { fileName = Encoding.ASCII.GetString(nameBytes); }
                }

                // Advance Stream past Extra and Comment
                _bs.Position += extraLen + commentLen;
                long nextEntryPos = _bs.Position;

                // 4. Extract File
                if (!IsDirectory(fileName))
                {
                    string fullPath = Path.Combine(destinationDir, fileName);
                    string dirName = Path.GetDirectoryName(fullPath);

                    if (!string.IsNullOrEmpty(dirName) && !Directory.Exists(dirName))
                        Directory.CreateDirectory(dirName);

                    try
                    {
                        // Open output stream directly on disk to save RAM
                        using (var fs = new FileStream(fullPath, FileMode.Create, FileAccess.Write))
                        {
                            ExtractEntryStreamed(localHeaderOffset, compressedSize, uncompressedSize, method, fileName, fs);
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Failed to extract {fileName}: {ex.Message}");
                    }
                }

                // Restore position for next loop
                _bs.Position = nextEntryPos;
            }
        }

        private void ExtractEntryStreamed(uint localHeaderOffset, uint compressedSize, uint uncompressedSize, ushort method, string fileName, Stream outputStream)
        {
            if (localHeaderOffset >= _stream.Length)
                throw new InvalidDataException($"Local Header Offset out of bounds for {fileName}");

            _bs.Position = localHeaderOffset;
            uint sig = _bs.ReadUInt32();
            if (sig != 0x04034b50)
                throw new InvalidDataException($"Invalid Local Header Signature for {fileName}");

            // Skip fixed header (22 bytes)
            _bs.Position += 22;
            ushort nameLen = _bs.ReadUInt16();
            ushort extraLen = _bs.ReadUInt16();

            // Skip variable header parts
            _bs.Position += nameLen + extraLen;

            // --- DECOMPRESSION LOGIC ---

            if (method == 0) // Store (No Compression)
            {
                // Copy directly from source stream to output stream
                CopyStream(_stream, outputStream, (int)compressedSize);
            }
            else if (method == 8) // Deflate
            {
                // We read the compressed chunk into memory. 
                // (For 10MB zip, compressed chunks are small enough. Streaming this part is complex without a wrapper).
                byte[] compressedData = _bs.ReadBytes((int)compressedSize);

                using (var ms = new MemoryStream(compressedData))
                using (var ds = new DeflateStream(ms, CompressionMode.Decompress))
                {
                    ds.CopyTo(outputStream); // Stream result directly to disk
                }
            }
            else if (method == 21) // LZX (Forza)
            {
                // LZX API requires byte arrays, so we must buffer this.
                // Sanity check for uncompressed size to prevent OOM
                if (uncompressedSize > 500 * 1024 * 1024) // 500MB limit per file
                    throw new InvalidDataException($"File {fileName} is too large for LZX decompression ({uncompressedSize} bytes).");

                byte[] compressedData = _bs.ReadBytes((int)compressedSize);
                byte[] decompressed = XMemCompress.Decompress(compressedData, (int)uncompressedSize);

                outputStream.Write(decompressed, 0, decompressed.Length);
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"Unsupported compression method {method} for {fileName}");
            }
        }

        // Helper to copy exact number of bytes between streams
        private void CopyStream(Stream input, Stream output, int bytesToCopy)
        {
            byte[] buffer = new byte[81920]; // 80KB buffer
            int read;
            while (bytesToCopy > 0 && (read = input.Read(buffer, 0, Math.Min(buffer.Length, bytesToCopy))) > 0)
            {
                output.Write(buffer, 0, read);
                bytesToCopy -= read;
            }
        }

        private long FindEOCD()
        {
            if (_stream.Length < 22) return -1;
            long maxScan = Math.Min(_stream.Length, 65535 + 22);
            long endPos = _stream.Length;

            for (long i = 22; i <= maxScan; i++)
            {
                long pos = endPos - i;
                _bs.Position = pos;
                if (_bs.ReadUInt32() == 0x06054b50) return pos;
            }
            return -1;
        }

        private bool IsDirectory(string path)
        {
            return path.EndsWith("/") || path.EndsWith("\\");
        }

        public void Dispose()
        {
            _stream?.Dispose();
        }
    }
}