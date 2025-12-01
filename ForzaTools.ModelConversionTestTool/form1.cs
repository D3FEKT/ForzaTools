namespace ForzaTools.ModelConversionTestTool;

using ForzaTools.Bundles;
using ForzaTools.Bundles.Blobs;
using ForzaTools.Utils;
using ForzaTools.Shared;
using System;
using System.IO;
using System.Windows.Forms;
using System.Drawing;
using System.Linq; // Added for ToList/RemoveRange logic

public enum ModelGameVersion
{
    FH4,
    FH5,
    FM
}

public class ModelConverterForm : Form
{
    // Controls
    private Button openButton;
    private Button saveButton;
    private Button convertButton;

    private TextBox inputPathTextBox;
    private TextBox outputPathTextBox;

    private Label inputLabel;
    private Label outputLabel;
    private Label statusLabel;
    private Label versionInfoLabel;

    private GroupBox conversionGroupBox;
    private RadioButton rbFH4ToFH5;
    private RadioButton rbFH5ToFH4;
    private RadioButton rbFMToFH5;
    private RadioButton rbOriginal;

    // Logic Data
    private Bundle bundle;

    public ModelConverterForm()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        // 1. Initialize all controls
        this.inputLabel = new Label();
        this.inputPathTextBox = new TextBox();
        this.openButton = new Button();

        this.outputLabel = new Label();
        this.outputPathTextBox = new TextBox();
        this.saveButton = new Button();

        this.versionInfoLabel = new Label();
        this.statusLabel = new Label();

        this.conversionGroupBox = new GroupBox();
        this.rbOriginal = new RadioButton();
        this.rbFH4ToFH5 = new RadioButton();
        this.rbFH5ToFH4 = new RadioButton();
        this.rbFMToFH5 = new RadioButton();

        this.convertButton = new Button();

        this.conversionGroupBox.SuspendLayout();
        this.SuspendLayout();

        // 
        // Form Settings
        // 
        this.ClientSize = new Size(500, 360);
        this.Text = "Forza Model Conversion Tool";
        this.FormBorderStyle = FormBorderStyle.FixedDialog;
        this.MaximizeBox = false;
        this.StartPosition = FormStartPosition.CenterScreen;

        // 
        // Input Section (Top)
        // 
        this.inputLabel.Text = "Input Model File (.modelbin):";
        this.inputLabel.Location = new Point(20, 20);
        this.inputLabel.Size = new Size(200, 20);

        this.inputPathTextBox.Location = new Point(20, 45);
        this.inputPathTextBox.Size = new Size(360, 23);
        this.inputPathTextBox.ReadOnly = true;

        this.openButton.Text = "Browse...";
        this.openButton.Location = new Point(390, 44);
        this.openButton.Size = new Size(90, 25);
        this.openButton.Click += OpenButton_Click;

        this.versionInfoLabel.Text = "Version: N/A";
        this.versionInfoLabel.Location = new Point(390, 20);
        this.versionInfoLabel.Size = new Size(90, 20);
        this.versionInfoLabel.TextAlign = ContentAlignment.TopRight;

        // 
        // Output Section (Middle)
        // 
        this.outputLabel.Text = "Output File Path:";
        this.outputLabel.Location = new Point(20, 85);
        this.outputLabel.Size = new Size(200, 20);

        this.outputPathTextBox.Location = new Point(20, 110);
        this.outputPathTextBox.Size = new Size(360, 23);

        this.saveButton.Text = "Save As...";
        this.saveButton.Location = new Point(390, 109);
        this.saveButton.Size = new Size(90, 25);
        this.saveButton.Click += SaveButton_Click;

        // 
        // Conversion Options GroupBox
        // 
        this.conversionGroupBox.Text = "Conversion Options";
        this.conversionGroupBox.Location = new Point(20, 150);
        this.conversionGroupBox.Size = new Size(460, 130);
        this.conversionGroupBox.Enabled = false; // Disabled until file loaded

        // Radio Buttons inside GroupBox
        this.rbOriginal.Text = "Keep Original Format";
        this.rbOriginal.Location = new Point(20, 25);
        this.rbOriginal.Size = new Size(400, 24);
        this.rbOriginal.Checked = true;

        this.rbFH4ToFH5.Text = "Convert FH4 to FH5";
        this.rbFH4ToFH5.Location = new Point(20, 50);
        this.rbFH4ToFH5.Size = new Size(400, 24);

        this.rbFH5ToFH4.Text = "Convert FH5 to FH4";
        this.rbFH5ToFH4.Location = new Point(20, 75);
        this.rbFH5ToFH4.Size = new Size(400, 24);

        this.rbFMToFH5.Text = "Convert FM ( Motorsport) to FH5";
        this.rbFMToFH5.Location = new Point(20, 100);
        this.rbFMToFH5.Size = new Size(400, 24);
        this.rbFMToFH5.Visible = false; // Hidden by default as per logic

        this.conversionGroupBox.Controls.Add(this.rbOriginal);
        this.conversionGroupBox.Controls.Add(this.rbFH4ToFH5);
        this.conversionGroupBox.Controls.Add(this.rbFH5ToFH4);
        this.conversionGroupBox.Controls.Add(this.rbFMToFH5);

