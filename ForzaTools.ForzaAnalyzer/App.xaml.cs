namespace ForzaTools.ForzaAnalyzer
{
    public partial class App : Microsoft.UI.Xaml.Application
    {
        public static Microsoft.UI.Xaml.Window MainWindow { get; private set; }

        public App()
        {
            this.InitializeComponent();
        }

        protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
        {
            MainWindow = new MainWindow();
            MainWindow.Activate();
        }
    }
}