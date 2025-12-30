using System;
using System.IO;

namespace ForzaTools.ForzaAnalyzer
{
    public static class XMemCompress
    {
        // Standard XMemCompress window size is 64KB.
        // The LzxDecoder expects "window bits", so 2^16 = 65536 bytes.
        private const int LZX_WINDOW_BITS = 16;

        /// <summary>
        /// Decompresses data using the pure C# LzxDecoder instead of xcompress.dll
        /// </summary>
        public static byte[] Decompress(byte[] compressedData, int uncompressedSize)
        {
            try
            {
                // Wrap the byte arrays in Streams as required by the new LzxDecoder
                using (var input = new MemoryStream(compressedData))
                using (var output = new MemoryStream(uncompressedSize))
                {
                    // Initialize the decoder with 16 bits (64KB window)
                    var decoder = new LzxDecoder(LZX_WINDOW_BITS);

                    // Perform decompression
                    // Signature: Decompress(Stream inData, int inLen, Stream outData, int outLen)
                    int result = decoder.Decompress(input, compressedData.Length, output, uncompressedSize);

                    // The LzxDecoder returns 0 on success, non-zero on failure
                    if (result != 0)
                    {
                        throw new Exception($"LZX Decompression failed with error code: {result}");
                    }

                    // Verification check similar to the original code
                    if (output.Length != uncompressedSize)
                    {
                        throw new InvalidDataException($"Decompression size mismatch. Expected {uncompressedSize}, got {output.Length}.");
                    }

                    return output.ToArray();
                }
            }
            catch (Exception ex)
            {
                // Preserve the error context for debugging
                throw new Exception($"Managed LZX Decompression failed: {ex.Message}", ex);
            }
        }
    }
}