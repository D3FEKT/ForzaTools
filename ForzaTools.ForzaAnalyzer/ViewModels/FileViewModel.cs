using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.VisualBasic.FileIO;

namespace ForzaTools.ForzaAnalyzer.ViewModels
{
    public partial class FileViewModel : ObservableObject
    {
        [ObservableProperty]
        private string _fileName;

        [ObservableProperty]
        private string _fileType;

        public object ParsedObject { get; }

        public FileViewModel(string fileName, object parsedObject)
        {
            FileName = fileName;
            ParsedObject = parsedObject;
            FileType = parsedObject.GetType().Name; // e.g., "Bundle" or "Scene"
        }
    }
}