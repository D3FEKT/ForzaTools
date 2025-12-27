using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;

namespace ForzaTools.ForzaAnalyzer.Services
{
    public class ProcessedGeometry
    {
        public byte[][] PositionData;
        public byte[][] NormalUVData;
        public byte[][] IndexData;
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

            // Safety Check 1: Positions
            if (input.Positions == null || input.Positions.Length == 0)
                throw new ArgumentException("Input geometry must have positions.");

            // 1. Calculate Bounds
            Vector3 min = new Vector3(float.MaxValue);
            Vector3 max = new Vector3(float.MinValue);

            foreach (var p in input.Positions)
            {
                min = Vector3.Min(min, p);
                max = Vector3.Max(max, p);
            }

            result.BoundingBoxMin = min;
            result.BoundingBoxMax = max;

            Vector3 center = (min + max) / 2.0f;
            Vector3 range = max - min;

            float maxDimension = Math.Max(range.X, Math.Max(range.Y, range.Z));
            if (maxDimension == 0) maxDimension = 1.0f;

            float expansionFactor = 0.1f;
            float expandedRange = maxDimension * (1.0f + expansionFactor);
            float radius = expandedRange / 2.0f; // Use Radius for scale

            result.PositionScale = new Vector4(radius, radius, radius, 1.0f);
            result.PositionTranslate = new Vector4(center, 0.0f);

            int vertexCount = input.Positions.Length;
            result.PositionData = new byte[vertexCount][];
            result.NormalUVData = new byte[vertexCount][];

            // 2. Generate Vertex Buffers
            for (int i = 0; i < vertexCount; i++)
            {
                Vector3 pos = input.Positions[i];

                // Safety Check 2: Normals and UVs (Handle null arrays or index out of bounds)
                Vector3 norm = (input.Normals != null && i < input.Normals.Length) ? Vector3.Normalize(input.Normals[i]) : Vector3.UnitY;
                Vector2 uv = (input.UVs != null && i < input.UVs.Length) ? input.UVs[i] : Vector2.Zero;

                // --- Buffer 0 (Pos) ---
                using (var ms = new MemoryStream(8))
                using (var bw = new BinaryWriter(ms))
                {
                    bw.Write(QuantizePosition(pos.X, center.X, radius));
                    bw.Write(QuantizePosition(pos.Y, center.Y, radius));
                    bw.Write(QuantizePosition(pos.Z, center.Z, radius));
                    bw.Write((short)(norm.X * 32767));
                    result.PositionData[i] = ms.ToArray();
                }

                // --- Buffer 1 (Norm/UV/Tan/Color) ---
                using (var ms = new MemoryStream(40))
                using (var bw = new BinaryWriter(ms))
                {
                    // 1. Normals (4 bytes)
                    bw.Write((short)(norm.Y * 32767));
                    bw.Write((short)(norm.Z * 32767));

                    // 2. UVs (5 slots -> 20 bytes)
                    ushort u = (ushort)(uv.X * 65535.0f);
                    ushort v = (ushort)((1.0f - uv.Y) * 65535.0f); // V-Flip happens here

                    for (int k = 0; k < 5; k++)
                    {
                        bw.Write(u);
                        bw.Write(v);
                    }

                    // 3. Tangents (3 slots -> 12 bytes) - FIX FOR UPSIDE DOWN REFLECTIONS
                    // Write Neutral Tangent (1,0,0) instead of UVs
                    uint packedTangent = Pack1010102(1.0f, 0.0f, 0.0f, 1.0f);
                    bw.Write(packedTangent);
                    bw.Write(packedTangent);
                    bw.Write(packedTangent);

                    // 4. Color (1 slot -> 4 bytes)
                    // Write White (RGBA 255)
                    bw.Write((uint)0xFFFFFFFF);

                    result.NormalUVData[i] = ms.ToArray();
                }
            }

            // 3. Generate Index Buffer
            // Safety Check 3: Indices
            if (input.Indices == null)
            {
                result.IndexData = new byte[0][];
            }
            else
            {
                int indexCount = input.Indices.Length;
                result.IndexData = new byte[indexCount][];
                for (int i = 0; i < indexCount; i++)
                {
                    result.IndexData[i] = BitConverter.GetBytes(input.Indices[i]);
                }
            }

            return result;
        }

        private short QuantizePosition(float value, float center, float radius)
        {
            float dist = value - center;
            float normalized = dist / radius;
            if (normalized < -1.0f) normalized = -1.0f;
            if (normalized > 1.0f) normalized = 1.0f;
            return (short)(normalized * 32767.0f);
        }

        private uint Pack1010102(float x, float y, float z, float w)
        {
            uint ix = (uint)((x * 0.5f + 0.5f) * 1023.0f);
            uint iy = (uint)((y * 0.5f + 0.5f) * 1023.0f);
            uint iz = (uint)((z * 0.5f + 0.5f) * 1023.0f);
            uint iw = (uint)((w * 0.5f + 0.5f) * 3.0f);
            return ix | (iy << 10) | (iz << 20) | (iw << 30);
        }
    }
}