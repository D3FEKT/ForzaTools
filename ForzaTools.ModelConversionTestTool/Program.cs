namespace ForzaTools.ModelConversionTestTool;

using ForzaTools.Bundles;
using ForzaTools.Bundles.Blobs;
using ForzaTools.Utils;
using ForzaTools.Shared;
using System;
using System.Windows.Forms;

internal class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        // Check if there are command-line arguments for backward compatibility
        if (args.Length >= 2)
        {
            // Use command-line mode for backward compatibility
            try
            {
                using var fs = new FileStream(args[0], FileMode.Open);
                var bundle = new Bundle();
                bundle.Load(fs);

                MakeFH5Compatible(bundle);

                using var output = new FileStream(args[1], FileMode.Create);
                bundle.Serialize(output);

                Console.WriteLine("Conversion completed successfully.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
                Environment.Exit(1);
            }
        }
        else
        {
            // Use GUI mode
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new ModelConverterForm());
        }
    }

    // Method to make FH4 models compatible with FH5
    public static void MakeFH5Compatible(Bundle bundle)
    {
        try
        {
            // Attempts to make black models imported from FH4 not black

            // Get the main lod mesh
            MeshBlob meshBlob = (MeshBlob)bundle.GetBlobByIndex(Bundle.TAG_BLOB_Mesh, 0);
            if (meshBlob == null)
            {
                throw new Exception("No mesh blob found in the model.");
            }

            // Add the required 3rd tangent component to the main vertex layout
            VertexLayoutBlob layout = (VertexLayoutBlob)bundle.GetBlobByIndex(Bundle.TAG_BLOB_VertexLayout, meshBlob.VertexLayoutIndex);
            if (layout == null)
            {
                throw new Exception("No vertex layout blob found in the model.");
            }

            // Check if the model already has a third tangent component
            bool hasThirdTangentComponent = false;
            foreach (var element in layout.Elements)
            {
                if (layout.SemanticNames[element.SemanticNameIndex] == "TANGENT" && element.SemanticIndex == 2)
                {
                    hasThirdTangentComponent = true;
                    break;
                }
            }

            // Only add the third tangent component if it doesn't already exist
            if (!hasThirdTangentComponent)
            {
                int tangentIndex = layout.Elements.FindIndex(e => layout.SemanticNames[e.SemanticNameIndex] == "TANGENT");
                if (tangentIndex < 0)
                {
                    throw new Exception("No tangent semantic found in vertex layout.");
                }

                D3D12_INPUT_LAYOUT_DESC thirdTangentComponent = new D3D12_INPUT_LAYOUT_DESC()
                {
                    SemanticNameIndex = (short)layout.SemanticNames.IndexOf("TANGENT"),
                    Format = DXGI_FORMAT.DXGI_FORMAT_R10G10B10A2_UNORM,
                    InputSlot = 1,
                    SemanticIndex = 2,
                    AlignedByteOffset = -1,
                    InstanceDataStepRate = 0,
                };

                layout.Elements.Insert(tangentIndex + 2, thirdTangentComponent);
                layout.PackedFormats.Insert(tangentIndex + 2, DXGI_FORMAT.DXGI_FORMAT_R8G8_TYPELESS); // Needed otherwise invisible
                layout.Flags |= 0x80; // Required

                int offset = layout.GetDataOffsetOfElement("TANGENT", 2);
                VertexBufferBlob buffer = (VertexBufferBlob)bundle.GetBlobByIndex(Bundle.TAG_BLOB_VertexBuffer, 1);
                if (buffer == null)
                {
                    throw new Exception("No vertex buffer blob found in the model.");
                }

                for (int i = 0; i < buffer.Header.Data.Length; i++)
                {
                    var l = buffer.Header.Data[i].ToList();
                    l.Insert(offset, 0xFF);
                    l.Insert(offset + 1, 0xFF);
                    l.Insert(offset + 2, 0xFF);
                    l.Insert(offset + 3, 0xFF);

                    buffer.Header.Data[i] = l.ToArray();
                }

                byte totalSize = layout.GetTotalVertexSize();
                buffer.Header.BufferWidth = totalSize;
                buffer.Header.NumElements = (byte)layout.Elements.Count;
            }
        }
        catch (Exception ex)
        {
            throw new Exception($"Failed to make model FH5 compatible: {ex.Message}", ex);
        }
    }
}
