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

            // FIX: Listen to FileGroups instead of Files
            ViewModel.FileGroups.CollectionChanged += FileGroups_CollectionChanged;

            this.Loaded += ShellPage_Loaded;
            NavView.SelectedItem = NavView.MenuItems[0];
        }

        private void FileGroups_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.NewItems != null)
            {
                foreach (FileGroupViewModel group in e.NewItems)
                {
                    // Create a parent Item for the Group (Folder)
                    var groupItem = new NavigationViewItem
                    {
                        Content = group.GroupName,
                        Icon = new SymbolIcon(Symbol.Folder),
                        SelectsOnInvoked = false // Clicking folder expands it, doesn't navigate
                    };

                    // Add individual files as children
                    foreach (var file in group.Files)
                    {
                        var fileItem = new NavigationViewItem
                        {
                            Content = file.FileName,
                            Icon = new SymbolIcon(Symbol.Document),
                            Tag = file // Tag is used for navigation
                        };
                        groupItem.MenuItems.Add(fileItem);
                    }

                    NavView.MenuItems.Add(groupItem);
                }
            }

            // Handle clearing the list
            if (e.Action == NotifyCollectionChangedAction.Reset)
            {
                // Remove everything after the static items (Home, 3D View, Separator)
                // Assuming the first 3 items are static defined in XAML
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