using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Threading.Tasks;
using Windows.Storage.Pickers;
using ForzaTools.ForzaAnalyzer.Services;

namespace ForzaTools.ForzaAnalyzer.ViewModels
{
    public partial class CreateModelBinViewModel : ObservableObject
    {
        private ModelBuilderService _builderService = new ModelBuilderService();

        [ObservableProperty]
        private string _statusMessage = "Ready to generate.";

        [ObservableProperty]
        private string _fileName = "TestCube.modelbin";

        [RelayCommand]
        public async Task CreateCubeAsync()
        {
            var picker = new FileSavePicker();

            // WinUI 3 Window Handle logic
            var window = App.MainWindow;
            var hWnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
            WinRT.Interop.InitializeWithWindow.Initialize(picker, hWnd);

            picker.SuggestedStartLocation = PickerLocationId.Desktop;
            picker.SuggestedFileName = FileName;
            picker.FileTypeChoices.Add("Forza ModelBin", new[] { ".modelbin" });

            var file = await picker.PickSaveFileAsync();
            if (file == null)
            {
                StatusMessage = "Operation cancelled.";
                return;
            }

            try
            {
                StatusMessage = "Building Cube...";

                await Task.Run(() =>
                {
                    _builderService.BuildTestCube(file.Path);
                });

                StatusMessage = $"Success! Saved to {file.Name}";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error: {ex.Message}";
            }
        }
    }
}