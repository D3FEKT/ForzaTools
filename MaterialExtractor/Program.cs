using System;
using System.IO;
using System.Windows.Forms;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using System.Drawing;
using ForzaTools.Bundles;
using ForzaTools.Bundles.Blobs;
using ForzaTools.Bundles.Metadata;

namespace MaterialExtractor
{
    internal class Program
    {
        private static readonly byte[] nameTag = new byte[] { 0x65, 0x6D, 0x61, 0x4E }; // "Name" in reversed bytes
        private static readonly byte[] idTag = new byte[] { 0x20, 0x20, 0x64, 0x49 }; // "Id  " in reversed bytes
        private static readonly string[] supportedExtensions = new[] { ".modelbin", ".materialbin" };

        [STAThread]
        static void Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MaterialExtractorForm());
        }

        private static string[] SelectFiles()
        {
            using (OpenFileDialog openFileDialog = new OpenFileDialog())
            {
                openFileDialog.Filter = "Model/Material Files (*.modelbin;*.materialbin)|*.modelbin;*.materialbin|All Files (*.*)|*.*";
                openFileDialog.Multiselect = true;
                openFileDialog.Title = "Select files to extract material data";

                if (openFileDialog.ShowDialog() == DialogResult.OK)
                {
                    return openFileDialog.FileNames;
                }
            }
            return null;
        }

        private static string[] SelectFolder()
        {
            using (FolderBrowserDialog folderDialog = new FolderBrowserDialog())
            {
                folderDialog.Description = "Select folder containing model/material files";
                folderDialog.UseDescriptionForTitle = true;
                folderDialog.ShowNewFolderButton = false;

                if (folderDialog.ShowDialog() == DialogResult.OK)
                {
                    List<string> files = new List<string>();
                    try
                    {
                        foreach (string extension in supportedExtensions)
                        {
                            files.AddRange(Directory.GetFiles(folderDialog.SelectedPath, $"*{extension}", SearchOption.AllDirectories));
                        }
                        return files.Count > 0 ? files.ToArray() : null;
                    }
                    catch (Exception)
                    {
                        return null;
                    }
                }
            }
            return null;
        }

        private static string CreateFormattedMetadataHex(string materialName)
        {
            byte[] nameBytes = Encoding.UTF8.GetBytes(materialName);
            byte stringLengthByte = (byte)(nameBytes.Length);
            byte sizeByteWithOffset = (byte)(nameBytes.Length + 8);
            byte firstByte, secondByte;
            CalculateSpecialSizeFormat(stringLengthByte, out firstByte, out secondByte);

            using (MemoryStream ms = new MemoryStream())
            {
                ms.Write(nameTag, 0, nameTag.Length);
                ms.WriteByte(firstByte);
                ms.WriteByte(secondByte);
                ms.WriteByte(0x10);
                ms.WriteByte(0x00);

                ms.Write(idTag, 0, idTag.Length);
                ms.WriteByte(0x40);
                ms.WriteByte(0x00);

                ms.WriteByte(sizeByteWithOffset);
                ms.WriteByte(0x00);

                ms.Write(nameBytes, 0, nameBytes.Length);

                ms.WriteByte(0x00);
                ms.WriteByte(0x00);
                ms.WriteByte(0x00);
                ms.WriteByte(0x00);

                return ByteArrayToHexString(ms.ToArray());
            }
        }

        private static void CalculateSpecialSizeFormat(byte size, out byte firstByte, out byte secondByte)
        {
            byte highNibble = (byte)((size & 0xF0) >> 4);
            byte lowNibble = (byte)(size & 0x0F);
            firstByte = (byte)(lowNibble << 4);
            secondByte = highNibble;
        }

        private static string GetMaterialName(MaterialBlob materialBlob)
        {
            foreach (var metadata in materialBlob.Metadatas)
            {
                if (metadata.Tag == BundleMetadata.TAG_METADATA_Name)
                {
                    byte[] nameBytes = metadata.GetContents();
                    string name = Encoding.UTF8.GetString(nameBytes).TrimEnd('\0');
                    return name;
                }
            }

            if (materialBlob.Bundle != null)
            {
                foreach (var nestedBlob in materialBlob.Bundle.Blobs)
                {
                    foreach (var metadata in nestedBlob.Metadatas)
                    {
                        if (metadata.Tag == BundleMetadata.TAG_METADATA_Name)
                        {
                            byte[] nameBytes = metadata.GetContents();
                            string name = Encoding.UTF8.GetString(nameBytes).TrimEnd('\0');
                            return name;
                        }
                    }
                }
            }

            return string.Empty;
        }

        private static string ByteArrayToHexString(byte[] bytes)
        {
            StringBuilder sb = new StringBuilder();
            foreach (byte b in bytes)
            {
                sb.Append(b.ToString("X2") + " ");
            }
            return sb.ToString().Trim();
        }

        public static Dictionary<string, MaterialData> ExtractMaterialData(string[] files, IProgress<(int current, int total, string message)> progress)
        {
            Dictionary<string, MaterialData> materialDataDict = new Dictionary<string, MaterialData>();
            Dictionary<string, List<string>> materialFilesDict = new Dictionary<string, List<string>>();
            int processedFileCount = 0;
            int totalFileCount = files.Length;
            int totalMaterialsExtracted = 0;

            foreach (string file in files)
            {
                try
                {
                    processedFileCount++;
                    string fileName = Path.GetFileName(file);
                    string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(file);

                    progress.Report((processedFileCount, totalFileCount, $"Processing {fileName}"));

                    using (FileStream fs = new FileStream(file, FileMode.Open, FileAccess.Read))
                    {
                        Bundle bundle = new Bundle();
                        bundle.Load(fs);

                        // Only process MaterialBlobs (TAG_BLOB_Material)
                        foreach (var blob in bundle.Blobs)
                        {
                            if (blob.Tag == Bundle.TAG_BLOB_Material)
                            {
                                MaterialBlob materialBlob = blob as MaterialBlob;
                                if (materialBlob != null)
                                {
                                    string materialName = GetMaterialName(materialBlob);
                                    if (string.IsNullOrEmpty(materialName))
                                    {
                                        materialName = $"unnamed_material_{Guid.NewGuid().ToString().Substring(0, 8)}";
                                    }

                                    string metadataHex = CreateFormattedMetadataHex(materialName);
                                    byte[] blobData = materialBlob.GetContents();

                                    MaterialData materialData = new MaterialData
                                    {
                                        Metadata = metadataHex,
                                        Data = ByteArrayToHexString(blobData)
                                    };

                                    if (!materialFilesDict.ContainsKey(materialName))
                                    {
                                        materialFilesDict[materialName] = new List<string>();
                                    }
                                    materialFilesDict[materialName].Add(fileNameWithoutExtension);

                                    string key;
                                    if (materialFilesDict[materialName].Count > 1)
                                    {
                                        key = $"{materialName}_{fileNameWithoutExtension}";
                                    }
                                    else
                                    {
                                        key = materialName;
                                    }

                                    materialDataDict[key] = materialData;
                                    totalMaterialsExtracted++;
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    progress.Report((processedFileCount, totalFileCount, $"Error processing {Path.GetFileName(file)}: {ex.Message}"));
                }
            }

            progress.Report((totalFileCount, totalFileCount, $"Extraction complete! Found {totalMaterialsExtracted} materials."));
            return materialDataDict;
        }

        public static void SaveMaterialData(Dictionary<string, MaterialData> materialDataDict, string outputFile)
        {
            using (StreamWriter writer = new StreamWriter(outputFile))
            {
                writer.WriteLine("{");

                int count = 0;
                foreach (var kvp in materialDataDict)
                {
                    count++;
                    writer.WriteLine($"    \"{kvp.Key}\": {{");
                    writer.WriteLine($"        \"metadata\": \"{kvp.Value.Metadata}\",");
                    writer.WriteLine($"        \"data\": \"{kvp.Value.Data}\"");

                    if (count < materialDataDict.Count)
                        writer.WriteLine("    },");
                    else
                        writer.WriteLine("    }");
                }

                writer.WriteLine("}");
            }
        }
    }

    public class MaterialExtractorForm : Form
    {
        private Dictionary<string, MaterialData> extractionResults;
        private string[] selectedFiles;
        private Button buttonSelectFiles;
        private Button buttonSelectFolder;
        private Button buttonSave;
        private ProgressBar progressBar;
        private TextBox logTextBox;
        private Label statusLabel;
        private Panel buttonPanel;

        public MaterialExtractorForm()
        {
            InitializeComponents();
        }

        private void InitializeComponents()
        {
            Text = "Material Extractor";
            Size = new Size(600, 400);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterScreen;
            MaximizeBox = false;
            MinimizeBox = true;
            Icon = SystemIcons.Application;

            // Button Panel
            buttonPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 60,
                Padding = new Padding(10)
            };

            // Buttons
            buttonSelectFiles = new Button
            {
                Text = "Select Files",
                Size = new Size(120, 30),
                Location = new Point(10, 15),
                UseVisualStyleBackColor = true
            };
            buttonSelectFiles.Click += ButtonSelectFiles_Click;

            buttonSelectFolder = new Button
            {
                Text = "Select Folder",
                Size = new Size(120, 30),
                Location = new Point(140, 15),
                UseVisualStyleBackColor = true
            };
            buttonSelectFolder.Click += ButtonSelectFolder_Click;

            buttonSave = new Button
            {
                Text = "Save Results",
                Size = new Size(120, 30),
                Location = new Point(450, 15),
                UseVisualStyleBackColor = true,
                Enabled = false
            };
            buttonSave.Click += ButtonSave_Click;

            // Progress bar
            progressBar = new ProgressBar
            {
                Dock = DockStyle.Top,
                Height = 20,
                Style = ProgressBarStyle.Continuous
            };

            // Status label
            statusLabel = new Label
            {
                Dock = DockStyle.Top,
                Height = 25,
                Text = "Ready to extract materials",
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(5)
            };

            // Log TextBox
            logTextBox = new TextBox
            {
                Dock = DockStyle.Fill,
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Vertical,
                Font = new Font("Consolas", 9F)
            };

            // Add controls
            buttonPanel.Controls.Add(buttonSelectFiles);
            buttonPanel.Controls.Add(buttonSelectFolder);
            buttonPanel.Controls.Add(buttonSave);
            Controls.Add(logTextBox);
            Controls.Add(statusLabel);
            Controls.Add(progressBar);
            Controls.Add(buttonPanel);
        }

        private void ButtonSelectFiles_Click(object sender, EventArgs e)
        {
            selectedFiles = Program.SelectFiles();
            if (selectedFiles != null && selectedFiles.Length > 0)
            {
                BeginExtraction();
            }
        }

        private void ButtonSelectFolder_Click(object sender, EventArgs e)
        {
            selectedFiles = Program.SelectFolder();
            if (selectedFiles != null && selectedFiles.Length > 0)
            {
                BeginExtraction();
            }
        }

        private void ButtonSave_Click(object sender, EventArgs e)
        {
            if (extractionResults == null || extractionResults.Count == 0)
            {
                MessageBox.Show("No extraction results to save.", "No Data", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            using (SaveFileDialog saveFileDialog = new SaveFileDialog())
            {
                saveFileDialog.Filter = "Text Files (*.txt)|*.txt|All Files (*.*)|*.*";
                saveFileDialog.Title = "Save extracted material data";
                saveFileDialog.FileName = "ExtractedMaterialData.txt";

                if (saveFileDialog.ShowDialog() == DialogResult.OK)
                {
                    try
                    {
                        Program.SaveMaterialData(extractionResults, saveFileDialog.FileName);
                        LogMessage($"Data saved successfully to {saveFileDialog.FileName}");
                        MessageBox.Show($"Extraction data saved successfully to:\n{saveFileDialog.FileName}",
                            "Save Complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    catch (Exception ex)
                    {
                        LogMessage($"Error saving data: {ex.Message}");
                        MessageBox.Show($"Failed to save data: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
        }

        private void BeginExtraction()
        {
            if (selectedFiles == null || selectedFiles.Length == 0)
                return;

            buttonSelectFiles.Enabled = false;
            buttonSelectFolder.Enabled = false;
            buttonSave.Enabled = false;
            progressBar.Value = 0;
            logTextBox.Clear();

            LogMessage($"Found {selectedFiles.Length} files to process...");
            statusLabel.Text = "Extracting materials...";

            var progress = new Progress<(int current, int total, string message)>(status =>
            {
                progressBar.Value = (int)(((double)status.current / status.total) * 100);
                statusLabel.Text = $"Processing {status.current} of {status.total}";
                LogMessage(status.message);
            });

            Task.Run(() =>
            {
                var results = Program.ExtractMaterialData(selectedFiles, progress);
                return results;
            })
            .ContinueWith(task =>
            {
                if (task.IsFaulted)
                {
                    this.Invoke(new Action(() =>
                    {
                        LogMessage($"Error during extraction: {task.Exception?.InnerException?.Message}");
                        statusLabel.Text = "Extraction failed";
                        buttonSelectFiles.Enabled = true;
                        buttonSelectFolder.Enabled = true;
                    }));
                }
                else
                {
                    extractionResults = task.Result;
                    this.Invoke(new Action(() =>
                    {
                        statusLabel.Text = $"Extraction complete! Found {extractionResults.Count} materials.";
                        buttonSelectFiles.Enabled = true;
                        buttonSelectFolder.Enabled = true;
                        buttonSave.Enabled = true;
                    }));
                }
            }, TaskScheduler.FromCurrentSynchronizationContext());
        }

        private void LogMessage(string message)
        {
            if (logTextBox.InvokeRequired)
            {
                logTextBox.Invoke(new Action(() => LogMessage(message)));
            }
            else
            {
                logTextBox.AppendText(message + Environment.NewLine);
                logTextBox.ScrollToCaret();
            }
        }
    }

    public class MaterialData
    {
        public string Metadata { get; set; }
        public string Data { get; set; }
    }
}
