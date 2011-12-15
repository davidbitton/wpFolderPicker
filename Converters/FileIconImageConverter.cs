using System;
using System.Drawing;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using FolderPickerLib.Helpers;
using FolderPickerLib.Model;

namespace FolderPickerLib.Converters {
    public class FileIconImageConverter : IValueConverter {

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
            var treeItem = value as TreeItem;
            if (treeItem == null) return null;

            Icon icon;

            if (treeItem is DriveTreeItem) {
                icon = IconHelper.GetFileIcon(treeItem.GetFullPath(), IconHelper.IconSize.Small, false);
            } else {
                icon = IconHelper.GetFolderIcon(treeItem.GetFullPath(), IconHelper.IconSize.Small, IconHelper.FolderType.Closed);
            }

            var bitmapSource = Imaging.CreateBitmapSourceFromHIcon(
                icon.Handle,
                new Int32Rect(0, 0, icon.Width, icon.Height),
                BitmapSizeOptions.FromEmptyOptions()
                );

            return BitmapFrame.Create(bitmapSource);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
            throw new NotImplementedException();
        }
    }
}
