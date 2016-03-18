using OneDriveClient.Model;

namespace OneDriveClient
{
    public class ItemViewModel
    {
        protected Value _model;
        public ItemViewModel(Value model)
        {
            _model = model;
        }

        public string Name { get { return _model.name; } }
    }

    public class FileViewModel : ItemViewModel
    {
        public FileViewModel(Value model) : base(model)
        {
        }
    }

    public class FolderViewModel : ItemViewModel
    {
        public FolderViewModel(Value model) : base(model)
        {
        }

        public int ChildCount { get { return _model.folder != null ? _model.folder.childCount : 0; } }
    }
}
