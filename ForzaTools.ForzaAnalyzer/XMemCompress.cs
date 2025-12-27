using System;
using System.IO;

namespace ForzaTools.ForzaAnalyzer
{
    public static class XMemCompress
    {
        // Removed P/Invokes to xcompress.dll to support 64-bit

        public static byte[] Decompress(byte[] compressedData, int uncompressedSize)
        {
            try
            {
                // Use Managed C# LZX Decoder
                var decoder = new LzxDecoder(65536); // Standard 64KB window for Forza
                byte[] decompressedBuffer = new byte[uncompressedSize];

                // Note: If the stub LzxDecoder above is insufficient, 
                // you might see zeros. Integrating a full open-source LZX file is recommended.
                decoder.Decompress(compressedData, uncompressedSize, decompressedBuffer);

                return decompressedBuffer;
            }
            catch (Exception ex)
            {
                throw new Exception($"Managed LZX Decompression failed: {ex.Message}");
            }
        }
    }
}