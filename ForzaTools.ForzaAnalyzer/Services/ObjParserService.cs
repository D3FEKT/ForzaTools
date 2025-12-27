using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Numerics;

namespace ForzaTools.ForzaAnalyzer.Services
{
    // Holds data for a specific group/object in the OBJ
    public class ObjGroup
    {
        public string Name { get; set; }
        public string MaterialName { get; set; } // The material used by this group
        public List<int> Indices { get; set; } = new List<int>();
    }

    // Represents the entire loaded OBJ scene
    public class ObjSceneData
    {
        public string Name { get; set; }
        public string MaterialLib { get; set; } // The .mtl filename defined in the OBJ
        public Vector3[] Positions { get; set; }
        public Vector3[] Normals { get; set; }
        public Vector2[] UVs { get; set; }
        public List<ObjGroup> Groups { get; set; } = new List<ObjGroup>();
    }

    public class ObjParserService
    {
        private class VertexKey : IEquatable<VertexKey>
        {
            public int V, VT, VN;
            public override bool Equals(object obj) => obj is VertexKey k && Equals(k);
            public bool Equals(VertexKey other) => V == other.V && VT == other.VT && VN == other.VN;
            public override int GetHashCode() => HashCode.Combine(V, VT, VN);
        }

        public ObjSceneData ParseObj(string filePath)
        {
            List<Vector3> rawPositions = new();
            List<Vector3> rawNormals = new();
            List<Vector2> rawUVs = new();

            List<Vector3> finalPositions = new();
            List<Vector3> finalNormals = new();
            List<Vector2> finalUVs = new();

            var groups = new List<ObjGroup>();
            var currentGroup = new ObjGroup { Name = "Default" };
            groups.Add(currentGroup);

            string materialLib = null;
            var indexCache = new Dictionary<VertexKey, int>();

            foreach (var line in File.ReadLines(filePath))
            {
                var trim = line.Trim();
                if (string.IsNullOrWhiteSpace(trim) || trim.StartsWith("#")) continue;

                var tokens = trim.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (tokens.Length == 0) continue;

                switch (tokens[0])
                {
                    case "mtllib":
                        if (tokens.Length > 1) materialLib = tokens[1];
                        break;

                    case "usemtl":
                        if (tokens.Length > 1) currentGroup.MaterialName = tokens[1];
                        break;

                    case "g":
                    case "o":
                        string groupName = tokens.Length > 1 ? tokens[1] : $"Group_{groups.Count}";
                        if (currentGroup.Indices.Count > 0)
                        {
                            currentGroup = new ObjGroup { Name = groupName };
                            groups.Add(currentGroup);
                        }
                        else
                        {
                            currentGroup.Name = groupName;
                        }
                        break;

                    case "v":
                        if (tokens.Length >= 4)
                            rawPositions.Add(new Vector3(ParseFloat(tokens[1]), ParseFloat(tokens[2]), -ParseFloat(tokens[3])));
                        break;

                    case "vn":
                        if (tokens.Length >= 4)
                            rawNormals.Add(new Vector3(ParseFloat(tokens[1]), ParseFloat(tokens[2]), -ParseFloat(tokens[3])));
                        break;

                    case "vt":
                        if (tokens.Length >= 3)
                            rawUVs.Add(new Vector2(ParseFloat(tokens[1]), ParseFloat(tokens[2])));
                        break;

                    case "f":
                        int vCount = tokens.Length - 1;
                        for (int i = 0; i < vCount - 2; i++)
                        {
                            ProcessVertexToken(tokens[1], rawPositions, rawNormals, rawUVs, finalPositions, finalNormals, finalUVs, currentGroup.Indices, indexCache);
                            ProcessVertexToken(tokens[2 + i], rawPositions, rawNormals, rawUVs, finalPositions, finalNormals, finalUVs, currentGroup.Indices, indexCache);
                            ProcessVertexToken(tokens[3 + i], rawPositions, rawNormals, rawUVs, finalPositions, finalNormals, finalUVs, currentGroup.Indices, indexCache);
                        }
                        break;
                }
            }

            if (finalNormals.Count == 0 && finalPositions.Count > 0)
            {
                for (int i = 0; i < finalPositions.Count; i++) finalNormals.Add(Vector3.UnitY);
            }

            return new ObjSceneData
            {
                Name = Path.GetFileNameWithoutExtension(filePath),
                MaterialLib = materialLib,
                Positions = finalPositions.ToArray(),
                Normals = finalNormals.ToArray(),
                UVs = finalUVs.ToArray(),
                Groups = groups
            };
        }

        // Returns a Dictionary mapping MaterialName -> DiffuseTextureMap
        public Dictionary<string, string> ParseMtl(string filePath)
        {
            var result = new Dictionary<string, string>();
            string currentMaterial = null;

            if (!File.Exists(filePath)) return result;

            foreach (var line in File.ReadLines(filePath))
            {
                var trim = line.Trim();
                if (string.IsNullOrWhiteSpace(trim) || trim.StartsWith("#")) continue;

                var tokens = trim.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (tokens.Length < 2) continue;

                if (tokens[0] == "newmtl")
                {
                    currentMaterial = tokens[1];
                }
                else if (tokens[0] == "map_Kd" && currentMaterial != null)
                {
                    // Map_Kd can contain full paths, we usually just want the filename
                    string texName = Path.GetFileName(tokens[1]);
                    result[currentMaterial] = texName;
                }
            }
            return result;
        }

        private void ProcessVertexToken(
            string token,
            List<Vector3> rPos, List<Vector3> rNorm, List<Vector2> rUV,
            List<Vector3> fPos, List<Vector3> fNorm, List<Vector2> fUV,
            List<int> indices, Dictionary<VertexKey, int> cache)
        {
            var parts = token.Split('/');

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
                int newIndex = fPos.Count;

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
                if (i < 0) return count + i + 1;
                return i;
            }
            return 0;
        }

        private float ParseFloat(string s) => float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out float f) ? f : 0f;
    }
}