using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace OneDriveClient
{
    public class TempateSelector : DataTemplateSelector
    {
        public DataTemplate FileTemplate { get; set; }
        public DataTemplate FolderTemplate { get; set; }

        protected override DataTemplate SelectTemplateCore(object item, DependencyObject container)
        {
            var file = item as FileViewModel;
            if (file == null)
                return FolderTemplate;
            return FileTemplate;
        }
    }
}
