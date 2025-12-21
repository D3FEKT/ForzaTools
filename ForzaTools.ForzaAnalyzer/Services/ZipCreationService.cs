using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace ForzaTools.ForzaAnalyzer.Services
{
    public class ZipCreationService
    {
        // --- XMem Structures ---
        [StructLayout(LayoutKind.Sequential)]
        struct XMemCodecParametersLZX
        {
            public int Flags;
            public int WindowSize;
            public int CompressionPartitionSize;
        }

        // --- P/Invokes ---
        [DllImport("xmemcompress.dll", EntryPoint = "XMemCompress")]
        public static extern void XMemCompressNative(
            IntPtr context,
            byte[] destination,
            ref int destSize,
            byte[] source,
            int srcSize
        );

        [DllImport("xmemcompress.dll", EntryPoint = "XMemCreateCompressionContext")]
        public static extern void XMemCreateCompressionContext(
            int codecType,
            IntPtr codecParams,
            int flags,
            ref IntPtr context
        );

        [DllImport("xmemcompress.dll", EntryPoint = "XMemDestroyCompressionContext")]
        public static extern void XMemDestroyCompressionContext(IntPtr context);

        // --- Standard Zip (Deflate) ---
        public async Task CreateStandardZipAsync(string outputPath, List<string> files, List<string> folders)
        {
            await Task.Run(() =>
            {
                using var fs = new FileStream(outputPath, FileMode.Create);
                using var archive = new ZipArchive(fs, ZipArchiveMode.Create);

                foreach (var file in files)
                    archive.CreateEntryFromFile(file, Path.GetFileName(file), CompressionLevel.Optimal);

                foreach (var folder in folders)
                {
                    var allFiles = Directory.GetFiles(folder, "*.*", SearchOption.AllDirectories);
                    foreach (var file in allFiles)
                    {
                        var relative = Path.GetRelativePath(Directory.GetParent(folder).FullName, file);
                        // Standard ZIP always uses forward slashes
                        archive.CreateEntryFromFile(file, relative.Replace("\\", "/"), CompressionLevel.Optimal);
                    }
                }
            });
        }

        // --- Forza Zip (XMemCompress) ---
        public async Task CreateForzaZipAsync(string outputPath, List<string> files, List<string> folders)
        {
            await Task.Run(() =>
            {
                var entries = new List<(string DiskPath, string ArchivePath)>();

                // Add Files
                foreach (var file in files)
                    entries.Add((file, Path.GetFileName(file)));

                // Add Folders
                foreach (var folder in folders)
                {
                    var allFiles = Directory.GetFiles(folder, "*.*", SearchOption.AllDirectories);
                    foreach (var file in allFiles)
                    {
                        var relative = Path.GetRelativePath(Directory.GetParent(folder).FullName, file);
                        // REVERTED: Use Forward Slashes '/' as seen in test.raw
                        entries.Add((file, relative.Replace("\\", "/")));
                    }
                }

                // REVERTED: Use Explicit LZX Parameters
                var lzxParams = new XMemCodecParametersLZX
                {
                    Flags = 0,
                    WindowSize = 65536, // 64KB Window
                    CompressionPartitionSize = 0
                };

                IntPtr paramPtr = Marshal.AllocHGlobal(Marshal.SizeOf(lzxParams));
                Marshal.StructureToPtr(lzxParams, paramPtr, false);

                try
                {
                    using var fs = new FileStream(outputPath, FileMode.Create);
                    using var bw = new BinaryWriter(fs);

                    var directoryEntries = new List<CentralDirectoryInfo>();

                    foreach (var entry in entries)
                    {
                        // Create Fresh Context for EVERY file
                        IntPtr context = IntPtr.Zero;
                        // Use the LZX Parameters
                        XMemCreateCompressionContext(1, paramPtr, 0, ref context);

                        try
                        {
                            byte[] rawData = File.ReadAllBytes(entry.DiskPath);
                            uint crc = Crc32.Compute(rawData);

                            // Compress (Safety buffer for overhead)
                            byte[] compressedData = new byte[rawData.Length + 65536];
                            int compSize = compressedData.Length;

                            XMemCompressNative(context, compressedData, ref compSize, rawData, rawData.Length);

                            long localHeaderOffset = bw.BaseStream.Position;
                            byte[] fileNameBytes = Encoding.ASCII.GetBytes(entry.ArchivePath);
                            (ushort time, ushort date) = GetDosDateTime(DateTime.Now);

                            // --- LOCAL HEADER ---
                            bw.Write(new byte[] { 0x50, 0x4B, 0x03, 0x04 }); // Signature
                            bw.Write((ushort)0x000A); // Version 10
                            bw.Write((ushort)0x0000); // Flags
                            bw.Write((ushort)0x0015); // Method 21 (XMem)
                            bw.Write(time);
                            bw.Write(date);
                            bw.Write(crc);
                            bw.Write((uint)compSize);
                            bw.Write((uint)rawData.Length);
                            bw.Write((ushort)fileNameBytes.Length);
                            bw.Write((ushort)0x0000); // Extra Field Len (0)

                            bw.Write(fileNameBytes);
                            bw.Write(compressedData, 0, compSize);

                            directoryEntries.Add(new CentralDirectoryInfo
                            {
                                Crc = crc,
                                CompressedSize = (uint)compSize,
                                UncompressedSize = (uint)rawData.Length,
                                FileNameBytes = fileNameBytes,
                                LocalHeaderOffset = (uint)localHeaderOffset,
                                Time = time,
                                Date = date
                            });
                        }
                        finally
                        {
                            XMemDestroyCompressionContext(context);
                        }
                    }

                    // --- CENTRAL DIRECTORY ---
                    long centralDirStart = bw.BaseStream.Position;

                    foreach (var dir in directoryEntries)
                    {
                        bw.Write(new byte[] { 0x50, 0x4B, 0x01, 0x02 });
                        bw.Write((ushort)0x000A);
                        bw.Write((ushort)0x000A);
                        bw.Write((ushort)0x0000);
                        bw.Write((ushort)0x0015);
                        bw.Write(dir.Time);
                        bw.Write(dir.Date);
                        bw.Write(dir.Crc);
                        bw.Write(dir.CompressedSize);
                        bw.Write(dir.UncompressedSize);
                        bw.Write((ushort)dir.FileNameBytes.Length);
                        bw.Write((ushort)0x0008); // Extra Field Size (8 Bytes)
                        bw.Write((ushort)0x0000);
                        bw.Write((ushort)0x0000);
                        bw.Write((ushort)0x0000);
                        bw.Write((uint)0x00000000);
                        bw.Write(dir.LocalHeaderOffset);
                        bw.Write(dir.FileNameBytes);

                        // --- EXTRA FIELD (8 Bytes) ---
                        // Tag: 0x1123, Size: 4, Value: Data Offset
                        bw.Write(new byte[] { 0x23, 0x11, 0x04, 0x00 });

                        // Calculate Absolute Data Offset: LocalHeader + 30 + NameLen
                        uint dataOffset = dir.LocalHeaderOffset + 30 + (uint)dir.FileNameBytes.Length;
                        bw.Write(dataOffset);
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
                finally
                {
                    Marshal.FreeHGlobal(paramPtr);
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