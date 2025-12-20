namespace ForzaTools.ForzaAnalyzer
{
    public partial class App : Microsoft.UI.Xaml.Application
    {
        public static Microsoft.UI.Xaml.Window MainWindow { get; private set; }

        public App()
        {
            this.InitializeComponent();

            // Add this to catch startup errors
            this.UnhandledException += (sender, e) =>
            {
                // Place a breakpoint here or look at the debug output window
                System.Diagnostics.Debug.WriteLine($"CRASH: {e.Exception.Message}");
                e.Handled = true; // Try to keep app alive to see the error
            };
        }
    }
}