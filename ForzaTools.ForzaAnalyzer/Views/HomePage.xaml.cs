using System;
using System.Linq;
using Windows.ApplicationModel.DataTransfer;
using ForzaTools.ForzaAnalyzer.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace ForzaTools.ForzaAnalyzer.Views
{
    public sealed partial class HomePage : Page
    {
        public MainViewModel ViewModel { get; set; }

        public HomePage()
        {
            this.InitializeComponent();
        }

        private void Grid_DragOver(object sender, DragEventArgs e)
        {
            e.AcceptedOperation = DataPackageOperation.Copy;
        }

        private async void Grid_Drop(object sender, DragEventArgs e)
        {
            if (e.DataView.Contains(StandardDataFormats.StorageItems))
            {
                var items = await e.DataView.GetStorageItemsAsync();
                var paths = items.Select(i => i.Path).ToList();
                if (ViewModel != null)
                {
                    await ViewModel.ProcessPathsAsync(paths);
                }
            }
        }
    }
}