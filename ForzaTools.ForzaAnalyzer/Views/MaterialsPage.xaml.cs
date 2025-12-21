using ForzaTools.ForzaAnalyzer.ViewModels;
using Microsoft.UI.Xaml.Controls;

namespace ForzaTools.ForzaAnalyzer.Views
{
    public sealed partial class MaterialsPage : Page
    {
        public MaterialsViewModel ViewModel { get; } = new MaterialsViewModel();

        public MaterialsPage()
        {
            this.InitializeComponent();
        }
    }
}