        // 
        // Convert Button & Status (Bottom)
        // 
        this.convertButton.Text = "Convert Model";
        this.convertButton.Location = new Point(340, 300);
        this.convertButton.Size = new Size(140, 40);
        this.convertButton.Font = new Font(this.Font, FontStyle.Bold);
        this.convertButton.Click += ConvertButton_Click;

        this.statusLabel.Text = "Ready";
        this.statusLabel.Location = new Point(20, 310);
        this.statusLabel.Size = new Size(300, 23);
        this.statusLabel.ForeColor = Color.Gray;

        // 
        // Add Controls to Form
        // 
        this.Controls.Add(this.inputLabel);
        this.Controls.Add(this.inputPathTextBox);
        this.Controls.Add(this.openButton);
        this.Controls.Add(this.versionInfoLabel);

        this.Controls.Add(this.outputLabel);
        this.Controls.Add(this.outputPathTextBox);
        this.Controls.Add(this.saveButton);

        this.Controls.Add(this.conversionGroupBox);
        this.Controls.Add(this.convertButton);
        this.Controls.Add(this.statusLabel);

        this.conversionGroupBox.ResumeLayout(false);
        this.ResumeLayout(false);
        this.PerformLayout();
    }

    // ---------------------------------------------------------
    // EXISTING LOGIC BELOW (Unchanged)
    // ---------------------------------------------------------

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
                    statusLabel.ForeColor = Color.Green;
                    conversionGroupBox.Enabled = true;
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error loading file: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    statusLabel.Text = "Error loading file";
                    statusLabel.ForeColor = Color.Red;
                    bundle = null;
                    versionInfoLabel.Text = "Version: N/A";
                    conversionGroupBox.Enabled = false;
                }
            }
        }
    }

    private void DetectModelFormat()
    {
        bool hasTangent3 = false;

        try
        {
            MeshBlob meshBlob = (MeshBlob)bundle.GetBlobByIndex(Bundle.TAG_BLOB_Mesh, 0);
            VertexLayoutBlob layout = (VertexLayoutBlob)bundle.GetBlobByIndex(Bundle.TAG_BLOB_VertexLayout, meshBlob.VertexLayoutIndex);

            // Check for tangent elements
            for (int i = 0; i < layout.Elements.Count; i++)
            {
                if (layout.SemanticNames[layout.Elements[i].SemanticNameIndex] == "TANGENT")
                {
                    if (layout.Elements[i].SemanticIndex == 2)
                    {
                        hasTangent3 = true;
                    }
                }
            }

            // Select appropriate radio button based on detection
            if (hasTangent3)
            {
                rbOriginal.Text = "Keep Original Format (FH5 detected)";
                rbFH5ToFH4.Checked = false;
                rbFH4ToFH5.Checked = false;
                rbOriginal.Checked = true;
            }
            else
            {
                rbOriginal.Text = "Keep Original Format (FH4 detected)";
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
            statusLabel.ForeColor = Color.Blue;
            Application.DoEvents();

            Bundle outputBundle = CloneBundle(bundle);

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

            using (var output = new FileStream(outputPathTextBox.Text, FileMode.Create))
            {
                outputBundle.Serialize(output);
            }

            statusLabel.Text = "Conversion completed successfully";
            statusLabel.ForeColor = Color.Green;
            MessageBox.Show("Conversion completed successfully!", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            statusLabel.Text = "Error during conversion";
            statusLabel.ForeColor = Color.Red;
            MessageBox.Show($"Error during conversion: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private Bundle CloneBundle(Bundle sourceBundle)
    {
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
        // Assuming Program class exists as per your original code
        Program.MakeFH5Compatible(bundle);
    }

    private void MakeFH4Compatible(Bundle bundle)
    {
        try
        {
            MeshBlob meshBlob = (MeshBlob)bundle.GetBlobByIndex(Bundle.TAG_BLOB_Mesh, 0);
            VertexLayoutBlob layout = (VertexLayoutBlob)bundle.GetBlobByIndex(Bundle.TAG_BLOB_VertexLayout, meshBlob.VertexLayoutIndex);

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
                layout.Elements.RemoveAt(tangentThirdIndex);
                layout.PackedFormats.RemoveAt(tangentThirdIndex);

                VertexBufferBlob buffer = (VertexBufferBlob)bundle.GetBlobByIndex(Bundle.TAG_BLOB_VertexBuffer, 1);
                int offset = tangentThirdIndex * 4;

                for (int i = 0; i < buffer.Header.Data.Length; i++)
                {
                    var l = buffer.Header.Data[i].ToList();
                    l.RemoveRange(offset, 4);
                    buffer.Header.Data[i] = l.ToArray();
                }

                layout.Flags &= 0xFFFFFF7F;
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
            MakeFH5Compatible(bundle);
            bundle.VersionMajor = 2;
            bundle.VersionMinor = 0;
        }
        catch (Exception ex)
        {
            throw new Exception($"Failed to convert from FM to FH5 format: {ex.Message}", ex);
        }
    }
}