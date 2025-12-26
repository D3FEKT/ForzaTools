using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Numerics;

namespace ForzaTools.ForzaAnalyzer.Services
{
    public class ObjParserService
    {
        private class VertexKey : IEquatable<VertexKey>
        {
            public int V, VT, VN;
            public override bool Equals(object obj) => obj is VertexKey k && Equals(k);
            public bool Equals(VertexKey other) => V == other.V && VT == other.VT && VN == other.VN;
            public override int GetHashCode() => HashCode.Combine(V, VT, VN);
        }

        public GeometryInput ParseObj(string filePath)
        {
            List<Vector3> rawPositions = new();
            List<Vector3> rawNormals = new();
            List<Vector2> rawUVs = new();

            // Result Lists
            List<Vector3> finalPositions = new();
            List<Vector3> finalNormals = new();
            List<Vector2> finalUVs = new();
            List<int> finalIndices = new();

            // Index Flattening Cache
            var indexCache = new Dictionary<VertexKey, int>();

            foreach (var line in File.ReadLines(filePath))
            {
                var trim = line.Trim();
                if (string.IsNullOrWhiteSpace(trim) || trim.StartsWith("#")) continue;

                var tokens = trim.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (tokens.Length == 0) continue;

                switch (tokens[0])
                {
                    case "v": // Position
                        if (tokens.Length >= 4)
                            rawPositions.Add(new Vector3(ParseFloat(tokens[1]), ParseFloat(tokens[2]), ParseFloat(tokens[3])));
                        break;

                    case "vn": // Normal
                        if (tokens.Length >= 4)
                            rawNormals.Add(new Vector3(ParseFloat(tokens[1]), ParseFloat(tokens[2]), ParseFloat(tokens[3])));
                        break;

                    case "vt": // UV
                        if (tokens.Length >= 3)
                        {
                            // Invert V coordinate as per main.py
                            rawUVs.Add(new Vector2(ParseFloat(tokens[1]), 1.0f - ParseFloat(tokens[2])));
                        }
                        break;

                    case "f": // Face
                        // Triangulate (Fan method): 0,1,2 -> 0,2,3 -> ...
                        int vCount = tokens.Length - 1;
                        for (int i = 0; i < vCount - 2; i++)
                        {
                            ProcessVertexToken(tokens[1], rawPositions, rawNormals, rawUVs, finalPositions, finalNormals, finalUVs, finalIndices, indexCache);
                            ProcessVertexToken(tokens[2 + i], rawPositions, rawNormals, rawUVs, finalPositions, finalNormals, finalUVs, finalIndices, indexCache);
                            ProcessVertexToken(tokens[3 + i], rawPositions, rawNormals, rawUVs, finalPositions, finalNormals, finalUVs, finalIndices, indexCache);
                        }
                        break;
                }
            }

            // Fallback if no normals provided
            if (finalNormals.Count == 0 && finalPositions.Count > 0)
            {
                for (int i = 0; i < finalPositions.Count; i++) finalNormals.Add(Vector3.UnitY);
            }

            return new GeometryInput
            {
                Name = Path.GetFileNameWithoutExtension(filePath),
                Positions = finalPositions.ToArray(),
                Normals = finalNormals.ToArray(),
                UVs = finalUVs.ToArray(),
                Indices = finalIndices.ToArray()
            };
        }

        private void ProcessVertexToken(
            string token,
            List<Vector3> rPos, List<Vector3> rNorm, List<Vector2> rUV,
            List<Vector3> fPos, List<Vector3> fNorm, List<Vector2> fUV,
            List<int> indices, Dictionary<VertexKey, int> cache)
        {
            var parts = token.Split('/');

            // Parse OBJ indices (1-based, handle negative/missing)
            int vIdx = ParseIndex(parts[0], rPos.Count);
            int vtIdx = (parts.Length > 1 && parts[1].Length > 0) ? ParseIndex(parts[1], rUV.Count) : 0;
            int vnIdx = (parts.Length > 2 && parts[2].Length > 0) ? ParseIndex(parts[2], rNorm.Count) : 0;

            var key = new VertexKey { V = vIdx, VT = vtIdx, VN = vnIdx };

            if (cache.TryGetValue(key, out int existingIndex))
            {
                indices.Add(existingIndex);
            }
            else
            {
                // Create new unified vertex
                int newIndex = fPos.Count;

                // Get Data (Adjust for 1-based index)
                fPos.Add(vIdx > 0 && vIdx <= rPos.Count ? rPos[vIdx - 1] : Vector3.Zero);
                fUV.Add(vtIdx > 0 && vtIdx <= rUV.Count ? rUV[vtIdx - 1] : Vector2.Zero);
                fNorm.Add(vnIdx > 0 && vnIdx <= rNorm.Count ? rNorm[vnIdx - 1] : Vector3.UnitY);

                cache[key] = newIndex;
                indices.Add(newIndex);
            }
        }

        private int ParseIndex(string s, int count)
        {
            if (int.TryParse(s, out int i))
            {
                if (i < 0) return count + i + 1; // Relative
                return i; // Absolute
            }
            return 0;
        }

        private float ParseFloat(string s) => float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out float f) ? f : 0f;
    }
}