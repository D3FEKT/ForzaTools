using ForzaTools.ForzaAnalyzer.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

namespace ForzaTools.ForzaAnalyzer.Views
{
    public sealed partial class ShellPage : Page
    {
        // ERROR FIX: This property must be 'public' for x:Bind to see it
        public MainViewModel ViewModel { get; } = new MainViewModel();

        public ShellPage()
        {
            this.InitializeComponent();

            // Setup the ViewModel with the Window Handle
            var window = App.MainWindow;
            if (window != null)
            {
                var windowHandle = WinRT.Interop.WindowNative.GetWindowHandle(window);
                ViewModel.Initialize(windowHandle);
            }

            // Select "Home" by default
            NavView.SelectedItem = NavView.MenuItems[0];
        }

        // ERROR FIX: This method must exist for the Visibility binding to work
        public Visibility BooleanToVisibility(bool value)
        {
            return value ? Visibility.Visible : Visibility.Collapsed;
        }

        private void NavView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
        {
            if (args.IsSettingsSelected) return;

            if (args.SelectedItem is NavigationViewItem item && item.Tag?.ToString() == "Home")
            {
                ContentFrame.Navigate(typeof(HomePage), null);
                if (ContentFrame.Content is HomePage home)
                {
                    home.ViewModel = this.ViewModel;
                }
            }
            else if (args.SelectedItem is FileViewModel fileVm)
            {
                ContentFrame.Navigate(typeof(FileDetailsPage), fileVm);
                ViewModel.SelectedFile = fileVm;
            }
        }
    }
}