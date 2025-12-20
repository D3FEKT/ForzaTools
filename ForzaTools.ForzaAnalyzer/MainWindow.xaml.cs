using ForzaTools.ForzaAnalyzer.Views;
using Microsoft.UI.Xaml;
using Microsoft.UI.Composition.SystemBackdrops; // Required for the check

namespace ForzaTools.ForzaAnalyzer
{
    public sealed partial class MainWindow : Window
    {
        public MainWindow()
        {
            this.InitializeComponent();

            // FIX: Check if Mica is supported before applying it.
            // If this check is missing, the window will be black/empty on Windows 10.
            if (MicaController.IsSupported())
            {
           //     SystemBackdrop = new Microsoft.UI.Xaml.Media.MicaBackdrop();
            }

            RootFrame.Navigate(typeof(ShellPage));
        }
    }
}