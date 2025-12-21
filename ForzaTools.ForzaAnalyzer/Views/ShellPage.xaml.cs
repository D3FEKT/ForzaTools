using ForzaTools.ForzaAnalyzer.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using System.Collections.Specialized;
using System.Linq;

namespace ForzaTools.ForzaAnalyzer.Views
{
    public sealed partial class ShellPage : Page
    {
        public MainViewModel ViewModel { get; } = new MainViewModel();

        public ShellPage()
        {
            this.InitializeComponent();
            ViewModel.Files.CollectionChanged += Files_CollectionChanged;
            this.Loaded += ShellPage_Loaded;
            NavView.SelectedItem = NavView.MenuItems[0];
        }

        private void Files_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.NewItems != null)
            {
                foreach (FileViewModel newItem in e.NewItems)
                {
                    var navItem = new NavigationViewItem
                    {
                        Content = newItem.FileName,
                        Icon = new SymbolIcon(Symbol.Document),
                        Tag = newItem
                    };
                    NavView.MenuItems.Add(navItem);
                }
            }
            if (e.Action == NotifyCollectionChangedAction.Reset)
            {
                while (NavView.MenuItems.Count > 3)
                {
                    NavView.MenuItems.RemoveAt(3);
                }
            }
        }

        private void ShellPage_Loaded(object sender, RoutedEventArgs e)
        {
            if (ViewModel.IsInitialized) return;
            var window = App.MainWindow;
            if (window != null)
            {
                var windowHandle = WinRT.Interop.WindowNative.GetWindowHandle(window);
                ViewModel.Initialize(windowHandle);
            }
        }

        public Visibility BooleanToVisibility(bool value) => value ? Visibility.Visible : Visibility.Collapsed;

        private void NavView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
        {
            if (args.IsSettingsSelected) return;

            if (args.SelectedItem is NavigationViewItem item)
            {
                if (item.Tag?.ToString() == "Home")
                {
                    ContentFrame.Navigate(typeof(HomePage), null);
                    if (ContentFrame.Content is HomePage home) home.ViewModel = this.ViewModel;
                }
                else if (item.Tag?.ToString() == "ModelView")
                {
                    // FIX: Pass the entire MainViewModel so the 3D page can see the file list
                    ContentFrame.Navigate(typeof(ModelViewerPage), this.ViewModel);
                }
                else if (item.Tag is FileViewModel fileVm)
                {
                    ContentFrame.Navigate(typeof(FileDetailsPage), fileVm);
                    ViewModel.SelectedFile = fileVm;
                }
            }
        }
    }
}