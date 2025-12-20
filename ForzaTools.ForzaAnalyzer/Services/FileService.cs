using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Windows.Storage.Pickers;
using ForzaTools.Bundles;   // For the 'Bundle' class
using ForzaTools.CarScene;  // For the 'CarbinFile' class
using ForzaTools.ModelBinEditor;

namespace ForzaTools.ForzaAnalyzer.Services
{
    public class FileService
    {
        private readonly nint _windowHandle;

        public FileService(nint windowHandle)
        {
            _windowHandle = windowHandle;
        }

        public async Task<IReadOnlyList<string>> PickFilesAsync()
        {
            var picker = new FileOpenPicker();
            WinRT.Interop.InitializeWithWindow.Initialize(picker, _windowHandle);

            picker.ViewMode = PickerViewMode.List;
            picker.SuggestedStartLocation = PickerLocationId.ComputerFolder;
            picker.FileTypeFilter.Add(".modelbin");
            picker.FileTypeFilter.Add(".carbin");
            picker.FileTypeFilter.Add(".zip");

            var files = await picker.PickMultipleFilesAsync();
            return files.Select(f => f.Path).ToList();
        }

        public async Task<List<(string FileName, object ParsedData)>> ProcessFileAsync(string filePath)
        {
            return await Task.Run(() =>
            {
                var results = new List<(string, object)>();
                var extension = Path.GetExtension(filePath).ToLower();

                if (extension == ".zip")
                {
                    string tempPath = Path.Combine(Path.GetTempPath(), "ForzaAnalyzer", Guid.NewGuid().ToString());
                    Directory.CreateDirectory(tempPath);

                    try
                    {
                        using (var zip = new CustomZipFile(filePath))
                        {
                            zip.ExtractToDirectory(tempPath);
                        }

                        var extractedFiles = Directory.GetFiles(tempPath, "*.*", SearchOption.AllDirectories)
                            .Where(f => f.EndsWith(".modelbin", StringComparison.OrdinalIgnoreCase) ||
                                        f.EndsWith(".carbin", StringComparison.OrdinalIgnoreCase));

                        foreach (var file in extractedFiles)
                        {
                            var result = ParseSingleFile(file);
                            if (result != null)
                            {
                                results.Add((Path.GetFileName(file), result));
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        // Log error or ignore specific zip failures
                        System.Diagnostics.Debug.WriteLine($"Zip error: {ex.Message}");
                    }
                }
                else
                {
                    var result = ParseSingleFile(filePath);
                    if (result != null)
                    {
                        results.Add((Path.GetFileName(filePath), result));
                    }
                }

                return results;
            });
        }

        private object ParseSingleFile(string path)
        {
            try
            {
                using var stream = File.OpenRead(path);

                if (path.EndsWith(".modelbin", StringComparison.OrdinalIgnoreCase))
                {
                    var bundle = new Bundle();
                    bundle.Load(stream);
                    return bundle;
                }
                else if (path.EndsWith(".carbin", StringComparison.OrdinalIgnoreCase))
                {
                    // CORRECTED: Use CarbinFile class from ForzaTools.CarScene namespace
                    var carbin = new CarbinFile();
                    carbin.Load(stream);

                    // Return the Scene object as it contains the actual data
                    return carbin.Scene;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to parse {path}: {ex.Message}");
            }
            return null;
        }
    }
}