using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Threading.Tasks;

namespace ForzaTools.ForzaAnalyzer.Services
{
    public class ZipCreationService
    {
        // Removed 32-bit P/Invokes (XMemCompressNative, etc.)

        public async Task CreateStandardZipAsync(string outputPath, List<string> files, List<string> folders)
        {
            // (Keep existing code, strictly referencing System.IO.Compression)
            await Task.Run(() =>
            {
                using var fs = new FileStream(outputPath, FileMode.Create);
                using var archive = new System.IO.Compression.ZipArchive(fs, System.IO.Compression.ZipArchiveMode.Create);

                foreach (var file in files)
                    archive.CreateEntryFromFile(file, Path.GetFileName(file), System.IO.Compression.CompressionLevel.Optimal);

                foreach (var folder in folders)
                {
                    var allFiles = Directory.GetFiles(folder, "*.*", SearchOption.AllDirectories);
                    foreach (var file in allFiles)
                    {
                        var relative = Path.GetRelativePath(Directory.GetParent(folder).FullName, file);
                        archive.CreateEntryFromFile(file, relative.Replace("\\", "/"), System.IO.Compression.CompressionLevel.Optimal);
                    }
                }
            });
        }

        public async Task CreateForzaZipAsync(string outputPath, List<string> files, List<string> folders)
        {
            await Task.Run(() =>
            {
                var entries = new List<(string DiskPath, string ArchivePath)>();

                foreach (var file in files)
                    entries.Add((file, Path.GetFileName(file)));

                foreach (var folder in folders)
                {
                    var allFiles = Directory.GetFiles(folder, "*.*", SearchOption.AllDirectories);
                    foreach (var file in allFiles)
                    {
                        var relative = Path.GetRelativePath(Directory.GetParent(folder).FullName, file);
                        entries.Add((file, relative.Replace("\\", "/")));
                    }
                }

                try
                {
                    using var fs = new FileStream(outputPath, FileMode.Create);
                    using var bw = new BinaryWriter(fs);

                    var directoryEntries = new List<CentralDirectoryInfo>();

                    foreach (var entry in entries)
                    {
                        // 64-BIT COMPATIBILITY CHANGE:
                        // Instead of LZX (which requires 32-bit DLL), we use STORE (Method 0).
                        // Forza games accept uncompressed files in the zip wrapper.

                        byte[] rawData = File.ReadAllBytes(entry.DiskPath);
                        uint crc = Crc32.Compute(rawData);

                        long localHeaderOffset = bw.BaseStream.Position;
                        byte[] fileNameBytes = Encoding.ASCII.GetBytes(entry.ArchivePath);
                        (ushort time, ushort date) = GetDosDateTime(DateTime.Now);

                        // --- LOCAL HEADER ---
                        bw.Write(new byte[] { 0x50, 0x4B, 0x03, 0x04 });
                        bw.Write((ushort)0x000A); // Version 10
                        bw.Write((ushort)0x0000); // Flags
                        bw.Write((ushort)0x0000); // METHOD 0 (Store) - Compatible with 64-bit
                        bw.Write(time);
                        bw.Write(date);
                        bw.Write(crc);
                        bw.Write((uint)rawData.Length); // Compressed = Uncompressed
                        bw.Write((uint)rawData.Length);
                        bw.Write((ushort)fileNameBytes.Length);
                        bw.Write((ushort)0x0000);

                        bw.Write(fileNameBytes);
                        bw.Write(rawData); // Write raw data directly

                        directoryEntries.Add(new CentralDirectoryInfo
                        {
                            Crc = crc,
                            CompressedSize = (uint)rawData.Length,
                            UncompressedSize = (uint)rawData.Length,
                            FileNameBytes = fileNameBytes,
                            LocalHeaderOffset = (uint)localHeaderOffset,
                            Time = time,
                            Date = date
                        });
                    }

                    // --- CENTRAL DIRECTORY ---
                    long centralDirStart = bw.BaseStream.Position;

                    foreach (var dir in directoryEntries)
                    {
                        bw.Write(new byte[] { 0x50, 0x4B, 0x01, 0x02 });
                        bw.Write((ushort)0x000A);
                        bw.Write((ushort)0x000A);
                        bw.Write((ushort)0x0000);
                        bw.Write((ushort)0x0000); // METHOD 0 (Store)
                        bw.Write(dir.Time);
                        bw.Write(dir.Date);
                        bw.Write(dir.Crc);
                        bw.Write(dir.CompressedSize);
                        bw.Write(dir.UncompressedSize);
                        bw.Write((ushort)dir.FileNameBytes.Length);
                        bw.Write((ushort)0x0000); // No Extra Field needed for Store
                        bw.Write((ushort)0x0000);
                        bw.Write((ushort)0x0000);
                        bw.Write((ushort)0x0000);
                        bw.Write((uint)0x00000000);
                        bw.Write(dir.LocalHeaderOffset);
                        bw.Write(dir.FileNameBytes);
                        // No extra data written here for Store method
                    }

                    long centralDirSize = bw.BaseStream.Position - centralDirStart;

                    // --- EOCD ---
                    bw.Write(new byte[] { 0x50, 0x4B, 0x05, 0x06 });
                    bw.Write((ushort)0x0000);
                    bw.Write((ushort)0x0000);
                    bw.Write((ushort)directoryEntries.Count);
                    bw.Write((ushort)directoryEntries.Count);
                    bw.Write((uint)centralDirSize);
                    bw.Write((uint)centralDirStart);
                    bw.Write((ushort)0x0000);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine("Zip Error: " + ex.Message);
                    throw;
                }
            });
        }

        // --- Helpers ---
        private struct CentralDirectoryInfo
        {
            public uint Crc;
            public uint CompressedSize;
            public uint UncompressedSize;
            public byte[] FileNameBytes;
            public uint LocalHeaderOffset;
            public ushort Time;
            public ushort Date;
        }

        private static (ushort Time, ushort Date) GetDosDateTime(DateTime dt)
        {
            uint time = (uint)((dt.Hour << 11) | (dt.Minute << 5) | (dt.Second / 2));
            uint date = (uint)(((dt.Year - 1980) << 9) | (dt.Month << 5) | dt.Day);
            return ((ushort)time, (ushort)date);
        }

        public static class Crc32
        {
            private static readonly uint[] Table;
            static Crc32()
            {
                uint poly = 0xedb88320;
                Table = new uint[256];
                for (uint i = 0; i < 256; i++)
                {
                    uint temp = i;
                    for (int j = 8; j > 0; j--)
                    {
                        if ((temp & 1) == 1) temp = (temp >> 1) ^ poly;
                        else temp >>= 1;
                    }
                    Table[i] = temp;
                }
            }
            public static uint Compute(byte[] bytes)
            {
                uint crc = 0xffffffff;
                for (int i = 0; i < bytes.Length; i++)
                {
                    byte index = (byte)((crc & 0xff) ^ bytes[i]);
                    crc = (crc >> 8) ^ Table[index];
                }
                return ~crc;
            }
        }
    }
}