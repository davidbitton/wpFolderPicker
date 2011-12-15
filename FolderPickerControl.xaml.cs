using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using FolderPickerLib.Model;

namespace FolderPickerLib {
    /// <summary>
    /// Interaction logic for FolderPicker.xaml
    /// </summary>
    public partial class FolderPickerControl : INotifyPropertyChanged {

        public static readonly DependencyProperty InitialPathProperty =
            DependencyProperty.Register("InitialPath", typeof (string), typeof (FolderPickerControl),
                                        new FrameworkPropertyMetadata(string.Empty, OnInitialPathPropertyChanged));

        private const string EmptyItemName = "Empty";
        private const string NewFolderName = "New Folder";
        private const int MaxNewFolderSuffix = 10000;

        private TreeItem _root;
        private TreeItem _selectedItem;
        //private string _initialPath;
        private Style _itemContainerStyle;

        #region Properties

        public TreeItem Root {
            get {
                return _root;
            }
            private set {
                _root = value;
                NotifyPropertyChanged(() => Root);
            }
        }

        public TreeItem SelectedItem {
            get {
                return _selectedItem;
            }
            private set {
                _selectedItem = value;
                NotifyPropertyChanged(() => SelectedItem);
            }
        }

        public string SelectedPath { get; private set; }

        public string InitialPath {
            get {
                //return _initialPath;
                return (string) GetValue(InitialPathProperty);
            }
            set {
                //_initialPath = value;
                SetValue(InitialPathProperty, value);
                //UpdateInitialPathUi();
            }
        }

        private static void OnInitialPathPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs args) {
            var control = d as FolderPickerControl;
            if(control == null) return;
            control.UpdateInitialPathUi();
        }

        public Style ItemContainerStyle {
            get {
                return _itemContainerStyle;
            }
            set {
                _itemContainerStyle = value;
                OnPropertyChanged("ItemContainerStyle");
            }
        }

        #endregion

        public FolderPickerControl() {
            InitializeComponent();

            Init();
        }

        public void CreateNewFolder() {
            CreateNewFolderImpl(SelectedItem);
        }

        public void RefreshTree() {
            Root = null;
            Init();
        }

        #region INotifyPropertyChanged Members

