using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Windows.Forms;
using ForzaTools.Bundles;

namespace ForzaTools.ModelBinEditor
{
    public class BatchArchiveProcessor
    {
        private string _outputDirectory;

        public BatchArchiveProcessor()
        {
            _outputDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "BatchOutput");
            if (!Directory.Exists(_outputDirectory))
            {
                Directory.CreateDirectory(_outputDirectory);
            }
        }

        // Updated callback signature to include message type
        public void ProcessArchives(string[] zipFilePaths, Action<string, string> statusCallback)
        {
            foreach (var zipPath in zipFilePaths)
            {
                string fileName = Path.GetFileName(zipPath);
                statusCallback?.Invoke($"--------------------------------------------------", "NORMAL");
                statusCallback?.Invoke($"Opening archive: {fileName}", "INFO");

                ProcessSingleArchive(zipPath, statusCallback);
            }
            statusCallback?.Invoke($"--------------------------------------------------", "NORMAL");
            statusCallback?.Invoke("Batch job finished.", "SUCCESS");
        }

        private void ProcessSingleArchive(string zipPath, Action<string, string> log)
        {
            string tempPath = Path.Combine(Path.GetTempPath(), "ForzaModelBinBatch", Path.GetFileNameWithoutExtension(zipPath) + "_" + Guid.NewGuid());

            try
            {
                if (Directory.Exists(tempPath)) Directory.Delete(tempPath, true);
                Directory.CreateDirectory(tempPath);

                log?.Invoke("  Extracting...", "NORMAL");
                ZipFile.ExtractToDirectory(zipPath, tempPath);

                var modelFiles = Directory.GetFiles(tempPath, "*.modelbin", SearchOption.AllDirectories);
                log?.Invoke($"  Found {modelFiles.Length} modelbin files.", "INFO");

                int convertedCount = 0;
                int errorCount = 0;

                foreach (var modelFile in modelFiles)
                {
                    string relPath = Path.GetFileName(modelFile); // Keep it short for log
                    try
                    {
                        bool result = ConvertModelBin(modelFile);
                        if (result)
                        {
                            log?.Invoke($"    [CONVERTED] {relPath}", "SUCCESS");
                            convertedCount++;
                        }
                        else
                        {
                            log?.Invoke($"    [SKIP] {relPath} (Already compatible)", "WARN");
                        }
                    }
                    catch (Exception ex)
                    {
                        log?.Invoke($"    [ERROR] {relPath}: {ex.Message}", "ERROR");
                        errorCount++;
                    }
                }

                log?.Invoke($"  Archiving results (Converted: {convertedCount}, Errors: {errorCount})...", "INFO");

                string outputZipPath = Path.Combine(_outputDirectory, Path.GetFileName(zipPath));
                if (File.Exists(outputZipPath)) File.Delete(outputZipPath);

                CreateWinRarStyleZip(tempPath, outputZipPath);
                log?.Invoke($"  Saved: {outputZipPath}", "SUCCESS");
            }
            catch (Exception ex)
            {
                log?.Invoke($"  ARCHIVE FAILED: {ex.Message}", "ERROR");
            }
            finally
            {
                if (Directory.Exists(tempPath))
                    Directory.Delete(tempPath, true);
            }
        }

        private bool ConvertModelBin(string filePath)
        {
            // Load
            var bundle = new Bundle();
            using (var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read))
            {
                bundle.Load(fs);
            }

            // Convert
            bool modified = ModelConverter.MakeFH5Compatible(bundle);

            // Save only if modified
            if (modified)
            {
                using (var fs = new FileStream(filePath, FileMode.Create, FileAccess.Write))
                {
                    bundle.Serialize(fs);
                }
            }
            return modified;
        }

        private void CreateWinRarStyleZip(string sourceDir, string destinationZip)
        {
            using (FileStream zipToOpen = new FileStream(destinationZip, FileMode.Create))
            using (ZipArchive archive = new ZipArchive(zipToOpen, ZipArchiveMode.Create))
            {
                DirectoryInfo dirInfo = new DirectoryInfo(sourceDir);

                foreach (FileInfo file in dirInfo.GetFiles("*", SearchOption.AllDirectories))
                {
                    string relPath = Path.GetRelativePath(sourceDir, file.FullName);
                    ZipArchiveEntry entry = archive.CreateEntry(relPath, CompressionLevel.Optimal);
                    entry.LastWriteTime = file.LastWriteTime;

                    using (var entryStream = entry.Open())
                    using (var fileStream = file.OpenRead())
                    {
                        fileStream.CopyTo(entryStream);
                    }
                }
            }
        }
    }
}