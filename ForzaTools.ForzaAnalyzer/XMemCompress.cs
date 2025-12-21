using System;
using System.IO;
using System.Runtime.InteropServices;

namespace ForzaTools.ForzaAnalyzer
{
    public static class XMemCompress
    {
        public enum XMemCodecType
        {
            Default = 0,
            LZX = 1
        }

        [DllImport("xcompress.dll", EntryPoint = "XMemCreateDecompressionContext")]
        public static extern int XMemCreateDecompressionContext(
            XMemCodecType codecType,
            int pCodecParams,
            int flags, ref int pContext);

        [DllImport("xcompress.dll", EntryPoint = "XMemDestroyDecompressionContext")]
        public static extern void XMemDestroyDecompressionContext(int context);

        [DllImport("xcompress.dll", EntryPoint = "XMemResetDecompressionContext")]
        public static extern int XMemResetDecompressionContext(int context);

        [DllImport("xcompress.dll", EntryPoint = "XMemDecompressStream")]
        public static extern int XMemDecompressStream(int context,
            byte[] pDestination, ref int pDestSize,
            byte[] pSource, ref int pSrcSize);

        public static byte[] Decompress(byte[] compressedData, int uncompressedSize)
        {
            int context = 0;
            try
            {
                // 1. Create Decompression Context (LZX)
                int result = XMemCreateDecompressionContext(
                    XMemCodecType.LZX,
                    0,
                    0,
                    ref context);

                if (result != 0)
                    throw new Exception($"XMemCreateDecompressionContext failed with error: {result}");

                // 2. Reset Context
                XMemResetDecompressionContext(context);

                // 3. Decompress
                byte[] decompressedBuffer = new byte[uncompressedSize];
                int destSize = uncompressedSize;
                int srcSize = compressedData.Length;

                result = XMemDecompressStream(
                    context,
                    decompressedBuffer,
                    ref destSize,
                    compressedData,
                    ref srcSize);

                if (result != 0)
                    throw new Exception($"XMemDecompressStream failed with error: {result}");

                if (destSize != uncompressedSize)
                    throw new InvalidDataException($"Decompression size mismatch. Expected {uncompressedSize}, got {destSize}.");

                return decompressedBuffer;
            }
            catch (DllNotFoundException)
            {
                throw new Exception("xcompress.dll was not found. Please place the 32-bit xcompress.dll in the application folder and ensure the app is running in x86 mode.");
            }
            catch (BadImageFormatException)
            {
                throw new Exception("xcompress.dll format error. This DLL is likely 32-bit; please ensure the application is built for 'x86' platform target, not 'Any CPU' or 'x64'.");
            }
            finally
            {
                // 4. Destroy Context
                if (context != 0)
                    XMemDestroyDecompressionContext(context);
            }
        }
    }
}