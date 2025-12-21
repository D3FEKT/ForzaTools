using ForzaTools.ForzaAnalyzer.ViewModels;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

namespace ForzaTools.ForzaAnalyzer.Views
{
    public sealed partial class FileDetailsPage : Page
    {
        public FileViewModel ViewModel { get; private set; }

        public FileDetailsPage()
        {
            this.InitializeComponent();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            ViewModel = e.Parameter as FileViewModel;
        }

        private void TreeView_ItemInvoked(TreeView sender, TreeViewItemInvokedEventArgs args)
        {
            if (args.InvokedItem is ObjectNode node)
            {
                ViewModel.SelectedNode = node;
            }
        }
    }
}