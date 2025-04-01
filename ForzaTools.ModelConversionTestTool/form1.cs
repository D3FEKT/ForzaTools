namespace ForzaTools.ModelConversionTestTool;

using ForzaTools.Bundles;
using ForzaTools.Bundles.Blobs;
using ForzaTools.Utils;
using ForzaTools.Shared;
using System;
using System.IO;
using System.Windows.Forms;
using System.Drawing;

public enum ModelGameVersion
{
    FH4,
    FH5,
    FM  // Forza Motorsport
}

public class ModelConverterForm : Form
{
    private Button openButton;
    private Button saveButton;
    private TextBox inputPathTextBox;
    private TextBox outputPathTextBox;
    private Label statusLabel;
    private Bundle bundle;
    private GroupBox conversionGroupBox;
    private RadioButton rbFH4ToFH5;
    private RadioButton rbFH5ToFH4;
    private RadioButton rbFMToFH5;
    private RadioButton rbOriginal;
    private Label versionInfoLabel;

    public ModelConverterForm()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        Text = "Forza Model Conversion Tool";
        Size = new Size(600, 350);  // Increased height for additional controls
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;

        // Create input file controls
        Label inputLabel = new Label
        {
            Text = "Input .modelbin file:",
            Location = new Point(20, 20),
            AutoSize = true
        };

        inputPathTextBox = new TextBox
        {
            Location = new Point(20, 45),
            Width = 450,
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
        };

        openButton = new Button
        {
            Text = "Browse...",
            Location = new Point(480, 43),
            Width = 80,
            Anchor = AnchorStyles.Top | AnchorStyles.Right
        };
        openButton.Click += OpenButton_Click;

        // Version info label
        versionInfoLabel = new Label
        {
            Text = "Version: N/A",
            Location = new Point(20, 70),
            AutoSize = true,
            Font = new Font(Font, FontStyle.Bold)
        };

        // Create conversion options group
        conversionGroupBox = new GroupBox
        {
            Text = "Conversion Options",
            Location = new Point(20, 95),
            Width = 540,
            Height = 110,
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
        };

        rbOriginal = new RadioButton
        {
            Text = "Keep Original Format",
            Location = new Point(15, 25),
            AutoSize = true,
            Checked = true
        };

        rbFH4ToFH5 = new RadioButton
        {
            Text = "FH4 to FH5 (Add 3rd tangent component)",
            Location = new Point(15, 50),
            AutoSize = true
        };

        rbFH5ToFH4 = new RadioButton
        {
            Text = "FH5 to FH4 (Remove 3rd tangent component)",
            Location = new Point(15, 75),
            AutoSize = true
        };

        rbFMToFH5 = new RadioButton
        {
            Text = "FM to FH5 (Experimental)",
            Location = new Point(250, 25),
            AutoSize = true
        };

        conversionGroupBox.Controls.AddRange(new Control[] { rbOriginal, rbFH4ToFH5, rbFH5ToFH4, rbFMToFH5 });

        // Create output file controls
        Label outputLabel = new Label
        {
            Text = "Output .modelbin file:",
            Location = new Point(20, 215),
            AutoSize = true
        };

        outputPathTextBox = new TextBox
        {
            Location = new Point(20, 240),
            Width = 450,
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
        };

        saveButton = new Button
        {
            Text = "Browse...",
            Location = new Point(480, 238),
            Width = 80,
            Anchor = AnchorStyles.Top | AnchorStyles.Right
        };
        saveButton.Click += SaveButton_Click;

        // Create convert button
        Button convertButton = new Button
        {
            Text = "Convert",
            Location = new Point(250, 275),
            Width = 100,
            Height = 30,
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
        };
        convertButton.Click += ConvertButton_Click;

        // Status label
        statusLabel = new Label
        {
            Text = "Ready",
            Location = new Point(20, 315),
            AutoSize = true,
            Anchor = AnchorStyles.Bottom | AnchorStyles.Left
        };

        // Add controls to form
        Controls.Add(inputLabel);
        Controls.Add(inputPathTextBox);
        Controls.Add(openButton);
        Controls.Add(versionInfoLabel);
        Controls.Add(conversionGroupBox);
        Controls.Add(outputLabel);
        Controls.Add(outputPathTextBox);
        Controls.Add(saveButton);
        Controls.Add(convertButton);
        Controls.Add(statusLabel);

