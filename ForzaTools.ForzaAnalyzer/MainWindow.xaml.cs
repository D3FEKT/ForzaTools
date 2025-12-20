using ForzaTools.ForzaAnalyzer.Views; // Ensure this using is here
using Microsoft.UI.Xaml;

namespace ForzaTools.ForzaAnalyzer
{
    public sealed partial class MainWindow : Window
    {
        public MainWindow()
        {
            this.InitializeComponent();
            SystemBackdrop = new Microsoft.UI.Xaml.Media.MicaBackdrop();

            // Navigate to the ShellPage
            RootFrame.Navigate(typeof(ShellPage));
        }
    }
}