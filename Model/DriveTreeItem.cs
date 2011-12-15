using System.IO;
using FolderPickerLib.Model;

namespace FolderPickerLib {
    public class DriveTreeItem : TreeItem
    {
        public DriveType DriveType { get; set; }

        public DriveTreeItem(string name, DriveType driveType, TreeItem parent)
            : base(name, parent)
        {
            DriveType = driveType;
        }
    }
}