        // Initially disable conversion options until a file is loaded
        conversionGroupBox.Enabled = false;
    }

    private void OpenButton_Click(object sender, EventArgs e)
    {
        using (OpenFileDialog openFileDialog = new OpenFileDialog())
        {
            openFileDialog.Filter = "Model bin files (*.modelbin)|*.modelbin|All files (*.*)|*.*";
            openFileDialog.Title = "Select a modelbin file";

            if (openFileDialog.ShowDialog() == DialogResult.OK)
            {
                inputPathTextBox.Text = openFileDialog.FileName;

                // Suggest output filename based on input filename
                string directory = Path.GetDirectoryName(openFileDialog.FileName);
                string filename = Path.GetFileNameWithoutExtension(openFileDialog.FileName);
                outputPathTextBox.Text = Path.Combine(directory, filename + "_converted.modelbin");

                try
                {
                    // Try to load the bundle
                    bundle = new Bundle();
                    using (var fs = new FileStream(openFileDialog.FileName, FileMode.Open))
                    {
                        bundle.Load(fs);
                    }

                    // Update the version info display
                    versionInfoLabel.Text = $"Version: {bundle.VersionMajor}.{bundle.VersionMinor}";

                    // Determine appropriate conversion based on file version
                    DetectModelFormat();

                    statusLabel.Text = "File loaded successfully";
                    conversionGroupBox.Enabled = true;
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error loading file: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    statusLabel.Text = "Error loading file";
                    bundle = null;
                    versionInfoLabel.Text = "Version: N/A";
                    conversionGroupBox.Enabled = false;
                }
            }
        }
    }

    private void DetectModelFormat()
    {
        // Detect the format based on version and structure
        bool hasTangent3 = false;

        try
        {
            MeshBlob meshBlob = (MeshBlob)bundle.GetBlobByIndex(Bundle.TAG_BLOB_Mesh, 0);
            VertexLayoutBlob layout = (VertexLayoutBlob)bundle.GetBlobByIndex(Bundle.TAG_BLOB_VertexLayout, meshBlob.VertexLayoutIndex);

            // Check for tangent elements
            int tangentCount = 0;
            for (int i = 0; i < layout.Elements.Count; i++)
            {
                if (layout.SemanticNames[layout.Elements[i].SemanticNameIndex] == "TANGENT")
                {
                    tangentCount++;
                    if (layout.Elements[i].SemanticIndex == 2)
                    {
                        hasTangent3 = true;
                    }
                }
            }

            // Select appropriate radio button based on detection
            if (hasTangent3)
            {
                // This is likely an FH5 model
                rbOriginal.Text = "Keep Original Format (FH5)";
                rbFH5ToFH4.Checked = false;
                rbFH4ToFH5.Checked = false;
                rbOriginal.Checked = true;
            }
            else
            {
                // This is likely an FH4 model
                rbOriginal.Text = "Keep Original Format (FH4)";
                rbFH4ToFH5.Checked = true;
                rbFH5ToFH4.Checked = false;
                rbOriginal.Checked = false;
            }

            // Based on version number, suggest conversions
            if (bundle.VersionMajor > 2)
            {
                rbFMToFH5.Visible = true;
                rbFMToFH5.Checked = false;
            }
            else
            {
                rbFMToFH5.Visible = false;
            }
        }
        catch (Exception)
        {
            // If any exceptions occur during detection, fall back to default
            rbOriginal.Text = "Keep Original Format";
            rbOriginal.Checked = true;
            rbFH4ToFH5.Checked = false;
            rbFH5ToFH4.Checked = false;
            rbFMToFH5.Checked = false;
        }
    }

    private void SaveButton_Click(object sender, EventArgs e)
    {
        using (SaveFileDialog saveFileDialog = new SaveFileDialog())
        {
            saveFileDialog.Filter = "Model bin files (*.modelbin)|*.modelbin|All files (*.*)|*.*";
            saveFileDialog.Title = "Save converted modelbin file";

            if (!string.IsNullOrEmpty(outputPathTextBox.Text))
            {
                saveFileDialog.FileName = outputPathTextBox.Text;
            }

            if (saveFileDialog.ShowDialog() == DialogResult.OK)
            {
                outputPathTextBox.Text = saveFileDialog.FileName;
            }
        }
    }

    private void ConvertButton_Click(object sender, EventArgs e)
    {
        if (bundle == null)
        {
            MessageBox.Show("Please load a .modelbin file first", "Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        if (string.IsNullOrEmpty(outputPathTextBox.Text))
        {
            MessageBox.Show("Please specify an output file path", "Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        try
        {
            statusLabel.Text = "Converting...";
            Application.DoEvents();

            // Create a clone of the bundle for conversion
            Bundle outputBundle = CloneBundle(bundle);

            // Apply selected conversion
            if (rbFH4ToFH5.Checked)
            {
                MakeFH5Compatible(outputBundle);
            }
            else if (rbFH5ToFH4.Checked)
            {
                MakeFH4Compatible(outputBundle);
            }
            else if (rbFMToFH5.Checked)
            {
                MakeFMToFH5Compatible(outputBundle);
            }
            // Original format - no changes needed

            // Save the converted file
            using (var output = new FileStream(outputPathTextBox.Text, FileMode.Create))
            {
                outputBundle.Serialize(output);
            }

            statusLabel.Text = "Conversion completed successfully";
            MessageBox.Show("Conversion completed successfully!", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            statusLabel.Text = "Error during conversion";
            MessageBox.Show($"Error during conversion: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private Bundle CloneBundle(Bundle sourceBundle)
    {
        // Create a memory stream to serialize and deserialize the bundle
        using (MemoryStream ms = new MemoryStream())
        {
            sourceBundle.Serialize(ms);
            ms.Position = 0;
            Bundle newBundle = new Bundle();
            newBundle.Load(ms);
            return newBundle;
        }
    }

    private void MakeFH5Compatible(Bundle bundle)
    {
        // Use the existing method from Program class
        Program.MakeFH5Compatible(bundle);
    }

    private void MakeFH4Compatible(Bundle bundle)
    {
        try
        {
            // Get the main lod mesh
            MeshBlob meshBlob = (MeshBlob)bundle.GetBlobByIndex(Bundle.TAG_BLOB_Mesh, 0);

            // Find the vertex layout
            VertexLayoutBlob layout = (VertexLayoutBlob)bundle.GetBlobByIndex(Bundle.TAG_BLOB_VertexLayout, meshBlob.VertexLayoutIndex);

            // Find 3rd tangent component if it exists
            int tangentThirdIndex = -1;
            for (int i = 0; i < layout.Elements.Count; i++)
            {
                if (layout.SemanticNames[layout.Elements[i].SemanticNameIndex] == "TANGENT" &&
                    layout.Elements[i].SemanticIndex == 2)
                {
                    tangentThirdIndex = i;
                    break;
                }
            }

            if (tangentThirdIndex >= 0)
            {
                // Remove the 3rd tangent component
                layout.Elements.RemoveAt(tangentThirdIndex);
                layout.PackedFormats.RemoveAt(tangentThirdIndex);

                // Remove the tangent data from vertex buffer
                VertexBufferBlob buffer = (VertexBufferBlob)bundle.GetBlobByIndex(Bundle.TAG_BLOB_VertexBuffer, 1);
                int offset = tangentThirdIndex * 4; // Assuming 4 bytes per element

                for (int i = 0; i < buffer.Header.Data.Length; i++)
                {
                    var l = buffer.Header.Data[i].ToList();
                    l.RemoveRange(offset, 4); // Remove 4 bytes for the tangent
                    buffer.Header.Data[i] = l.ToArray();
                }

                // Clear the flag that FH5 requires
                layout.Flags &= 0xFFFFFF7F;

                // Update sizes
                byte totalSize = layout.GetTotalVertexSize();
                buffer.Header.BufferWidth = totalSize;
                buffer.Header.NumElements = (byte)layout.Elements.Count;
            }
        }
        catch (Exception ex)
        {
            throw new Exception($"Failed to convert to FH4 format: {ex.Message}", ex);
        }
    }

    private void MakeFMToFH5Compatible(Bundle bundle)
    {
        try
        {
            // First apply FH5 compatibility changes
            MakeFH5Compatible(bundle);

            // Additional FM-specific conversions could be added here
            // For example, adjust specific flags or formats that might be different in FM

            // For now, just update version to indicate it's for FH5
            bundle.VersionMajor = 2;  // Typical for FH5
            bundle.VersionMinor = 0;
        }
        catch (Exception ex)
        {
            throw new Exception($"Failed to convert from FM to FH5 format: {ex.Message}", ex);
        }
    }
}
