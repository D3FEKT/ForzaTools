using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Windows.Storage.Pickers;
using ForzaTools.Bundles;
using ForzaTools.CarScene;

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

        public async IAsyncEnumerable<(string FileName, object ParsedData)> ProcessFileAsync(string filePath, [EnumeratorCancellation] CancellationToken token = default)
        {
            var extension = Path.GetExtension(filePath).ToLower();

            if (extension == ".zip")
            {
                string tempPath = Path.Combine(Path.GetTempPath(), "ForzaAnalyzer", Guid.NewGuid().ToString());
                Directory.CreateDirectory(tempPath);

                await Task.Run(() =>
                {
                    try
                    {
                        using (var zip = new CustomZipFile(filePath))
                        {
                            zip.ExtractToDirectory(tempPath);
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Zip error: {ex.Message}");
                    }
                });

                var extractedFiles = Directory.GetFiles(tempPath, "*.*", SearchOption.AllDirectories)
                    .Where(f => f.EndsWith(".modelbin", StringComparison.OrdinalIgnoreCase) ||
                                f.EndsWith(".carbin", StringComparison.OrdinalIgnoreCase));

                foreach (var file in extractedFiles)
                {
                    token.ThrowIfCancellationRequested();
                    var result = await Task.Run(() => ParseSingleFile(file));
                    if (result != null)
                    {
                        yield return (Path.GetFileName(file), result);
                    }
                    // Optional: Force GC after each heavy file to prevent buildup
                    // GC.Collect(); 
                }
            }
            else
            {
                var result = await Task.Run(() => ParseSingleFile(filePath));
                if (result != null)
                {
                    yield return (Path.GetFileName(filePath), result);
                }
            }
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
                    var carbin = new CarbinFile();
                    carbin.Load(stream);
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