        public void NotifyPropertyChanged<TProperty>(Expression<Func<TProperty>> property) {
            var lambda = (LambdaExpression)property;
            MemberExpression memberExpression;
            if (lambda.Body is UnaryExpression) {
                var unaryExpression = (UnaryExpression)lambda.Body;
                memberExpression = (MemberExpression)unaryExpression.Operand;
            } else memberExpression = (MemberExpression)lambda.Body;
            OnPropertyChanged(memberExpression.Member.Name);
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropertyChanged(string propertyName) {
            if (PropertyChanged != null) {
                PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
            }
        }

        #endregion

        #region Private methods

        private void Init() {
            _root = new TreeItem("root", null);
            var systemDrives = DriveInfo.GetDrives();

            foreach (var item in systemDrives.Select(sd => new DriveTreeItem(sd.Name, sd.DriveType, _root))) {
                item.Children.Add(new TreeItem(EmptyItemName, item));

                _root.Children.Add(item);
            }

            Root = _root; // to notify UI
        }

        private void TreeViewItemSelected(object sender, RoutedEventArgs e) {
            var tvi = e.OriginalSource as TreeViewItem;
            if (tvi == null) return;
            SelectedItem = tvi.DataContext as TreeItem;
            if (SelectedItem != null) SelectedPath = SelectedItem.GetFullPath();
        }

        private void TreeViewExpanded(object sender, RoutedEventArgs e) {
            var tvi = e.OriginalSource as TreeViewItem;
            if (tvi == null) return;
            var treeItem = tvi.DataContext as TreeItem;

            if (treeItem == null) throw new Exception();
            if (treeItem.IsFullyLoaded) return;
            treeItem.Children.Clear();

            var path = treeItem.GetFullPath();

            var dir = new DirectoryInfo(path);

            try {
                var subDirs = dir.GetDirectories();
                foreach (var item in subDirs.Select(sd => new TreeItem(sd.Name, treeItem))) {
                    item.Children.Add(new TreeItem(EmptyItemName, item));

                    treeItem.Children.Add(item);
                }
            } catch {
            }

            treeItem.IsFullyLoaded = true;
        }

        private void UpdateInitialPathUi() {
            if (!Directory.Exists(InitialPath))
                return;

            var initialDir = new DirectoryInfo(InitialPath);

            if (!initialDir.Exists)
                return;

            var stack = TraverseUpToRoot(initialDir);
            var containerGenerator = TreeView.ItemContainerGenerator;
            var uiContext = TaskScheduler.FromCurrentSynchronizationContext();
            DirectoryInfo currentDir = null;
            var dirContainer = Root;

            var waitEvent = new AutoResetEvent(true);

            var processStackTask = Task.Factory.StartNew(() => {
                while (stack.Count > 0) {
                    waitEvent.WaitOne();

                    currentDir = stack.Pop();

                    var waitGeneratorTask = Task.Factory.StartNew(() => {
                        if (containerGenerator == null)
                            return;

                        while (containerGenerator.Status != GeneratorStatus.ContainersGenerated)
                            Thread.Sleep(50);
                    }, CancellationToken.None, TaskCreationOptions.None, TaskScheduler.Default);

                    var updateUiTask = waitGeneratorTask.ContinueWith(r => {
                        try {
                            var childItem = dirContainer.Children.Where(c => c.Name == currentDir.Name).FirstOrDefault();
                            var treeViewItem = containerGenerator.ContainerFromItem(childItem) as TreeViewItem;
                            dirContainer = treeViewItem.DataContext as TreeItem;
                            treeViewItem.IsExpanded = true;

                            treeViewItem.Focus();

                            containerGenerator = treeViewItem.ItemContainerGenerator;
                        } catch { }

                        waitEvent.Set();
                    }, uiContext);
                }

            }, CancellationToken.None, TaskCreationOptions.None, TaskScheduler.Default);
        }

        private static Stack<DirectoryInfo> TraverseUpToRoot(DirectoryInfo child) {
            if (child == null)
                return null;

            if (!child.Exists)
                return null;

            var queue = new Stack<DirectoryInfo>();
            queue.Push(child);
            var ti = child.Parent;

            while (ti != null) {
                queue.Push(ti);
                ti = ti.Parent;
            }

            return queue;
        }
        #endregion

        #region ContextMenu items
        private static void CreateNewFolderImpl(TreeItem parent) {
            try {
                if (parent == null)
                    return;

                var parentPath = parent.GetFullPath();
                var newDirName = GenerateNewFolderName(parentPath);
                var newPath = Path.Combine(parentPath, newDirName);

                Directory.CreateDirectory(newPath);

                var childs = parent.Children;
                var newChild = new TreeItem(newDirName, parent);
                childs.Add(newChild);
                parent.Children = childs.OrderBy(c => c.Name).ToObservableCollection();
            } catch (Exception ex) {
                MessageBox.Show(String.Format("Can't create new folder. Error: {0}", ex.Message));
            }
        }

        private static string GenerateNewFolderName(string parentPath) {
            var result = NewFolderName;

            if (Directory.Exists(Path.Combine(parentPath, result))) {
                for (var i = 1; i < MaxNewFolderSuffix; ++i) {
                    var nameWithIndex = String.Format(NewFolderName + " {0}", i);

                    if (Directory.Exists(Path.Combine(parentPath, nameWithIndex))) continue;
                    result = nameWithIndex;
                    break;
                }
            }

            return result;
        }

        private void CreateMenuItemClick(object sender, RoutedEventArgs e) {
            var item = sender as MenuItem;
            if (item == null) return;
            var context = item.DataContext as TreeItem;
            CreateNewFolderImpl(context);
        }

        private void RenameMenuItemClick(object sender, RoutedEventArgs e) {
            try {
                var item = sender as MenuItem;
                if (item != null) {
                    var context = item.DataContext as TreeItem;
                    if (context != null && !(context is DriveTreeItem)) {
                        var dialog = new InputDialog {
                            Message = "New folder name:",
                            InputText = context.Name,
                            Title = String.Format("Do you really want to rename folder {0}?", context.Name)
                        };

                        if (dialog.ShowDialog() == true) {
                            var newFolderName = dialog.InputText;

                            /*
                             * Parent for context is always not null due to the fact
                             * that we don't allow to change the name of DriveTreeItem
                             */
                            var newFolderFullPath = Path.Combine(context.Parent.GetFullPath(), newFolderName);
                            if (Directory.Exists(newFolderFullPath)) {
                                MessageBox.Show(String.Format("Directory already exists: {0}", newFolderFullPath));
                            } else {
                                Directory.Move(context.GetFullPath(), newFolderFullPath);
                                context.Name = newFolderName;
                            }
                        }
                    }
                }
            } catch (Exception ex) {
                MessageBox.Show(String.Format("Can't rename folder. Error: {0}", ex.Message));
            }
        }

        private void DeleteMenuItemClick(object sender, RoutedEventArgs e) {
            try {
                var item = sender as MenuItem;
                if (item != null) {
                    var context = item.DataContext as TreeItem;
                    if (context != null && !(context is DriveTreeItem)) {
                        var confirmed =
                            MessageBox.Show(
                                String.Format("Do you really want to delete folder {0}?", context.Name),
                                "Confirm folder removal",
                                MessageBoxButton.YesNo);

                        if (confirmed == MessageBoxResult.Yes) {
                            Directory.Delete(context.GetFullPath());
                            var parent = context.Parent;
                            parent.Children.Remove(context);
                        }
                    }
                }
            } catch (Exception ex) {
                MessageBox.Show(String.Format("Can't delete folder. Error: {0}", ex.Message));
            }
        }
        #endregion

    }
}
