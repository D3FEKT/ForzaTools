using ForzaTools.ForzaAnalyzer.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using System;
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

            ViewModel.FileGroups.CollectionChanged += FileGroups_CollectionChanged;

            // NAVIGATION HANDLER
            ViewModel.NavigationRequested += (pageType) =>
            {
                ContentFrame.Navigate(pageType);

                // CRITICAL FIX: Deselect menu items so "Home" can be clicked again
                NavView.SelectedItem = null;
                NavView.IsBackEnabled = ContentFrame.CanGoBack;
            };

            this.Loaded += ShellPage_Loaded;

            // Update Back Button state whenever we navigate
            ContentFrame.Navigated += (s, e) => NavView.IsBackEnabled = ContentFrame.CanGoBack;

            // Default to Home
            NavView.SelectedItem = NavView.MenuItems[0];
        }

        // --- BACK BUTTON LOGIC ---
        private void NavView_BackRequested(NavigationView sender, NavigationViewBackRequestedEventArgs args)
        {
            if (ContentFrame.CanGoBack)
            {
                ContentFrame.GoBack();
            }
        }

        private void FileGroups_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.NewItems != null)
            {
                foreach (FileGroupViewModel group in e.NewItems)
                {
                    var groupItem = new NavigationViewItem
                    {
                        Content = group.GroupName,
                        Icon = new SymbolIcon(Symbol.Folder),
                        SelectsOnInvoked = false
                    };

                    foreach (var file in group.Files)
                    {
                        var fileItem = new NavigationViewItem
                        {
                            Content = file.FileName,
                            Icon = new SymbolIcon(Symbol.Document),
                            Tag = file
                        };
                        groupItem.MenuItems.Add(fileItem);
                    }
                    NavView.MenuItems.Add(groupItem);
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

        private void NavView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
        {
            if (args.IsSettingsSelected) return;

            if (args.SelectedItem is NavigationViewItem item)
            {
                string tag = item.Tag?.ToString();

                if (tag == "Home")
                {
                    ContentFrame.Navigate(typeof(HomePage));
                    if (ContentFrame.Content is HomePage home) home.ViewModel = this.ViewModel;
                }
                else if (tag == "Materials")
                {
                    ContentFrame.Navigate(typeof(MaterialsPage));
                }
                else if (tag == "ModelView")
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