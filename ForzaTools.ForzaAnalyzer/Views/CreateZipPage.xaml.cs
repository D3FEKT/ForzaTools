using ForzaTools.ForzaAnalyzer.ViewModels;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

namespace ForzaTools.ForzaAnalyzer.Views
{
    public sealed partial class CreateZipPage : Page
    {
        public CreateZipViewModel ViewModel { get; } = new CreateZipViewModel();

        public CreateZipPage()
        {
            this.InitializeComponent();
        }
    }
}