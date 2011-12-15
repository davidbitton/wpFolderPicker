using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;

namespace FolderPickerLib.Model {
    public class TreeItem : NotifiableObject {
        private string _name;
        private TreeItem _parent;
        private ObservableCollection<TreeItem> _children;

        #region Properties

        public bool IsFullyLoaded { get; set; }

        public string Name {
            get {
                return _name;
            }
            set {
                if (value == _name) return;
                _name = value;
                NotifyPropertyChanged(() => Name);
            }
        }

        public TreeItem Parent {
            get {
                return _parent;
            }
            set {
                if (value == _parent) return;
                _parent = value;
                NotifyPropertyChanged(() => Parent);
            }
        }

        public ObservableCollection<TreeItem> Children {
            get {
                return _children;
            }
            set {
                if (value == _children) return;
                _children = value;
                NotifyPropertyChanged(() => Children);
            }
        }

        #endregion

        public TreeItem(string name, TreeItem parent) {
            Name = name;
            IsFullyLoaded = false;
            Parent = parent;
            Children = new ObservableCollection<TreeItem>();
        }

        public string GetFullPath() {
            var stack = new Stack<string>();

            var ti = this;

            while (ti.Parent != null) {
                stack.Push(ti.Name);
                ti = ti.Parent;
            }

            var path = stack.Pop();

            while (stack.Count > 0) {
                path = Path.Combine(path, stack.Pop());
            }

            return path;
        }
    }
}
