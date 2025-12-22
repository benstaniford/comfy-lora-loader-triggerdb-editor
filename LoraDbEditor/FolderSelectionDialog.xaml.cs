using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using LoraDbEditor.Models;

namespace LoraDbEditor
{
    public partial class FolderSelectionDialog : Window, INotifyPropertyChanged
    {
        private List<string> _allFilePaths;
        private bool _isPathValid = false;

        public string SelectedPath { get; private set; } = "";

        public bool IsPathValid
        {
            get => _isPathValid;
            set
            {
                _isPathValid = value;
                OnPropertyChanged();
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public FolderSelectionDialog(List<string> allFilePaths)
        {
            InitializeComponent();
            DataContext = this;
            _allFilePaths = allFilePaths;
            BuildFolderTree();
        }

        private void BuildFolderTree()
        {
            var root = new ObservableCollection<TreeViewNode>();
            var folderSet = new HashSet<string>();

            // Add root level option
            var rootNode = new TreeViewNode
            {
                Name = "(Root Folder)",
                FullPath = "",
                IsFile = false
            };
            root.Add(rootNode);

            // Extract all unique folder paths
            foreach (var path in _allFilePaths)
            {
                var parts = path.Split('/');
                string currentPath = "";

                // Process all parts except the last one (which is the file)
                for (int i = 0; i < parts.Length - 1; i++)
                {
                    currentPath = string.IsNullOrEmpty(currentPath) ? parts[i] : currentPath + "/" + parts[i];
                    folderSet.Add(currentPath);
                }
            }

            // Build tree structure
            var folderDict = new Dictionary<string, TreeViewNode>();

            foreach (var folder in folderSet.OrderBy(f => f))
            {
                var parts = folder.Split('/');
                string currentPath = "";

                for (int i = 0; i < parts.Length; i++)
                {
                    var part = parts[i];
                    var prevPath = currentPath;
                    currentPath = string.IsNullOrEmpty(currentPath) ? part : currentPath + "/" + part;

                    if (!folderDict.ContainsKey(currentPath))
                    {
                        var node = new TreeViewNode
                        {
                            Name = part,
                            FullPath = currentPath,
                            IsFile = false
                        };

                        folderDict[currentPath] = node;

                        if (i == 0)
                        {
                            // Top level folder - add to root
                            root.Add(node);
                        }
                        else
                        {
                            // Sub-folder - add to parent
                            if (folderDict.TryGetValue(prevPath, out var parent))
                            {
                                parent.Children.Add(node);
                            }
                        }
                    }
                }
            }

            FolderTreeView.ItemsSource = root;
        }

        private void FolderTreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (e.NewValue is TreeViewNode node)
            {
                PathTextBox.Text = node.FullPath;
                ValidatePath();
            }
        }

        private void PathTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            ValidatePath();
        }

        private void ValidatePath()
        {
            var path = PathTextBox.Text?.Trim() ?? "";

            // Path is valid if:
            // 1. It's not empty OR
            // 2. It doesn't contain invalid characters OR
            // 3. It doesn't end with a slash
            if (string.IsNullOrWhiteSpace(path))
            {
                // Empty path means root folder - valid
                IsPathValid = true;
            }
            else
            {
                // Check for invalid characters (basic validation)
                var invalidChars = System.IO.Path.GetInvalidFileNameChars();
                var hasInvalidChars = path.Split('/').Any(part =>
                    string.IsNullOrWhiteSpace(part) || part.IndexOfAny(invalidChars) >= 0);

                IsPathValid = !hasInvalidChars && !path.EndsWith("/");
            }
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            SelectedPath = PathTextBox.Text?.Trim() ?? "";
            DialogResult = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
