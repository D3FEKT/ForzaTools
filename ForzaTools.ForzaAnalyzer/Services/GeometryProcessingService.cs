using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;

namespace ForzaTools.ForzaAnalyzer.Services
{


    public class ProcessedGeometry
    {
        public byte[][] PositionData; // Buffer 0: PosX, PosY, PosZ, NormX (Stride 8)
        public byte[][] NormalUVData; // Buffer 1: NormY, NormZ, U, V x9 (Stride 40)
        public byte[][] IndexData;    // Index Buffer
        public Vector4 PositionScale;
        public Vector4 PositionTranslate;
        public Vector3 BoundingBoxMin;
        public Vector3 BoundingBoxMax;
    }

    public struct GeometryInput
    {
        public string Name;
        public Vector3[] Positions;
        public Vector3[] Normals;
        public Vector2[] UVs;
        public int[] Indices;
    }

    public class GeometryProcessingService
    {
        public ProcessedGeometry ProcessGeometry(GeometryInput input)
        {
            var result = new ProcessedGeometry();

            if (input.Positions == null || input.Positions.Length == 0)
                throw new ArgumentException("Input geometry must have positions.");

            // 1. Calculate Bounds and Center
            Vector3 min = new Vector3(float.MaxValue);
            Vector3 max = new Vector3(float.MinValue);

            foreach (var p in input.Positions)
            {
                min = Vector3.Min(min, p);
                max = Vector3.Max(max, p);
            }

            result.BoundingBoxMin = min;
            result.BoundingBoxMax = max;

            Vector3 center = (min + max) / 2;
            Vector3 range = max - min;

            // Calculate max dimension for uniform scaling
            float maxDimension = Math.Max(range.X, Math.Max(range.Y, range.Z));
            if (maxDimension == 0) maxDimension = 1.0f;

            // Apply 10% expansion padding (matching main.py)
            float expansionFactor = 0.1f;
            float expandedRange = maxDimension * (1.0f + expansionFactor);

            // Set scale and translate vectors
            // Scale: The full range of the bounding box
            // Translate: The center point
            result.PositionScale = new Vector4(expandedRange, expandedRange, expandedRange, 1.0f);
            result.PositionTranslate = new Vector4(center, 0.0f);

            int vertexCount = input.Positions.Length;
            result.PositionData = new byte[vertexCount][];
            result.NormalUVData = new byte[vertexCount][];

            // 2. Quantize Vertices and Generate Buffers
            for (int i = 0; i < vertexCount; i++)
            {
                // Get data, defaulting if missing
                Vector3 pos = input.Positions[i];
                Vector3 norm = (input.Normals != null && i < input.Normals.Length) ? Vector3.Normalize(input.Normals[i]) : Vector3.UnitY;
                Vector2 uv = (input.UVs != null && i < input.UVs.Length) ? input.UVs[i] : Vector2.Zero;

                // --- Buffer 0 (Stride 8): PosX, PosY, PosZ, NormX ---
                using (var ms = new MemoryStream(8))
                using (var bw = new BinaryWriter(ms))
                {
                    // Position Quantization: (Value - Center) / Range + 0.5
                    // Then map 0..1 to -32768..32767
                    bw.Write(QuantizePosition(pos.X, center.X, expandedRange));
                    bw.Write(QuantizePosition(pos.Y, center.Y, expandedRange));
                    bw.Write(QuantizePosition(pos.Z, center.Z, expandedRange));

                    // NormX Quantization: Value * 32767
                    bw.Write((short)(norm.X * 32767));

                    result.PositionData[i] = ms.ToArray();
                }

                // --- Buffer 1 (Stride 40): NormY, NormZ, U, V (repeated) ---
                using (var ms = new MemoryStream(40))
                using (var bw = new BinaryWriter(ms))
                {
                    // NormY, NormZ
                    bw.Write((short)(norm.Y * 32767));
                    bw.Write((short)(norm.Z * 32767));

                    // UV Quantization: Value * 65535
                    // Note: V is flipped (1.0 - V) in main.py logic
                    ushort u = (ushort)(uv.X * 65535);
                    ushort v = (ushort)((1.0f - uv.Y) * 65535);

                    // Write UV pair 9 times to fill stride (1 + 8 extra from main.py loop)
                    for (int k = 0; k < 9; k++)
                    {
                        bw.Write(u);
                        bw.Write(v);
                    }

                    result.NormalUVData[i] = ms.ToArray();
                }
            }

            // 3. Generate Index Buffer
            // We assume input indices are 0-based. main.py reads 1-based OBJ and subtracts 1.
            int indexCount = input.Indices.Length;
            // The BufferHeader expects an array of byte arrays, where each inner array is one element.
            // For IndexBuffer, one element is one int32 index (4 bytes).
            result.IndexData = new byte[indexCount][];

            for (int i = 0; i < indexCount; i++)
            {
                result.IndexData[i] = BitConverter.GetBytes(input.Indices[i]);
            }

            return result;
        }

        private short QuantizePosition(float value, float center, float range)
        {
            // Center the value relative to the model center
            float relative = value - center;

            // Normalize to 0.0 - 1.0 based on the expanded range
            // (relative / range) is -0.5 to 0.5, so add 0.5
            float scaled = 0.5f + (relative / range);

            // Clamp
            if (scaled < 0.0f) scaled = 0.0f;
            if (scaled > 1.0f) scaled = 1.0f;

            // Map to Int16 range
            return (short)((scaled * 65535f) - 32768f);
        }
    }
}