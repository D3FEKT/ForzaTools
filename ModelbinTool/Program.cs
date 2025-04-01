using System;
using System.Collections.Generic;
using System.IO;
using System.Globalization;
using System.Linq;
using System.Numerics;
using Syroot.BinaryData;
using ForzaTools.Bundles;
using ForzaTools.Bundles.Blobs;

class ModelbinRebuilder
{
    static void Main(string[] args)
    {
        if (args.Length < 3)
        {
            Console.WriteLine("Usage: ModelbinRebuilder input.modelbin input.obj output.modelbin");
            return;
        }

        string modelbinPath = args[0];
        string objPath = args[1];
        string outputModelbinPath = args[2];

        Console.WriteLine($"Reading existing modelbin file: {modelbinPath}");

        // Load the existing .modelbin as a Bundle.
        Bundle modelbinBundle;
        using (FileStream fs = new FileStream(modelbinPath, FileMode.Open, FileAccess.Read))
        {
            modelbinBundle = new Bundle();
            modelbinBundle.Load(fs);
        }

        Console.WriteLine($"Reading OBJ file: {objPath}");
        List<Vector3> vertices = new List<Vector3>();
        List<Vector3> normals = new List<Vector3>();
        List<Vector2> uvs = new List<Vector2>();
        List<int> indices = new List<int>();

        // Read OBJ file line-by-line.
        foreach (string line in File.ReadLines(objPath))
        {
            string[] parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0)
                continue;

            if (parts[0] == "v") // Vertex positions
            {
                vertices.Add(new Vector3(
                    float.Parse(parts[1], CultureInfo.InvariantCulture),
                    float.Parse(parts[2], CultureInfo.InvariantCulture),
                    float.Parse(parts[3], CultureInfo.InvariantCulture)));
            }
            else if (parts[0] == "vn") // Normals
            {
                normals.Add(new Vector3(
                    float.Parse(parts[1], CultureInfo.InvariantCulture),
                    float.Parse(parts[2], CultureInfo.InvariantCulture),
                    float.Parse(parts[3], CultureInfo.InvariantCulture)));
            }
            else if (parts[0] == "vt") // UV coordinates
            {
                uvs.Add(new Vector2(
                    float.Parse(parts[1], CultureInfo.InvariantCulture),
                    float.Parse(parts[2], CultureInfo.InvariantCulture)));
            }
            else if (parts[0] == "f") // Faces (only triangulated faces assumed)
            {
                // Here we assume that faces are defined as "f v1/vt1/vn1 v2/vt2/vn2 v3/vt3/vn3"
                // (Indices in OBJ are 1-based)
                for (int i = 1; i <= 3; i++)
                {
                    string[] vertData = parts[i].Split('/');
                    int vertexIndex = int.Parse(vertData[0]) - 1;
                    indices.Add(vertexIndex);
                }
            }
        }

        Console.WriteLine($"OBJ Data -> Vertices: {vertices.Count}, Normals: {normals.Count}, UVs: {uvs.Count}, Indices: {indices.Count}");

        // Update Model Blob if needed.
        ModelBlob modelBlob = modelbinBundle.GetBlobById<ModelBlob>(Bundle.TAG_BLOB_Model, 0);
        if (modelBlob == null)
        {
            Console.WriteLine("Error: No Model Blob found!");
            return;
        }
        // (Additional model-level changes could be applied here.)

        // Update Mesh Blob.
        MeshBlob meshBlob = modelbinBundle.GetBlobById<MeshBlob>(Bundle.TAG_BLOB_Mesh, 0);
        if (meshBlob == null)
        {
            Console.WriteLine("Error: No Mesh Blob found!");
            return;
        }
        meshBlob.NumVerts = (uint)vertices.Count;
        meshBlob.FaceCount = (uint)(indices.Count / 3);

        // Fetch all Vertex Buffer Blobs.
        List<VertexBufferBlob> vertexBufferBlobs = modelbinBundle.Blobs
            .Where(blob => blob.Tag == Bundle.TAG_BLOB_VertexBuffer)
            .Cast<VertexBufferBlob>()
            .ToList();

        if (vertexBufferBlobs.Count < 2)
        {
            Console.WriteLine("Error: Not enough vertex buffers found!");
            return;
        }

        // --- Update the Position Buffer (first Vertex Buffer) ---
        VertexBufferBlob positionBuffer = vertexBufferBlobs[0];
        // For positions, we assume a 4-component float vector (X,Y,Z,W) => 4 floats (16 bytes)
        positionBuffer.Header.BufferWidth = 16;
        positionBuffer.Header.NumElements = (byte)vertices.Count;
        positionBuffer.Header.Data = new byte[vertices.Count][];

        for (int i = 0; i < vertices.Count; i++)
        {
            using (MemoryStream vBuffer = new MemoryStream())
            using (BinaryStream bs = new BinaryStream(vBuffer))
            {
                bs.WriteSingle(vertices[i].X);
                bs.WriteSingle(vertices[i].Y);
                bs.WriteSingle(vertices[i].Z);
                bs.WriteSingle(1.0f);  // W component set to 1.0f
                positionBuffer.Header.Data[i] = vBuffer.ToArray();
            }
        }

        // --- Update the Normal Buffer (second Vertex Buffer) ---
        VertexBufferBlob normalBuffer = vertexBufferBlobs[1];
        // For normals (and tangents if present), we assume a 4-component float vector per vertex (16 bytes).
        normalBuffer.Header.BufferWidth = 16;
        normalBuffer.Header.NumElements = (byte)normals.Count;
        normalBuffer.Header.Data = new byte[normals.Count][];

        for (int i = 0; i < normals.Count; i++)
        {
            using (MemoryStream nBuffer = new MemoryStream())
            using (BinaryStream bs = new BinaryStream(nBuffer))
            {
                bs.WriteSingle(normals[i].X);
                bs.WriteSingle(normals[i].Y);
                bs.WriteSingle(normals[i].Z);
                bs.WriteSingle(1.0f);  // Placeholder for tangent or extra data
                normalBuffer.Header.Data[i] = nBuffer.ToArray();
            }
        }

        // --- Update the UV Buffer (third Vertex Buffer), if it exists and there are UVs ---
        if (vertexBufferBlobs.Count > 2 && uvs.Count > 0)
        {
            VertexBufferBlob uvBuffer = vertexBufferBlobs[2];
            // For UVs, we assume 2 x UInt16 per vertex (4 bytes total)
            uvBuffer.Header.BufferWidth = 4;
            uvBuffer.Header.NumElements = (byte)uvs.Count;
            uvBuffer.Header.Data = new byte[uvs.Count][];

            for (int i = 0; i < uvs.Count; i++)
            {
                using (MemoryStream uvStream = new MemoryStream())
                using (BinaryStream bs = new BinaryStream(uvStream))
                {
                    // Scale UV values from [0,1] to 0..65535.
                    bs.WriteUInt16((ushort)(uvs[i].X * 65535));
                    bs.WriteUInt16((ushort)(uvs[i].Y * 65535));
                    uvBuffer.Header.Data[i] = uvStream.ToArray();
                }
            }
        }

        // At this point you should recalculate any blob offsets, sizes, and update header fields accordingly.
        // This is assumed to be done within your Bundle.Serialize() method.

        Console.WriteLine($"Saving modified modelbin file: {outputModelbinPath}");
        using (FileStream fs = new FileStream(outputModelbinPath, FileMode.Create, FileAccess.Write))
        {
            modelbinBundle.Serialize(fs);
        }

        Console.WriteLine("Successfully saved modified `.modelbin` file!");
    }
}
