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

            ViewModel.NavigationRequested += (pageType) =>
            {
                ContentFrame.Navigate(pageType);
                NavView.SelectedItem = null;
                NavView.IsBackEnabled = ContentFrame.CanGoBack;
            };

            this.Loaded += ShellPage_Loaded;
            ContentFrame.Navigated += (s, e) => NavView.IsBackEnabled = ContentFrame.CanGoBack;
            NavView.SelectedItem = NavView.MenuItems[0];
        }

        private void NavView_BackRequested(NavigationView sender, NavigationViewBackRequestedEventArgs args)
        {
            if (ContentFrame.CanGoBack) ContentFrame.GoBack();
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

                    // Add existing files
                    foreach (var file in group.Files)
                    {
                        AddFileToMenu(groupItem, file);
                    }

                    // FIX: Listen for future files added to this group (Streaming support)
                    group.Files.CollectionChanged += (s, args) =>
                    {
                        if (args.NewItems != null)
                        {
                            foreach (FileViewModel newFile in args.NewItems)
                            {
                                AddFileToMenu(groupItem, newFile);
                            }
                        }
                    };

                    NavView.MenuItems.Add(groupItem);
                }
            }

            if (e.Action == NotifyCollectionChangedAction.Reset)
            {
                // Clear dynamic items (Keep Home, Materials, ModelView, CreateModelBin -> 4 items)
                while (NavView.MenuItems.Count > 4)
                {
                    NavView.MenuItems.RemoveAt(4);
                }
            }
        }

        private void AddFileToMenu(NavigationViewItem groupItem, FileViewModel file)
        {
            var fileItem = new NavigationViewItem
            {
                Content = file.FileName,
                Icon = new SymbolIcon(Symbol.Document),
                Tag = file
            };
            groupItem.MenuItems.Add(fileItem);
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
                else if (tag == "Materials") ContentFrame.Navigate(typeof(MaterialsPage));
                else if (tag == "ModelView") ContentFrame.Navigate(typeof(ModelViewerPage), this.ViewModel);
                else if (tag == "CreateModelBinPage") ContentFrame.Navigate(typeof(CreateModelBinPage));

                // Specific File Handling
                else if (item.Tag is FileViewModel fileVm)
                {
                    ContentFrame.Navigate(typeof(FileDetailsPage), fileVm);
                    ViewModel.SelectedFile = fileVm;
                }
            }
        }
    }
}