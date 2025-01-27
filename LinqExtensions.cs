using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace FolderPickerLib {
    public static class LinqExtensions {
        public static ObservableCollection<T> ToObservableCollection<T>(this IEnumerable<T> source) {
            var result = new ObservableCollection<T>();

            foreach (var ci in source) {
                result.Add(ci);
            }

            return result;
        }
    }
}