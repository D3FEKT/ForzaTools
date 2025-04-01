using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;

namespace ModelbinParser
{
    public class ModelData
    {
        public List<Vector3> Vertices { get; set; }
        public List<Vector3> Normals { get; set; }
        public List<Vector2> UVs { get; set; }
        public List<(int, int, int)> Faces { get; set; }

        public ModelData()
        {
            Vertices = new List<Vector3>();
            Normals = new List<Vector3>();
            UVs = new List<Vector2>();
            Faces = new List<(int, int, int)>();
        }

        public static ModelData ParseObjFile(string filePath)
        {
            var model = new ModelData();
            var lines = File.ReadAllLines(filePath);

            foreach (var line in lines)
            {
                if (line.StartsWith("v "))
                {
                    var parts = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length >= 4 &&
                        float.TryParse(parts[1], out float x) &&
                        float.TryParse(parts[2], out float y) &&
                        float.TryParse(parts[3], out float z))
                    {
                        model.Vertices.Add(new Vector3(x, y, z));
                    }
                }
                else if (line.StartsWith("vn "))
                {
                    var parts = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length >= 4 &&
                        float.TryParse(parts[1], out float x) &&
                        float.TryParse(parts[2], out float y) &&
                        float.TryParse(parts[3], out float z))
                    {
                        model.Normals.Add(new Vector3(x, y, z));
                    }
                }
                else if (line.StartsWith("vt "))
                {
                    var parts = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length >= 3 &&
                        float.TryParse(parts[1], out float u) &&
                        float.TryParse(parts[2], out float v))
                    {
                        model.UVs.Add(new Vector2(u, v));
                    }
                }
                else if (line.StartsWith("f "))
                {
                    var parts = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length >= 4)
                    {
                        var v1 = ParseFaceIndex(parts[1]);
                        var v2 = ParseFaceIndex(parts[2]);
                        var v3 = ParseFaceIndex(parts[3]);

                        model.Faces.Add((v1, v2, v3));
                    }
                }
            }

            return model;
        }

        private static int ParseFaceIndex(string part)
        {
            var indices = part.Split('/');
            if (indices.Length > 0 && int.TryParse(indices[0], out int index))
            {
                return index - 1; // OBJ indices are 1-based
            }
            return -1;
        }
    }
}
