using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using LoraDbEditor.Models;
using LoraDbEditor.Services;

namespace LoraDbEditor
{
    public partial class MainWindow : Window
    {
        private readonly LoraDatabase _database;
        private readonly FileSystemScanner _scanner;
        private readonly string _galleryBasePath;
        private List<string> _allFilePaths = new();
        private ObservableCollection<TreeViewNode> _treeNodes = new();
        private LoraEntry? _currentEntry;
        private bool _isNewEntry = false;
        private bool _isLoadingEntry = false;
        private bool _hasUnsavedChanges = false;
        private bool _isGitAvailable = false;
        private bool _isGitRepo = false;
        private Point _dragStartPoint;
        private TreeViewNode? _draggedNode;
        private TreeViewItem? _dragHoverItem;
        private Border? _dragHoverBorder;
        private Brush? _dragHoverOriginalBackground;

        public MainWindow()
        {
            InitializeComponent();
            _database = new LoraDatabase();
            _scanner = new FileSystemScanner(_database.LorasBasePath);

            // Set up gallery base path
            string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            _galleryBasePath = System.IO.Path.Combine(userProfile, "Documents", "ComfyUI", "user", "default", "user-db", "lora-triggers-pictures");

            // Ensure gallery directory exists
            if (!Directory.Exists(_galleryBasePath))
            {
                Directory.CreateDirectory(_galleryBasePath);
            }
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                StatusText.Text = "Loading database...";

                // Load database
                await _database.LoadAsync();

                // Scan file system
                StatusText.Text = "Scanning file system...";
                _allFilePaths = _scanner.ScanForLoraFiles();

                // Build tree view
                BuildTreeView();

                // Populate search combo box
                SearchComboBox.ItemsSource = _allFilePaths;

                // Setup text changed event for fuzzy search
                var textBox = (TextBox)SearchComboBox.Template.FindName("PART_EditableTextBox", SearchComboBox);
                if (textBox != null)
                {
                    textBox.TextChanged += SearchTextBox_TextChanged;
                }

                // Check for git availability
                await CheckGitAvailabilityAsync();

                StatusText.Text = $"Ready. Found {_allFilePaths.Count} LoRA files, {_database.GetAllEntries().Count()} database entries.";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                StatusText.Text = "Error loading database.";
            }
        }

        private void BuildTreeView()
        {
            _treeNodes.Clear();

            // Build a flat dictionary first with full paths as keys
            var allNodes = new Dictionary<string, TreeViewNode>();

            // First, add all directories from the filesystem
            if (Directory.Exists(_database.LorasBasePath))
            {
                AddAllDirectories(_database.LorasBasePath, "", allNodes);
            }

            // Then, add all files from the scanned paths
            foreach (var path in _allFilePaths)
            {
                AddFileNode(path, allNodes);
            }

            // Now build the hierarchy by linking parent-child relationships
            var rootCollection = new ObservableCollection<TreeViewNode>();
            
            foreach (var kvp in allNodes.OrderBy(x => x.Key))
            {
                var node = kvp.Value;
                var path = kvp.Key;
                
                // Check if this is a root-level node (no slash in path)
                if (!path.Contains('/'))
                {
                    rootCollection.Add(node);
                }
                else
                {
                    // Find parent and add to parent's children
                    var lastSlash = path.LastIndexOf('/');
                    var parentPath = path.Substring(0, lastSlash);
                    
                    if (allNodes.ContainsKey(parentPath))
                    {
                        var parent = allNodes[parentPath];
                        if (!parent.Children.Contains(node))
                        {
                            parent.Children.Add(node);
                        }
                    }
                }
            }

            // Sort children in each node
            SortTreeNodes(rootCollection);

            FileTreeView.ItemsSource = rootCollection;
        }

        private void SortTreeNodes(ObservableCollection<TreeViewNode> nodes)
        {
            // Sort current level: folders first, then by name
            var sorted = nodes.OrderBy(x => x.IsFile).ThenBy(x => x.Name).ToList();
            nodes.Clear();
            foreach (var node in sorted)
            {
                nodes.Add(node);
                if (!node.IsFile && node.Children.Count > 0)
                {
                    SortTreeNodes(node.Children);
                }
            }
        }

        private void AddAllDirectories(string basePath, string relativePath, Dictionary<string, TreeViewNode> allNodes)
        {
            string currentDirPath = string.IsNullOrEmpty(relativePath) 
                ? basePath 
                : Path.Combine(basePath, relativePath.Replace("/", Path.DirectorySeparatorChar.ToString()));

            if (!Directory.Exists(currentDirPath))
                return;

            var directories = Directory.GetDirectories(currentDirPath);
            
            foreach (var dir in directories)
            {
                var dirName = Path.GetFileName(dir);
                var dirRelativePath = string.IsNullOrEmpty(relativePath) 
                    ? dirName 
                    : relativePath + "/" + dirName;

                // Add this directory node if it doesn't exist
                if (!allNodes.ContainsKey(dirRelativePath))
                {
                    allNodes[dirRelativePath] = new TreeViewNode
                    {
                        Name = dirName,
                        FullPath = dirRelativePath,
                        IsFile = false
                    };
                }

                // Recursively add subdirectories
                AddAllDirectories(basePath, dirRelativePath, allNodes);
            }
        }

        private void AddFileNode(string path, Dictionary<string, TreeViewNode> allNodes)
        {
            var parts = path.Split('/');
            string currentPath = "";

            for (int i = 0; i < parts.Length; i++)
            {
                var part = parts[i];
                var previousPath = currentPath;
                currentPath = string.IsNullOrEmpty(currentPath) ? part : currentPath + "/" + part;
                bool isFileNode = (i == parts.Length - 1);

                if (!allNodes.ContainsKey(currentPath))
                {
                    allNodes[currentPath] = new TreeViewNode
                    {
                        Name = part,
                        FullPath = currentPath,
                        IsFile = isFileNode
                    };
                }
            }
        }

        private void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            var textBox = sender as TextBox;
            if (textBox == null) return;

            string searchText = textBox.Text;

            if (string.IsNullOrWhiteSpace(searchText))
            {
                SearchComboBox.ItemsSource = _allFilePaths;
            }
            else
            {
                var filtered = FileSystemScanner.FuzzySearch(_allFilePaths, searchText);
                SearchComboBox.ItemsSource = filtered;
                SearchComboBox.IsDropDownOpen = filtered.Count > 0;
            }
        }

        private void SearchComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (SearchComboBox.SelectedItem is string selectedPath)
            {
                LoadLoraEntry(selectedPath);
            }
        }

        private void FileTreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (e.NewValue is TreeViewNode node && node.IsFile)
            {
                LoadLoraEntry(node.FullPath);
            }
        }

        private void FileTreeView_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.F2)
            {
                RenameSelectedLora();
                e.Handled = true;
            }
            else if (e.Key == Key.Delete)
            {
                DeleteSelectedLora();
                e.Handled = true;
            }
        }

        private void FileTreeView_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _dragStartPoint = e.GetPosition(null);
        }

        private void FileTreeView_MouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                Point currentPosition = e.GetPosition(null);
                Vector diff = _dragStartPoint - currentPosition;

                if (Math.Abs(diff.X) > SystemParameters.MinimumHorizontalDragDistance ||
                    Math.Abs(diff.Y) > SystemParameters.MinimumVerticalDragDistance)
                {
                    var treeView = sender as TreeView;
                    if (treeView?.SelectedItem is TreeViewNode node && node.IsFile)
                    {
                        _draggedNode = node;
                        var dragData = new DataObject("TreeViewNode", node);
                        DragDrop.DoDragDrop(treeView, dragData, DragDropEffects.Move);
                        _draggedNode = null;
                    }
                }
            }
        }

        private void FileTreeView_DragOver(object sender, DragEventArgs e)
        {
            // Check if this is a URL drop (download)
            bool isUrlDrop = e.Data.GetDataPresent(DataFormats.Text) ||
                            e.Data.GetDataPresent(DataFormats.UnicodeText) ||
                            e.Data.GetDataPresent(DataFormats.Html);

            // Check if this is a file move
            bool isFileDrop = e.Data.GetDataPresent("TreeViewNode");

            if (!isUrlDrop && !isFileDrop)
            {
                e.Effects = DragDropEffects.None;
                ClearDragHoverHighlight();
                StatusText.Text = "DragOver: No valid data";
                return;
            }

            // Handle file move
            if (isFileDrop && !isUrlDrop)
            {
                var draggedNode = e.Data.GetData("TreeViewNode") as TreeViewNode;
                if (draggedNode == null || !draggedNode.IsFile)
                {
                    e.Effects = DragDropEffects.None;
                    ClearDragHoverHighlight();
                    StatusText.Text = "DragOver: Invalid dragged node";
                    return;
                }

                // Get the target node
                var targetNode = GetTreeViewNodeAtPoint(e.GetPosition(FileTreeView));

                if (targetNode == null)
                {
                    // Allow drop to root
                    e.Effects = DragDropEffects.Move;
                    ClearDragHoverHighlight();
                    StatusText.Text = "DragOver: Root (null target)";
                }
                else if (targetNode == draggedNode)
                {
                    // Can't drop on itself
                    e.Effects = DragDropEffects.None;
                    ClearDragHoverHighlight();
                    StatusText.Text = $"DragOver: Can't drop on self ({targetNode.Name})";
                }
                else if (!targetNode.IsFile)
                {
                    // Can drop on folders - highlight the folder
                    e.Effects = DragDropEffects.Move;
                    StatusText.Text = $"DragOver: Folder target - {targetNode.Name}";
                    HighlightDragTarget(targetNode, e.GetPosition(FileTreeView));
                }
                else
                {
                    // Can't drop on other files
                    e.Effects = DragDropEffects.None;
                    ClearDragHoverHighlight();
                    StatusText.Text = $"DragOver: File target (not allowed) - {targetNode.Name}";
                }

                e.Handled = true;
                return;
            }

            // Handle URL drop (download)
            if (isUrlDrop)
            {
                var targetNode = GetTreeViewNodeAtPoint(e.GetPosition(FileTreeView));

                if (targetNode == null)
                {
                    // Allow drop to root
                    e.Effects = DragDropEffects.Copy;
                    ClearDragHoverHighlight();
                    StatusText.Text = "Drop URL here to download to root folder";
                }
                else if (!targetNode.IsFile)
                {
                    // Can drop on folders - highlight the folder
                    e.Effects = DragDropEffects.Copy;
                    StatusText.Text = $"Drop URL here to download to: {targetNode.FullPath}";
                    HighlightDragTarget(targetNode, e.GetPosition(FileTreeView));
                }
                else
                {
                    // Can't drop on files
                    e.Effects = DragDropEffects.None;
                    ClearDragHoverHighlight();
                    StatusText.Text = "Cannot download to a file, drop on a folder instead";
                }

                e.Handled = true;
                return;
            }

            e.Handled = true;
        }

        private void FileTreeView_DragLeave(object sender, DragEventArgs e)
        {
            // Clear highlight when drag leaves the TreeView
            ClearDragHoverHighlight();
        }

        private async void FileTreeView_Drop(object sender, DragEventArgs e)
        {
            // Clear the drag hover highlight
            ClearDragHoverHighlight();

            // Check if this is a URL drop (download)
            bool isUrlDrop = e.Data.GetDataPresent(DataFormats.Text) ||
                            e.Data.GetDataPresent(DataFormats.UnicodeText) ||
                            e.Data.GetDataPresent(DataFormats.Html);

            // Check if this is a file move
            bool isFileDrop = e.Data.GetDataPresent("TreeViewNode");

            // Handle file move
            if (isFileDrop && !isUrlDrop)
            {
                var draggedNode = e.Data.GetData("TreeViewNode") as TreeViewNode;
                if (draggedNode == null || !draggedNode.IsFile)
                    return;

                // Get the target node
                var targetNode = GetTreeViewNodeAtPoint(e.GetPosition(FileTreeView));

                // Determine target directory
                string targetDirectory = "";
                if (targetNode != null && !targetNode.IsFile)
                {
                    targetDirectory = targetNode.FullPath;
                }

                // Perform the move
                MoveLoraToFolder(draggedNode.FullPath, targetDirectory);

                e.Handled = true;
                return;
            }

            // Handle URL drop (download)
            if (isUrlDrop)
            {
                try
                {
                    string? url = null;

                    // Try to get URL from various data formats
                    if (e.Data.GetDataPresent(DataFormats.Text))
                    {
                        url = e.Data.GetData(DataFormats.Text) as string;
                    }
                    else if (e.Data.GetDataPresent(DataFormats.UnicodeText))
                    {
                        url = e.Data.GetData(DataFormats.UnicodeText) as string;
                    }
                    else if (e.Data.GetDataPresent(DataFormats.Html))
                    {
                        // Extract URL from HTML content
                        var html = e.Data.GetData(DataFormats.Html) as string;
                        if (!string.IsNullOrEmpty(html))
                        {
                            // Simple extraction - look for href attribute
                            var match = Regex.Match(html, @"href=""([^""]+)""");
                            if (match.Success)
                            {
                                url = match.Groups[1].Value;
                            }
                        }
                    }

                    if (string.IsNullOrWhiteSpace(url))
                    {
                        MessageBox.Show("No valid URL found in dropped data.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }

                    url = url.Trim();

                    // Validate URL
                    if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
                    {
                        MessageBox.Show("Invalid URL format.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }

                    // Get the target node
                    var targetNode = GetTreeViewNodeAtPoint(e.GetPosition(FileTreeView));

                    // Determine target directory
                    string targetPath = "";
                    if (targetNode != null && !targetNode.IsFile)
                    {
                        targetPath = targetNode.FullPath;
                    }

                    // Download the file to the target folder
                    await DownloadAndAddLoraAsync(url, targetPath);

                    e.Handled = true;
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error processing dropped URL: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }

                return;
            }

            e.Handled = true;
        }

        private TreeViewNode? GetTreeViewNodeAtPoint(Point point)
        {
            var hitTestResult = VisualTreeHelper.HitTest(FileTreeView, point);
            if (hitTestResult == null)
                return null;

            var element = hitTestResult.VisualHit;
            while (element != null && element != FileTreeView)
            {
                if (element is FrameworkElement fe && fe.DataContext is TreeViewNode node)
                {
                    return node;
                }
                element = VisualTreeHelper.GetParent(element);
            }

            return null;
        }

        private TreeViewItem? GetTreeViewItemAtPoint(Point point)
        {
            var hitTestResult = VisualTreeHelper.HitTest(FileTreeView, point);
            if (hitTestResult == null)
                return null;

            var element = hitTestResult.VisualHit;
            while (element != null && element != FileTreeView)
            {
                if (element is TreeViewItem tvi)
                {
                    return tvi;
                }
                element = VisualTreeHelper.GetParent(element);
            }

            return null;
        }

        private void HighlightDragTarget(TreeViewNode targetNode, Point position)
        {
            var treeViewItem = GetTreeViewItemAtPoint(position);

            if (treeViewItem == null)
            {
                StatusText.Text = $"Highlight: TreeViewItem is NULL for {targetNode.Name}";
                return;
            }

            if (treeViewItem.DataContext != targetNode)
            {
                StatusText.Text = $"Highlight: DataContext mismatch - TVI has {(treeViewItem.DataContext as TreeViewNode)?.Name ?? "null"}, expected {targetNode.Name}";
                return;
            }

            if (treeViewItem == _dragHoverItem)
            {
                // Already highlighted, no need to update
                return;
            }

            // Clear previous highlight
            ClearDragHoverHighlight();

            // Store the TreeViewItem
            _dragHoverItem = treeViewItem;

            // Try multiple approaches to make the highlight visible

            // Approach 1: Set TreeViewItem background directly with a bright, opaque color
            var highlightColor = (Color)ColorConverter.ConvertFromString("#007ACC")!;
            _dragHoverItem.Background = new SolidColorBrush(Color.FromArgb(180, highlightColor.R, highlightColor.G, highlightColor.B));
            _dragHoverItem.BorderBrush = new SolidColorBrush(highlightColor);
            _dragHoverItem.BorderThickness = new Thickness(2);

            // Approach 2: Also try to find and highlight the Border in the visual tree
            var border = FindVisualChild<Border>(_dragHoverItem);
            if (border != null)
            {
                _dragHoverBorder = border;
                _dragHoverOriginalBackground = border.Background;
                border.Background = new SolidColorBrush(Color.FromArgb(180, highlightColor.R, highlightColor.G, highlightColor.B));
                StatusText.Text = $"HIGHLIGHTED: {targetNode.Name} (Border found)";
            }
            else
            {
                StatusText.Text = $"HIGHLIGHTED: {targetNode.Name} (Border NOT found)";
            }
        }

        private void ClearDragHoverHighlight()
        {
            if (_dragHoverItem != null)
            {
                // Restore TreeViewItem properties
                _dragHoverItem.Background = Brushes.Transparent;
                _dragHoverItem.BorderBrush = Brushes.Transparent;
                _dragHoverItem.BorderThickness = new Thickness(0);
            }

            if (_dragHoverBorder != null && _dragHoverOriginalBackground != null)
            {
                _dragHoverBorder.Background = _dragHoverOriginalBackground;
                _dragHoverBorder = null;
                _dragHoverOriginalBackground = null;
            }

            _dragHoverItem = null;
        }

        private T? FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
        {
            if (parent == null)
                return null;

            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is T typedChild)
                {
                    return typedChild;
                }

                var result = FindVisualChild<T>(child);
                if (result != null)
                {
                    return result;
                }
            }

            return null;
        }

        private void MoveLoraToFolder(string sourcePath, string targetDirectory)
        {
            var oldPath = sourcePath;
            var oldFullPath = Path.Combine(_database.LorasBasePath, oldPath + ".safetensors");

            // Check if file exists
            if (!File.Exists(oldFullPath))
            {
                MessageBox.Show("The file does not exist on disk.", "File Not Found", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // Get the filename
            var fileName = Path.GetFileName(oldPath);

            // Build new path
            var newPath = string.IsNullOrEmpty(targetDirectory) ? fileName : targetDirectory + "/" + fileName;
            var newFullPath = Path.Combine(_database.LorasBasePath, newPath + ".safetensors");

            // Check if source and target are the same
            if (oldPath == newPath)
            {
                return; // No move needed
            }

            // Check if target already exists
            if (File.Exists(newFullPath))
            {
                MessageBox.Show($"A file with the same name already exists in the target folder:\n{newPath}", 
                    "File Exists", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                // Ensure target directory exists
                var targetDir = Path.GetDirectoryName(newFullPath);
                if (!string.IsNullOrEmpty(targetDir) && !Directory.Exists(targetDir))
                {
                    Directory.CreateDirectory(targetDir);
                }

                // Move the file on disk
                File.Move(oldFullPath, newFullPath);

                // Update the database
                var entry = _database.GetEntry(oldPath);
                if (entry != null)
                {
                    // Remove old entry
                    _database.RemoveEntry(oldPath);

                    // Update entry paths
                    entry.Path = newPath;
                    entry.FullPath = newFullPath;

                    // Add with new path
                    _database.AddEntry(newPath, entry);

                    // Update gallery image filenames if they exist
                    if (entry.Gallery != null && entry.Gallery.Count > 0)
                    {
                        UpdateGalleryFilenames(oldPath, newPath, entry);
                    }
                }

                // Mark as changed
                _hasUnsavedChanges = true;
                SaveButton.IsEnabled = true;

                // Refresh file list and tree view
                _allFilePaths = _scanner.ScanForLoraFiles();
                BuildTreeView();
                SearchComboBox.ItemsSource = _allFilePaths;

                // Select the moved file in the tree
                SelectAndExpandPath(newPath);

                // Load the moved entry
                LoadLoraEntry(newPath);

                StatusText.Text = $"Moved {oldPath} to {newPath}. Don't forget to save!";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error moving file: {ex.Message}", "Move Error", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void RenameMenuItem_Click(object sender, RoutedEventArgs e)
        {
            RenameSelectedLora();
        }

        private void NewFolderMenuItem_Click(object sender, RoutedEventArgs e)
        {
            CreateNewFolder();
        }

        private void DeleteMenuItem_Click(object sender, RoutedEventArgs e)
        {
            DeleteSelectedLora();
        }

        private void CreateNewFolder()
        {
            // Determine parent directory based on selection
            string parentDirectory = "";
            if (FileTreeView.SelectedItem is TreeViewNode node)
            {
                if (node.IsFile)
                {
                    // If a file is selected, create folder in its parent directory
                    var parentPath = Path.GetDirectoryName(node.FullPath)?.Replace("\\", "/");
                    parentDirectory = parentPath ?? "";
                }
                else
                {
                    // If a folder is selected, create subfolder inside it
                    parentDirectory = node.FullPath;
                }
            }

            // Show create folder dialog
            var dialog = new Window
            {
                Title = "Create New Folder",
                Width = 450,
                Height = 180,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this,
                Background = (SolidColorBrush)FindResource("BackgroundBrush"),
                ResizeMode = ResizeMode.NoResize
            };

            var grid = new Grid { Margin = new Thickness(15) };
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var locationText = string.IsNullOrEmpty(parentDirectory) 
                ? "Location: (root)" 
                : $"Location: {parentDirectory}";

            var locationLabel = new TextBlock
            {
                Text = locationText,
                Foreground = (SolidColorBrush)FindResource("TextBrush"),
                Margin = new Thickness(0, 0, 0, 15),
                Opacity = 0.7
            };
            Grid.SetRow(locationLabel, 0);
            grid.Children.Add(locationLabel);

            var label = new TextBlock
            {
                Text = "Folder name:",
                Foreground = (SolidColorBrush)FindResource("TextBrush"),
                Margin = new Thickness(0, 0, 0, 10)
            };
            Grid.SetRow(label, 1);
            grid.Children.Add(label);

            var textBox = new TextBox
            {
                Text = "",
                Foreground = (SolidColorBrush)FindResource("TextBrush"),
                Background = (SolidColorBrush)FindResource("SurfaceBrush"),
                BorderBrush = (SolidColorBrush)FindResource("BorderBrush"),
                Padding = new Thickness(5),
                FontSize = 13
            };
            Grid.SetRow(textBox, 2);
            grid.Children.Add(textBox);

            var buttonPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 15, 0, 0)
            };
            Grid.SetRow(buttonPanel, 3);

            var okButton = new Button
            {
                Content = "Create",
                Width = 80,
                Height = 30,
                Margin = new Thickness(0, 0, 10, 0),
                IsDefault = true
            };

            var cancelButton = new Button
            {
                Content = "Cancel",
                Width = 80,
                Height = 30,
                IsCancel = true
            };

            bool dialogResult = false;
            okButton.Click += (s, e) =>
            {
                dialogResult = true;
                dialog.Close();
            };

            cancelButton.Click += (s, e) =>
            {
                dialog.Close();
            };

            buttonPanel.Children.Add(okButton);
            buttonPanel.Children.Add(cancelButton);
            grid.Children.Add(buttonPanel);

            dialog.Content = grid;
            textBox.Focus();

            dialog.ShowDialog();

            if (!dialogResult)
                return;

            var folderName = textBox.Text.Trim();

            // Validate folder name
            if (string.IsNullOrWhiteSpace(folderName))
            {
                MessageBox.Show("Folder name cannot be empty.", "Invalid Name", 
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Check for invalid characters
            var invalidChars = Path.GetInvalidFileNameChars();
            if (folderName.IndexOfAny(invalidChars) >= 0 || folderName.Contains("/") || folderName.Contains("\\"))
            {
                MessageBox.Show("Folder name contains invalid characters.", "Invalid Name", 
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Build full folder path
            var folderPath = string.IsNullOrEmpty(parentDirectory) 
                ? folderName 
                : parentDirectory + "/" + folderName;
            var fullDiskPath = Path.Combine(_database.LorasBasePath, folderPath);

            // Check if folder already exists
            if (Directory.Exists(fullDiskPath))
            {
                MessageBox.Show($"A folder already exists at: {folderPath}", "Folder Exists", 
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                // Create the directory
                Directory.CreateDirectory(fullDiskPath);

                // Refresh file list and tree view
                _allFilePaths = _scanner.ScanForLoraFiles();
                BuildTreeView();
                SearchComboBox.ItemsSource = _allFilePaths;

                // Try to select the new folder in the tree
                SelectAndExpandPath(folderPath);

                StatusText.Text = $"Created folder: {folderPath}";
                MessageBox.Show($"Successfully created folder:\n\n{folderPath}", 
                    "Folder Created", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error creating folder: {ex.Message}", "Create Folder Error", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SelectAndExpandPath(string path)
        {
            // Walk through the tree to find and select the node
            var parts = path.Split('/');
            var nodes = FileTreeView.ItemsSource as ObservableCollection<TreeViewNode>;
            
            if (nodes == null)
                return;

            TreeViewNode? currentNode = null;
            var currentCollection = nodes;

            foreach (var part in parts)
            {
                currentNode = currentCollection.FirstOrDefault(n => n.Name == part);
                if (currentNode == null)
                    break;

                currentCollection = currentNode.Children;
            }

            if (currentNode != null)
            {
                // Need to wait for tree to be rendered before selecting
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    SelectTreeViewNode(FileTreeView, currentNode);
                }), System.Windows.Threading.DispatcherPriority.Loaded);
            }
        }

        private void SelectTreeViewNode(ItemsControl parent, TreeViewNode node)
        {
            // First, we need to expand all parent nodes to ensure containers are generated
            var pathParts = node.FullPath.Split('/');
            ItemsControl currentParent = parent;
            
            for (int i = 0; i < pathParts.Length; i++)
            {
                var part = pathParts[i];
                var isLast = (i == pathParts.Length - 1);
                
                foreach (var item in currentParent.Items)
                {
                    if (item is TreeViewNode itemNode && itemNode.Name == part)
                    {
                        var container = currentParent.ItemContainerGenerator.ContainerFromItem(item) as TreeViewItem;
                        
                        if (container == null)
                        {
                            // Container not generated yet, force generation
                            currentParent.UpdateLayout();
                            container = currentParent.ItemContainerGenerator.ContainerFromItem(item) as TreeViewItem;
                        }
                        
                        if (container != null)
                        {
                            if (isLast)
                            {
                                // This is the target node - select and scroll into view
                                container.IsSelected = true;
                                container.BringIntoView();
                            }
                            else
                            {
                                // This is a parent node - expand it
                                container.IsExpanded = true;
                                container.UpdateLayout();
                                currentParent = container;
                            }
                        }
                        break;
                    }
                }
            }
        }

        private void RenameSelectedLora()
        {
            if (FileTreeView.SelectedItem is not TreeViewNode node || !node.IsFile)
            {
                MessageBox.Show("Please select a LoRA file to rename.", "No File Selected", 
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var oldPath = node.FullPath;
            var oldFullPath = Path.Combine(_database.LorasBasePath, oldPath + ".safetensors");

            // Check if file exists
            if (!File.Exists(oldFullPath))
            {
                MessageBox.Show("The selected file does not exist on disk.", "File Not Found", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // Get current name (without extension)
            var currentName = Path.GetFileName(oldPath);
            var directory = Path.GetDirectoryName(oldPath)?.Replace("\\", "/") ?? "";

            // Show rename dialog
            var dialog = new Window
            {
                Title = "Rename LoRA",
                Width = 450,
                Height = 180,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this,
                Background = (SolidColorBrush)FindResource("BackgroundBrush"),
                ResizeMode = ResizeMode.NoResize
            };

            var grid = new Grid { Margin = new Thickness(15) };
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var label = new TextBlock
            {
                Text = "New name (without .safetensors extension):",
                Foreground = (SolidColorBrush)FindResource("TextBrush"),
                Margin = new Thickness(0, 0, 0, 10)
            };
            Grid.SetRow(label, 0);
            grid.Children.Add(label);

            var textBox = new TextBox
            {
                Text = currentName,
                Foreground = (SolidColorBrush)FindResource("TextBrush"),
                Background = (SolidColorBrush)FindResource("SurfaceBrush"),
                BorderBrush = (SolidColorBrush)FindResource("BorderBrush"),
                Padding = new Thickness(5),
                FontSize = 13
            };
            Grid.SetRow(textBox, 1);
            grid.Children.Add(textBox);

            var buttonPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 15, 0, 0)
            };
            Grid.SetRow(buttonPanel, 3);

            var okButton = new Button
            {
                Content = "OK",
                Width = 80,
                Height = 30,
                Margin = new Thickness(0, 0, 10, 0),
                IsDefault = true
            };

            var cancelButton = new Button
            {
                Content = "Cancel",
                Width = 80,
                Height = 30,
                IsCancel = true
            };

            bool dialogResult = false;
            okButton.Click += (s, e) =>
            {
                dialogResult = true;
                dialog.Close();
            };

            cancelButton.Click += (s, e) =>
            {
                dialog.Close();
            };

            buttonPanel.Children.Add(okButton);
            buttonPanel.Children.Add(cancelButton);
            grid.Children.Add(buttonPanel);

            dialog.Content = grid;
            textBox.SelectAll();
            textBox.Focus();

            dialog.ShowDialog();

            if (!dialogResult)
                return;

            var newName = textBox.Text.Trim();

            // Validate new name
            if (string.IsNullOrWhiteSpace(newName))
            {
                MessageBox.Show("Name cannot be empty.", "Invalid Name", 
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (newName == currentName)
            {
                return; // No change
            }

            // Check for invalid characters
            var invalidChars = Path.GetInvalidFileNameChars();
            if (newName.IndexOfAny(invalidChars) >= 0)
            {
                MessageBox.Show("Name contains invalid characters.", "Invalid Name", 
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Build new path
            var newPath = string.IsNullOrEmpty(directory) ? newName : directory + "/" + newName;
            var newFullPath = Path.Combine(_database.LorasBasePath, newPath + ".safetensors");

            // Check if target already exists
            if (File.Exists(newFullPath))
            {
                MessageBox.Show($"A file already exists at: {newPath}", "File Exists", 
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                // Rename the file on disk
                File.Move(oldFullPath, newFullPath);

                // Update the database
                var entry = _database.GetEntry(oldPath);
                if (entry != null)
                {
                    // Remove old entry
                    _database.RemoveEntry(oldPath);

                    // Update entry paths
                    entry.Path = newPath;
                    entry.FullPath = newFullPath;

                    // Add with new path
                    _database.AddEntry(newPath, entry);

                    // Update gallery image filenames if they exist
                    if (entry.Gallery != null && entry.Gallery.Count > 0)
                    {
                        UpdateGalleryFilenames(oldPath, newPath, entry);
                    }
                }

                // Mark as changed
                _hasUnsavedChanges = true;
                SaveButton.IsEnabled = true;

                // Refresh file list and tree view
                _allFilePaths = _scanner.ScanForLoraFiles();
                BuildTreeView();
                SearchComboBox.ItemsSource = _allFilePaths;

                // Select the renamed file in the tree
                SelectAndExpandPath(newPath);

                // Load the renamed entry
                LoadLoraEntry(newPath);

                StatusText.Text = $"Renamed {oldPath} to {newPath}. Don't forget to save!";
                MessageBox.Show($"Successfully renamed:\n\nFrom: {oldPath}\nTo: {newPath}", 
                    "Rename Complete", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error renaming file: {ex.Message}", "Rename Error", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void UpdateGalleryFilenames(string oldPath, string newPath, LoraEntry entry)
        {
            if (entry.Gallery == null)
                return;

            var oldSafePath = oldPath.Replace("/", "_").Replace("\\", "_");
            var newSafePath = newPath.Replace("/", "_").Replace("\\", "_");

            var updatedGallery = new List<string>();

            foreach (var oldFileName in entry.Gallery)
            {
                // Check if the filename starts with the old safe path
                if (oldFileName.StartsWith(oldSafePath + "_"))
                {
                    var oldFilePath = Path.Combine(_galleryBasePath, oldFileName);
                    if (File.Exists(oldFilePath))
                    {
                        // Create new filename by replacing the prefix
                        var newFileName = newSafePath + oldFileName.Substring(oldSafePath.Length);
                        var newFilePath = Path.Combine(_galleryBasePath, newFileName);

                        try
                        {
                            // Rename the gallery image file
                            File.Move(oldFilePath, newFilePath);
                            updatedGallery.Add(newFileName);
                        }
                        catch
                        {
                            // If rename fails, keep old filename
                            updatedGallery.Add(oldFileName);
                        }
                    }
                    else
                    {
                        // File doesn't exist, update the name anyway
                        var newFileName = newSafePath + oldFileName.Substring(oldSafePath.Length);
                        updatedGallery.Add(newFileName);
                    }
                }
                else
                {
                    // Filename doesn't match pattern, keep as is
                    updatedGallery.Add(oldFileName);
                }
            }

            entry.Gallery = updatedGallery;
        }

        private void DeleteSelectedLora()
        {
            if (FileTreeView.SelectedItem is not TreeViewNode node || !node.IsFile)
            {
                MessageBox.Show("Please select a LoRA file to delete.", "No File Selected",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var loraPath = node.FullPath;
            var fullPath = Path.Combine(_database.LorasBasePath, loraPath + ".safetensors");

            // Confirm deletion
            var result = MessageBox.Show(
                $"Are you sure you want to delete this LoRA?\n\n{loraPath}\n\nThis will delete:\n- The .safetensors file\n- The database entry (if it exists)\n- All associated gallery images\n\nThis action cannot be undone!",
                "Confirm Delete",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes)
            {
                return;
            }

            try
            {
                // Get the database entry if it exists (to delete gallery images)
                var entry = _database.GetEntry(loraPath);

                // Delete gallery images if they exist
                if (entry?.Gallery != null && entry.Gallery.Count > 0)
                {
                    var safePath = loraPath.Replace("/", "_").Replace("\\", "_");
                    foreach (var imageName in entry.Gallery)
                    {
                        try
                        {
                            var imagePath = Path.Combine(_galleryBasePath, imageName);
                            if (File.Exists(imagePath))
                            {
                                File.Delete(imagePath);
                            }
                        }
                        catch (Exception ex)
                        {
                            // Log but continue - don't fail the whole operation for a gallery image
                            System.Diagnostics.Debug.WriteLine($"Failed to delete gallery image {imageName}: {ex.Message}");
                        }
                    }
                }

                // Delete the database entry if it exists
                if (entry != null)
                {
                    _database.RemoveEntry(loraPath);
                    _hasUnsavedChanges = true;
                    SaveButton.IsEnabled = true;
                }

                // Delete the .safetensors file if it exists
                if (File.Exists(fullPath))
                {
                    File.Delete(fullPath);
                }

                // Clear the details panel if this was the currently loaded entry
                if (_currentEntry?.Path == loraPath)
                {
                    _currentEntry = null;
                    DetailsPanel.Visibility = Visibility.Collapsed;
                }

                // Refresh file list and tree view
                _allFilePaths = _scanner.ScanForLoraFiles();
                BuildTreeView();
                SearchComboBox.ItemsSource = _allFilePaths;

                StatusText.Text = $"Deleted {loraPath}. Don't forget to save!";
                MessageBox.Show($"Successfully deleted:\n\n{loraPath}",
                    "Delete Complete", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error deleting file: {ex.Message}", "Delete Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void LoadLoraEntry(string path)
        {
            try
            {
                // Get or create entry
                var entry = _database.GetEntry(path);
                _isNewEntry = (entry == null);

                if (entry == null)
                {
                    // Create new entry for file not in database
                    entry = new LoraEntry
                    {
                        Path = path,
                        FullPath = System.IO.Path.Combine(_database.LorasBasePath, path + ".safetensors"),
                        FileExists = System.IO.File.Exists(System.IO.Path.Combine(_database.LorasBasePath, path + ".safetensors"))
                    };

                    if (entry.FileExists)
                    {
                        entry.CalculatedFileId = FileIdCalculator.CalculateFileId(entry.FullPath);
                    }
                }
                else
                {
                    // Entry exists in database - ensure runtime properties are populated
                    entry.FileExists = System.IO.File.Exists(entry.FullPath);

                    if (entry.FileExists)
                    {
                        try
                        {
                            entry.CalculatedFileId = FileIdCalculator.CalculateFileId(entry.FullPath);
                            entry.FileIdValid = !string.IsNullOrEmpty(entry.FileId) &&
                                               entry.FileId != "unknown" &&
                                               entry.FileId == entry.CalculatedFileId;
                        }
                        catch
                        {
                            entry.FileIdValid = false;
                        }
                    }
                    else
                    {
                        entry.FileIdValid = false;
                    }
                }

                _currentEntry = entry;

                // Update UI
                SelectedFileTitle.Text = path;
                DetailsPanel.Visibility = Visibility.Visible;

                // Display File ID
                if (!string.IsNullOrEmpty(entry.FileId))
                {
                    FileIdText.Text = entry.FileId;
                }
                else
                {
                    FileIdText.Text = "(not set)";
                }

                // Validate File ID
                UpdateFileIdButton.Visibility = Visibility.Collapsed;

                if (_isNewEntry && entry.FileExists)
                {
                    // New entry - file exists but not in database
                    FileIdWarningBorder.Visibility = Visibility.Visible;
                    FileIdWarningBorder.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFA500")!);
                    FileIdWarningText.Text = "WARNING: File exists but no database entry found!";
                    FileIdWarningText.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFA500")!);
                    CurrentFileIdText.Text = "(no entry)";
                    ExpectedFileIdText.Text = entry.CalculatedFileId ?? "(calculating...)";
                    UpdateFileIdButton.Visibility = Visibility.Visible;
                    UpdateFileIdButton.Content = "Create new record";
                }
                else if (!entry.FileExists)
                {
                    FileIdWarningBorder.Visibility = Visibility.Visible;
                    FileIdWarningBorder.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F44747")!);
                    FileIdWarningText.Text = "WARNING: File not found on disk!";
                    FileIdWarningText.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F44747")!);
                    CurrentFileIdText.Text = entry.FileId ?? "(none)";
                    ExpectedFileIdText.Text = "(file missing)";
                }
                else if (string.IsNullOrEmpty(entry.FileId) || entry.FileId == "unknown")
                {
                    FileIdWarningBorder.Visibility = Visibility.Visible;
                    FileIdWarningBorder.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFA500")!);
                    FileIdWarningText.Text = "WARNING: File ID is missing or unknown!";
                    FileIdWarningText.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFA500")!);
                    CurrentFileIdText.Text = entry.FileId ?? "(none)";
                    ExpectedFileIdText.Text = entry.CalculatedFileId ?? "(calculating...)";
                    UpdateFileIdButton.Visibility = Visibility.Visible;
                    UpdateFileIdButton.Content = "Update File ID";
                }
                else if (!entry.FileIdValid)
                {
                    FileIdWarningBorder.Visibility = Visibility.Visible;
                    FileIdWarningBorder.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F44747")!);
                    FileIdWarningText.Text = "WARNING: File ID mismatch!";
                    FileIdWarningText.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F44747")!);
                    CurrentFileIdText.Text = entry.FileId;
                    ExpectedFileIdText.Text = entry.CalculatedFileId ?? "(error calculating)";
                    UpdateFileIdButton.Visibility = Visibility.Visible;
                    UpdateFileIdButton.Content = "Update File ID";
                }
                else
                {
                    FileIdWarningBorder.Visibility = Visibility.Collapsed;
                }

                // Display all fields (suppress TextChanged events while loading)
                _isLoadingEntry = true;
                try
                {
                    // Convert \n to actual newlines for display
                    if (!string.IsNullOrEmpty(entry.ActiveTriggers))
                    {
                        ActiveTriggersText.Text = entry.ActiveTriggers.Replace("\n", Environment.NewLine);
                    }
                    else
                    {
                        ActiveTriggersText.Text = "";
                    }

                    // Convert \n to actual newlines for display
                    if (!string.IsNullOrEmpty(entry.AllTriggers))
                    {
                        AllTriggersText.Text = entry.AllTriggers.Replace("\n", Environment.NewLine);
                    }
                    else
                    {
                        AllTriggersText.Text = "";
                    }

                    // Load new optional fields
                    SourceUrlText.Text = entry.SourceUrl ?? "";
                    SuggestedStrengthText.Text = entry.SuggestedStrength ?? "";

                    // Convert \n to actual newlines for notes display
                    if (!string.IsNullOrEmpty(entry.Notes))
                    {
                        NotesText.Text = entry.Notes.Replace("\n", Environment.NewLine);
                    }
                    else
                    {
                        NotesText.Text = "";
                    }
                }
                finally
                {
                    _isLoadingEntry = false;
                }

                // Update the source URL link visibility after loading is complete
                UpdateSourceUrlLink();

                // Load gallery images
                LoadGallery();

                StatusText.Text = $"Loaded: {path}";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading entry: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void UpdateFileIdButton_Click(object sender, RoutedEventArgs e)
        {
            if (_currentEntry == null || string.IsNullOrEmpty(_currentEntry.CalculatedFileId))
            {
                MessageBox.Show("Cannot update file ID.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            try
            {
                if (_isNewEntry)
                {
                    // Set the file ID and add to database if not already added
                    _currentEntry.FileId = _currentEntry.CalculatedFileId;

                    // Check if entry was already added by TextChanged handlers
                    if (_database.GetEntry(_currentEntry.Path) == null)
                    {
                        _database.AddEntry(_currentEntry.Path, _currentEntry);
                    }
                    else
                    {
                        // Entry was already added, just update the file ID
                        _database.UpdateFileId(_currentEntry.Path, _currentEntry.CalculatedFileId);
                    }

                    StatusText.Text = $"Created new record for {_currentEntry.Path}. Don't forget to save!";
                }
                else
                {
                    // Update existing entry
                    _database.UpdateFileId(_currentEntry.Path, _currentEntry.CalculatedFileId);
                    StatusText.Text = $"Updated file ID for {_currentEntry.Path}. Don't forget to save!";
                }

                _hasUnsavedChanges = true;
                SaveButton.IsEnabled = true;

                // Reload the entry to refresh UI
                LoadLoraEntry(_currentEntry.Path);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error updating file ID: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                StatusText.Text = "Saving database...";
                SaveButton.IsEnabled = false;

                await _database.SaveAsync();

                _hasUnsavedChanges = false;
                StatusText.Text = "Database saved successfully.";

                // Check if there are git changes after saving
                if (_isGitAvailable && _isGitRepo)
                {
                    await UpdateCommitButtonStateAsync();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error saving database: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                StatusText.Text = "Error saving database.";
                SaveButton.IsEnabled = true;
            }
        }

        private void ActiveTriggersText_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isLoadingEntry || _currentEntry == null)
                return;

            // Convert actual newlines to \n for storage
            var textWithEncodedNewlines = ActiveTriggersText.Text.Replace(Environment.NewLine, "\n");
            _currentEntry.ActiveTriggers = textWithEncodedNewlines;

            // If this is a new entry, add it to the database
            if (_isNewEntry && _currentEntry.FileExists)
            {
                _database.AddEntry(_currentEntry.Path, _currentEntry);
                _isNewEntry = false; // No longer new since it's in the database
            }

            // Mark as changed
            _hasUnsavedChanges = true;
            SaveButton.IsEnabled = true;
            StatusText.Text = $"Modified: {_currentEntry.Path}. Don't forget to save!";
        }

        private void AllTriggersText_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isLoadingEntry || _currentEntry == null)
                return;

            // Convert actual newlines to \n for storage
            var textWithEncodedNewlines = AllTriggersText.Text.Replace(Environment.NewLine, "\n");
            _currentEntry.AllTriggers = textWithEncodedNewlines;

            // If this is a new entry, add it to the database
            if (_isNewEntry && _currentEntry.FileExists)
            {
                _database.AddEntry(_currentEntry.Path, _currentEntry);
                _isNewEntry = false; // No longer new since it's in the database
            }

            // Mark as changed
            _hasUnsavedChanges = true;
            SaveButton.IsEnabled = true;
            StatusText.Text = $"Modified: {_currentEntry.Path}. Don't forget to save!";
        }

        private void SourceUrlText_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isLoadingEntry || _currentEntry == null)
                return;

            // Update the entry
            _currentEntry.SourceUrl = string.IsNullOrWhiteSpace(SourceUrlText.Text) ? null : SourceUrlText.Text;

            // Update hyperlink visibility and URI
            UpdateSourceUrlLink();

            // If this is a new entry, add it to the database
            if (_isNewEntry && _currentEntry.FileExists)
            {
                _database.AddEntry(_currentEntry.Path, _currentEntry);
                _isNewEntry = false; // No longer new since it's in the database
            }

            // Mark as changed
            _hasUnsavedChanges = true;
            SaveButton.IsEnabled = true;
            StatusText.Text = $"Modified: {_currentEntry.Path}. Don't forget to save!";
        }

        private void UpdateSourceUrlLink()
        {
            var urlText = SourceUrlText.Text?.Trim();

            if (!string.IsNullOrWhiteSpace(urlText) && Uri.TryCreate(urlText, UriKind.Absolute, out var uri))
            {
                SourceUrlHyperlink.NavigateUri = uri;
                SourceUrlLink.Visibility = Visibility.Visible;
            }
            else
            {
                SourceUrlLink.Visibility = Visibility.Collapsed;
            }
        }

        private void SourceUrlHyperlink_RequestNavigate(object sender, System.Windows.Navigation.RequestNavigateEventArgs e)
        {
            try
            {
                // Open the URL in the default browser
                var psi = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = e.Uri.AbsoluteUri,
                    UseShellExecute = true
                };
                System.Diagnostics.Process.Start(psi);
                e.Handled = true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error opening URL: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SourceUrlText_PreviewDragOver(object sender, DragEventArgs e)
        {
            // Accept text or file list data
            if (e.Data.GetDataPresent(DataFormats.Text) ||
                e.Data.GetDataPresent(DataFormats.UnicodeText) ||
                e.Data.GetDataPresent(DataFormats.Html))
            {
                e.Effects = DragDropEffects.Copy;
                e.Handled = true;
            }
            else
            {
                e.Effects = DragDropEffects.None;
            }
        }

        private void SourceUrlText_Drop(object sender, DragEventArgs e)
        {
            try
            {
                string? url = null;

                // Try to get URL from various data formats
                if (e.Data.GetDataPresent(DataFormats.Text))
                {
                    url = e.Data.GetData(DataFormats.Text) as string;
                }
                else if (e.Data.GetDataPresent(DataFormats.UnicodeText))
                {
                    url = e.Data.GetData(DataFormats.UnicodeText) as string;
                }
                else if (e.Data.GetDataPresent(DataFormats.Html))
                {
                    // Extract URL from HTML content
                    var html = e.Data.GetData(DataFormats.Html) as string;
                    if (!string.IsNullOrEmpty(html))
                    {
                        // Simple extraction - look for href attribute
                        var match = System.Text.RegularExpressions.Regex.Match(html, @"href=""([^""]+)""");
                        if (match.Success)
                        {
                            url = match.Groups[1].Value;
                        }
                    }
                }

                if (!string.IsNullOrWhiteSpace(url))
                {
                    SourceUrlText.Text = url.Trim();
                }

                e.Handled = true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error processing dropped URL: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SuggestedStrengthText_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isLoadingEntry || _currentEntry == null)
                return;

            // Update the entry
            _currentEntry.SuggestedStrength = string.IsNullOrWhiteSpace(SuggestedStrengthText.Text) ? null : SuggestedStrengthText.Text;

            // If this is a new entry, add it to the database
            if (_isNewEntry && _currentEntry.FileExists)
            {
                _database.AddEntry(_currentEntry.Path, _currentEntry);
                _isNewEntry = false; // No longer new since it's in the database
            }

            // Mark as changed
            _hasUnsavedChanges = true;
            SaveButton.IsEnabled = true;
            StatusText.Text = $"Modified: {_currentEntry.Path}. Don't forget to save!";
        }

        private void NotesText_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isLoadingEntry || _currentEntry == null)
                return;

            // Convert actual newlines to \n for storage
            var textWithEncodedNewlines = NotesText.Text.Replace(Environment.NewLine, "\n");
            _currentEntry.Notes = string.IsNullOrWhiteSpace(textWithEncodedNewlines) ? null : textWithEncodedNewlines;

            // If this is a new entry, add it to the database
            if (_isNewEntry && _currentEntry.FileExists)
            {
                _database.AddEntry(_currentEntry.Path, _currentEntry);
                _isNewEntry = false; // No longer new since it's in the database
            }

            // Mark as changed
            _hasUnsavedChanges = true;
            SaveButton.IsEnabled = true;
            StatusText.Text = $"Modified: {_currentEntry.Path}. Don't forget to save!";
        }

        private void LoadGallery()
        {
            // Clear existing gallery images (but keep the Add Image box)
            var itemsToRemove = GalleryPanel.Children.OfType<Border>()
                .Where(b => b != AddImageBox)
                .ToList();

            foreach (var item in itemsToRemove)
            {
                GalleryPanel.Children.Remove(item);
            }

            if (_currentEntry?.Gallery == null || _currentEntry.Gallery.Count == 0)
                return;

            // Load each image
            for (int i = 0; i < _currentEntry.Gallery.Count; i++)
            {
                var imageName = _currentEntry.Gallery[i];
                var imagePath = System.IO.Path.Combine(_galleryBasePath, imageName);

                if (System.IO.File.Exists(imagePath))
                {
                    try
                    {
                        var border = new Border
                        {
                            Width = 256,
                            Height = 256,
                            Margin = new Thickness(0, 0, 10, 0),
                            BorderThickness = new Thickness(1),
                            BorderBrush = (SolidColorBrush)FindResource("BorderBrush"),
                            Background = (SolidColorBrush)FindResource("SurfaceBrush"),
                            Cursor = Cursors.Hand,
                            Tag = imagePath // Store the full path in Tag
                        };

                        var image = new System.Windows.Controls.Image
                        {
                            Source = new System.Windows.Media.Imaging.BitmapImage(new Uri(imagePath)),
                            Stretch = Stretch.UniformToFill
                        };

                        border.Child = image;
                        border.MouseLeftButtonDown += GalleryImage_Click;

                        // Insert before the Add Image box
                        var addBoxIndex = GalleryPanel.Children.IndexOf(AddImageBox);
                        GalleryPanel.Children.Insert(addBoxIndex, border);
                    }
                    catch (Exception ex)
                    {
                        // Skip images that can't be loaded
                        System.Diagnostics.Debug.WriteLine($"Failed to load image {imagePath}: {ex.Message}");
                    }
                }
            }
        }

        private void GalleryImage_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (sender is Border border && border.Tag is string imagePath)
            {
                try
                {
                    // Open the image in the default image viewer
                    var psi = new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = imagePath,
                        UseShellExecute = true
                    };
                    System.Diagnostics.Process.Start(psi);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error opening image: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void AddImageBox_DragOver(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                var files = (string[])e.Data.GetData(DataFormats.FileDrop);
                if (files != null && files.Length > 0 && IsImageFile(files[0]))
                {
                    e.Effects = DragDropEffects.Copy;
                    e.Handled = true;
                    return;
                }
            }

            e.Effects = DragDropEffects.None;
        }

        private async void AddImageBox_Drop(object sender, DragEventArgs e)
        {
            if (_currentEntry == null)
                return;

            try
            {
                if (e.Data.GetDataPresent(DataFormats.FileDrop))
                {
                    var files = (string[])e.Data.GetData(DataFormats.FileDrop);
                    if (files != null && files.Length > 0 && IsImageFile(files[0]))
                    {
                        var sourceFile = files[0];
                        var extension = System.IO.Path.GetExtension(sourceFile);

                        // Create a unique filename using the lora path and timestamp
                        var safePath = _currentEntry.Path.Replace("/", "_").Replace("\\", "_");
                        var timestamp = DateTime.Now.ToString("yyyyMMddHHmmss");
                        var fileName = $"{safePath}_{timestamp}{extension}";
                        var destPath = System.IO.Path.Combine(_galleryBasePath, fileName);

                        // Copy the file
                        System.IO.File.Copy(sourceFile, destPath, overwrite: false);

                        // Add to gallery list
                        if (_currentEntry.Gallery == null)
                        {
                            _currentEntry.Gallery = new List<string>();
                        }

                        _currentEntry.Gallery.Add(fileName);

                        // If this is a new entry, add it to the database
                        if (_isNewEntry && _currentEntry.FileExists)
                        {
                            _database.AddEntry(_currentEntry.Path, _currentEntry);
                            _isNewEntry = false;
                        }

                        // Mark as changed
                        _hasUnsavedChanges = true;
                        SaveButton.IsEnabled = true;

                        // Reload gallery
                        LoadGallery();

                        StatusText.Text = $"Added image to gallery for {_currentEntry.Path}. Don't forget to save!";

                        // Update git commit button state since we added a new file
                        if (_isGitAvailable && _isGitRepo)
                        {
                            await UpdateCommitButtonStateAsync();
                        }
                    }
                }

                e.Handled = true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error adding image: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private bool IsImageFile(string filePath)
        {
            var extension = System.IO.Path.GetExtension(filePath)?.ToLowerInvariant();
            return extension == ".jpg" || extension == ".jpeg" || extension == ".png" ||
                   extension == ".bmp" || extension == ".gif" || extension == ".webp";
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            if (_hasUnsavedChanges)
            {
                var result = MessageBox.Show("You have unsaved changes. Do you want to save before exiting?",
                    "Unsaved Changes", MessageBoxButton.YesNoCancel, MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    SaveButton_Click(this, new RoutedEventArgs());
                }
                else if (result == MessageBoxResult.Cancel)
                {
                    e.Cancel = true;
                }
            }

            base.OnClosing(e);
        }

        private void AddLoraZone_PreviewDragOver(object sender, DragEventArgs e)
        {
            // Accept text or HTML data (URLs from browsers)
            if (e.Data.GetDataPresent(DataFormats.Text) ||
                e.Data.GetDataPresent(DataFormats.UnicodeText) ||
                e.Data.GetDataPresent(DataFormats.Html))
            {
                e.Effects = DragDropEffects.Copy;
                e.Handled = true;
            }
            else
            {
                e.Effects = DragDropEffects.None;
            }
        }

        private async void AddLoraZone_Drop(object sender, DragEventArgs e)
        {
            try
            {
                string? url = null;

                // Try to get URL from various data formats
                if (e.Data.GetDataPresent(DataFormats.Text))
                {
                    url = e.Data.GetData(DataFormats.Text) as string;
                }
                else if (e.Data.GetDataPresent(DataFormats.UnicodeText))
                {
                    url = e.Data.GetData(DataFormats.UnicodeText) as string;
                }
                else if (e.Data.GetDataPresent(DataFormats.Html))
                {
                    // Extract URL from HTML content
                    var html = e.Data.GetData(DataFormats.Html) as string;
                    if (!string.IsNullOrEmpty(html))
                    {
                        // Simple extraction - look for href attribute
                        var match = System.Text.RegularExpressions.Regex.Match(html, @"href=""([^""]+)""");
                        if (match.Success)
                        {
                            url = match.Groups[1].Value;
                        }
                    }
                }

                if (string.IsNullOrWhiteSpace(url))
                {
                    MessageBox.Show("No valid URL found in dropped data.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                url = url.Trim();

                // Validate URL
                if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
                {
                    MessageBox.Show("Invalid URL format.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                // Ask user where to save the file
                var folderDialog = new FolderSelectionDialog(_allFilePaths);
                if (folderDialog.ShowDialog() != true)
                {
                    return; // User cancelled
                }

                string targetPath = folderDialog.SelectedPath;

                // Show progress window and download
                await DownloadAndAddLoraAsync(url, targetPath);

                e.Handled = true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error processing dropped URL: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            var settingsDialog = new SettingsDialog();
            settingsDialog.Owner = this;
            settingsDialog.ShowDialog();
        }

        private string ApplyCivitaiApiKey(string url)
        {
            try
            {
                // Check if this is a Civitai URL
                var uri = new Uri(url);
                if (uri.Host.Contains("civitai.com", StringComparison.OrdinalIgnoreCase))
                {
                    // Get the API key from settings
                    var apiKey = SettingsDialog.GetCivitaiApiKey();
                    if (!string.IsNullOrWhiteSpace(apiKey))
                    {
                        // Check if URL already has the token parameter
                        if (url.Contains("?token=", StringComparison.OrdinalIgnoreCase) ||
                            url.Contains("&token=", StringComparison.OrdinalIgnoreCase))
                        {
                            System.Diagnostics.Debug.WriteLine("URL already contains a token parameter");
                            return url;
                        }

                        // Append the API key
                        var separator = url.Contains('?') ? "&" : "?";
                        var authenticatedUrl = $"{url}{separator}token={apiKey}";
                        System.Diagnostics.Debug.WriteLine($"Applied Civitai API key to URL");
                        return authenticatedUrl;
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine("No Civitai API key configured");
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error applying API key: {ex.Message}");
            }

            return url;
        }

        private async Task DownloadAndAddLoraAsync(string url, string folderPath)
        {
            var progressWindow = new DownloadProgressWindow();
            progressWindow.Owner = this;

            try
            {
                // Apply Civitai API key if applicable
                url = ApplyCivitaiApiKey(url);

                // Extract filename from URL (will be improved after getting Content-Disposition header)
                string filename = GetFilenameFromUrl(url);

                // Build the relative path by combining folder and filename
                string relativePath = string.IsNullOrEmpty(folderPath)
                    ? filename
                    : folderPath + "/" + filename;

                // Build the full file path
                string fullPath = System.IO.Path.Combine(_database.LorasBasePath, relativePath + ".safetensors");

                // Ensure directory exists
                var directory = System.IO.Path.GetDirectoryName(fullPath);
                if (directory != null && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                // Check if file already exists
                if (System.IO.File.Exists(fullPath))
                {
                    var result = MessageBox.Show($"File already exists at {relativePath}.safetensors. Overwrite?",
                        "File Exists", MessageBoxButton.YesNo, MessageBoxImage.Question);
                    if (result != MessageBoxResult.Yes)
                    {
                        return;
                    }
                }

                // Show progress window
                progressWindow.Show();
                progressWindow.UpdateStatus("Downloading...");

                // Download the file
                using (var httpClient = new HttpClient())
                {
                    httpClient.Timeout = TimeSpan.FromMinutes(30); // Long timeout for large files

                    // Set Chrome user agent and other headers to avoid being blocked
                    httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0.0.0 Safari/537.36");
                    httpClient.DefaultRequestHeaders.Add("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,image/apng,*/*;q=0.8,application/signed-exchange;v=b3;q=0.7");
                    httpClient.DefaultRequestHeaders.Add("Accept-Language", "en-US,en;q=0.9");
                    httpClient.DefaultRequestHeaders.Add("Accept-Encoding", "gzip, deflate, br");
                    httpClient.DefaultRequestHeaders.Add("DNT", "1");
                    httpClient.DefaultRequestHeaders.Add("Connection", "keep-alive");
                    httpClient.DefaultRequestHeaders.Add("Upgrade-Insecure-Requests", "1");
                    httpClient.DefaultRequestHeaders.Add("Sec-Fetch-Dest", "document");
                    httpClient.DefaultRequestHeaders.Add("Sec-Fetch-Mode", "navigate");
                    httpClient.DefaultRequestHeaders.Add("Sec-Fetch-Site", "none");
                    httpClient.DefaultRequestHeaders.Add("Sec-Fetch-User", "?1");
                    httpClient.DefaultRequestHeaders.Add("sec-ch-ua", "\"Google Chrome\";v=\"131\", \"Chromium\";v=\"131\", \"Not_A Brand\";v=\"24\"");
                    httpClient.DefaultRequestHeaders.Add("sec-ch-ua-mobile", "?0");
                    httpClient.DefaultRequestHeaders.Add("sec-ch-ua-platform", "\"Windows\"");

                    using (var response = await httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead))
                    {
                        response.EnsureSuccessStatusCode();

                        // Try to get filename from Content-Disposition header
                        string? headerFilename = GetFilenameFromContentDisposition(response);
                        if (!string.IsNullOrEmpty(headerFilename))
                        {
                            // Update filename from header
                            filename = System.IO.Path.GetFileNameWithoutExtension(headerFilename);

                            // Rebuild paths with the correct filename
                            relativePath = string.IsNullOrEmpty(folderPath)
                                ? filename
                                : folderPath + "/" + filename;
                            fullPath = System.IO.Path.Combine(_database.LorasBasePath, relativePath + ".safetensors");

                            progressWindow.UpdateStatus($"Downloading: {filename}.safetensors [from server]");
                            System.Diagnostics.Debug.WriteLine($"Using server-provided filename: {filename}.safetensors");

                            // Recheck if file exists with the new filename
                            if (System.IO.File.Exists(fullPath))
                            {
                                progressWindow.Close();
                                var result = MessageBox.Show($"File already exists at {relativePath}.safetensors. Overwrite?",
                                    "File Exists", MessageBoxButton.YesNo, MessageBoxImage.Question);
                                if (result != MessageBoxResult.Yes)
                                {
                                    return;
                                }
                                progressWindow.Show();
                                progressWindow.UpdateStatus($"Downloading: {filename}.safetensors [from server]");
                            }
                        }
                        else
                        {
                            progressWindow.UpdateStatus($"Downloading: {filename}.safetensors [from URL - no server filename]");
                            System.Diagnostics.Debug.WriteLine($"WARNING: No Content-Disposition header found, using URL-based filename: {filename}.safetensors");
                        }

                        var totalBytes = response.Content.Headers.ContentLength ?? 0;

                        using (var contentStream = await response.Content.ReadAsStreamAsync())
                        using (var fileStream = new FileStream(fullPath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true))
                        {
                            var buffer = new byte[8192];
                            long totalBytesRead = 0;
                            int bytesRead;

                            while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                            {
                                await fileStream.WriteAsync(buffer, 0, bytesRead);
                                totalBytesRead += bytesRead;

                                if (totalBytes > 0)
                                {
                                    var progress = (int)((totalBytesRead * 100) / totalBytes);
                                    progressWindow.UpdateProgress(progress, totalBytesRead, totalBytes);
                                }
                            }
                        }
                    }
                }

                progressWindow.UpdateStatus("Calculating file ID...");

                // Calculate file ID
                string fileId = FileIdCalculator.CalculateFileId(fullPath);

                // Create new entry
                var newEntry = new LoraEntry
                {
                    Path = relativePath,
                    FullPath = fullPath,
                    FileId = fileId,
                    FileExists = true,
                    CalculatedFileId = fileId,
                    FileIdValid = true,
                    SourceUrl = url,
                    ActiveTriggers = "",
                    AllTriggers = ""
                };

                // Add to database
                _database.AddEntry(relativePath, newEntry);
                _hasUnsavedChanges = true;
                SaveButton.IsEnabled = true;

                progressWindow.UpdateStatus("Saving database...");

                // Auto-save the database
                await _database.SaveAsync();
                _hasUnsavedChanges = false;
                SaveButton.IsEnabled = false;

                progressWindow.UpdateStatus("Updating file list...");

                // Refresh file list and tree view
                _allFilePaths = _scanner.ScanForLoraFiles();
                BuildTreeView();
                SearchComboBox.ItemsSource = _allFilePaths;

                // Select and expand the new file in the tree
                SelectAndExpandPath(relativePath);

                // Load the new entry in the details panel
                LoadLoraEntry(relativePath);

                progressWindow.Close();

                StatusText.Text = $"Successfully downloaded to: {fullPath}";
                MessageBox.Show($"LoRA file downloaded successfully!\n\nPath: {relativePath}\nFull Path: {fullPath}\nFile ID: {fileId}",
                    "Success", MessageBoxButton.OK, MessageBoxImage.Information);

                // Update git button state after saving
                if (_isGitAvailable && _isGitRepo)
                {
                    await UpdateCommitButtonStateAsync();
                }
            }
            catch (Exception ex)
            {
                progressWindow.Close();
                MessageBox.Show($"Error downloading file: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task CheckGitAvailabilityAsync()
        {
            try
            {
                // Check if git is installed
                var gitVersion = await RunGitCommandAsync("--version", Path.GetDirectoryName(_database.DatabasePath)!);
                _isGitAvailable = !string.IsNullOrEmpty(gitVersion);

                if (_isGitAvailable)
                {
                    // Check if the database file is in a git repository
                    var gitStatus = await RunGitCommandAsync("status --porcelain", Path.GetDirectoryName(_database.DatabasePath)!);
                    _isGitRepo = gitStatus != null; // If git status works, we're in a repo

                    if (_isGitRepo)
                    {
                        // Show the commit button
                        CommitButton.Visibility = Visibility.Visible;
                        await UpdateCommitButtonStateAsync();
                    }
                }
            }
            catch
            {
                _isGitAvailable = false;
                _isGitRepo = false;
            }
        }

        private async Task UpdateCommitButtonStateAsync()
        {
            try
            {
                // Check if there are uncommitted changes to the database file or gallery
                var dbFileName = Path.GetFileName(_database.DatabasePath);
                var dbDirectory = Path.GetDirectoryName(_database.DatabasePath)!;
                var galleryFolderName = Path.GetFileName(_galleryBasePath);

                var status = await RunGitCommandAsync($"status --porcelain \"{dbFileName}\" \"{galleryFolderName}\"", dbDirectory);

                // Enable button if there are changes
                CommitButton.IsEnabled = !string.IsNullOrWhiteSpace(status);
            }
            catch
            {
                CommitButton.IsEnabled = false;
            }
        }

        private async void CommitButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                StatusText.Text = "Committing to git...";
                CommitButton.IsEnabled = false;

                var dbDirectory = Path.GetDirectoryName(_database.DatabasePath)!;
                var dbFileName = Path.GetFileName(_database.DatabasePath);
                var galleryFolderName = Path.GetFileName(_galleryBasePath);

                // Add the database file
                await RunGitCommandAsync($"add \"{dbFileName}\"", dbDirectory);

                // Add all files in the gallery folder
                await RunGitCommandAsync($"add \"{galleryFolderName}\"", dbDirectory);

                // Commit with the specified message
                await RunGitCommandAsync("commit -m \"Updated by Lora Db Editor\"", dbDirectory);

                StatusText.Text = "Successfully committed to git.";
                MessageBox.Show("Database and gallery images committed to git successfully!",
                    "Git Commit", MessageBoxButton.OK, MessageBoxImage.Information);

                // Update button state (should be disabled now since there are no changes)
                await UpdateCommitButtonStateAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error committing to git: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                StatusText.Text = "Error committing to git.";
                await UpdateCommitButtonStateAsync();
            }
        }

        private async Task<string?> RunGitCommandAsync(string arguments, string workingDirectory)
        {
            try
            {
                var processInfo = new ProcessStartInfo
                {
                    FileName = "git",
                    Arguments = arguments,
                    WorkingDirectory = workingDirectory,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var process = new Process { StartInfo = processInfo };
                process.Start();

                var output = await process.StandardOutput.ReadToEndAsync();
                var error = await process.StandardError.ReadToEndAsync();

                await process.WaitForExitAsync();

                if (process.ExitCode != 0 && !string.IsNullOrEmpty(error))
                {
                    throw new Exception(error);
                }

                return output;
            }
            catch
            {
                return null;
            }
        }

        private string GetFilenameFromUrl(string url)
        {
            try
            {
                var uri = new Uri(url);
                var path = uri.AbsolutePath;

                // Get the last segment of the path
                var segments = path.Split('/');
                var lastSegment = segments.LastOrDefault(s => !string.IsNullOrWhiteSpace(s));

                if (!string.IsNullOrEmpty(lastSegment))
                {
                    // Remove extension if it's .safetensors
                    if (lastSegment.EndsWith(".safetensors", StringComparison.OrdinalIgnoreCase))
                    {
                        return System.IO.Path.GetFileNameWithoutExtension(lastSegment);
                    }

                    // Check if it looks like a filename (has an extension)
                    if (lastSegment.Contains('.'))
                    {
                        return System.IO.Path.GetFileNameWithoutExtension(lastSegment);
                    }

                    // Use as-is
                    return lastSegment;
                }
            }
            catch
            {
                // Fall through to default
            }

            // Default fallback
            return "downloaded-lora";
        }

        private string? GetFilenameFromContentDisposition(HttpResponseMessage response)
        {
            try
            {
                string? rawHeaderValue = null;

                // FIRST: Try to get the raw header value from all possible locations
                // Check response.Content.Headers first
                if (response.Content.Headers.TryGetValues("Content-Disposition", out var contentHeaderValues))
                {
                    rawHeaderValue = contentHeaderValues.FirstOrDefault();
                    System.Diagnostics.Debug.WriteLine($"Found Content-Disposition in Content.Headers: {rawHeaderValue}");
                }
                // Then check response.Headers (some servers put it here instead)
                else if (response.Headers.TryGetValues("Content-Disposition", out var responseHeaderValues))
                {
                    rawHeaderValue = responseHeaderValues.FirstOrDefault();
                    System.Diagnostics.Debug.WriteLine($"Found Content-Disposition in Headers: {rawHeaderValue}");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("No Content-Disposition header found in response");
                }

                // If we found a raw header, parse it manually with multiple patterns
                if (!string.IsNullOrEmpty(rawHeaderValue))
                {
                    // Pattern 1: filename*=UTF-8''encoded%20name.ext (RFC 5987)
                    var match = Regex.Match(rawHeaderValue, @"filename\*\s*=\s*(?:UTF-8''|utf-8'')(.+?)(?:;|$)", RegexOptions.IgnoreCase);
                    if (match.Success)
                    {
                        var encoded = match.Groups[1].Value.Trim();
                        try
                        {
                            var decoded = Uri.UnescapeDataString(encoded).Trim('"', ' ', '\'');
                            System.Diagnostics.Debug.WriteLine($"Extracted filename from filename*= (encoded): {decoded}");
                            if (!string.IsNullOrWhiteSpace(decoded))
                                return decoded;
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"Failed to decode UTF-8 filename: {ex.Message}");
                        }
                    }

                    // Pattern 2: filename="name with spaces.ext"
                    match = Regex.Match(rawHeaderValue, @"filename\s*=\s*""([^""]+)""", RegexOptions.IgnoreCase);
                    if (match.Success)
                    {
                        var filename = match.Groups[1].Value.Trim();
                        System.Diagnostics.Debug.WriteLine($"Extracted filename from filename=\"...\": {filename}");
                        if (!string.IsNullOrWhiteSpace(filename))
                            return filename;
                    }

                    // Pattern 3: filename='name with spaces.ext' (single quotes)
                    match = Regex.Match(rawHeaderValue, @"filename\s*=\s*'([^']+)'", RegexOptions.IgnoreCase);
                    if (match.Success)
                    {
                        var filename = match.Groups[1].Value.Trim();
                        System.Diagnostics.Debug.WriteLine($"Extracted filename from filename='...': {filename}");
                        if (!string.IsNullOrWhiteSpace(filename))
                            return filename;
                    }

                    // Pattern 4: filename=name.ext (no quotes)
                    match = Regex.Match(rawHeaderValue, @"filename\s*=\s*([^;""\s]+)", RegexOptions.IgnoreCase);
                    if (match.Success)
                    {
                        var filename = match.Groups[1].Value.Trim();
                        System.Diagnostics.Debug.WriteLine($"Extracted filename from filename=... (no quotes): {filename}");
                        if (!string.IsNullOrWhiteSpace(filename))
                            return filename;
                    }

                    System.Diagnostics.Debug.WriteLine($"Could not extract filename from header: {rawHeaderValue}");
                }

                // SECOND: Try the parsed ContentDisposition properties as fallback
                if (response.Content.Headers.ContentDisposition != null)
                {
                    // Check FileName property
                    if (!string.IsNullOrWhiteSpace(response.Content.Headers.ContentDisposition.FileName))
                    {
                        var filename = response.Content.Headers.ContentDisposition.FileName.Trim('"', ' ', '\'');
                        System.Diagnostics.Debug.WriteLine($"Extracted filename from ContentDisposition.FileName: {filename}");
                        if (!string.IsNullOrWhiteSpace(filename))
                            return filename;
                    }

                    // Check FileNameStar property (RFC 5987)
                    if (!string.IsNullOrWhiteSpace(response.Content.Headers.ContentDisposition.FileNameStar))
                    {
                        var filename = response.Content.Headers.ContentDisposition.FileNameStar.Trim('"', ' ', '\'');
                        System.Diagnostics.Debug.WriteLine($"Extracted filename from ContentDisposition.FileNameStar: {filename}");
                        if (!string.IsNullOrWhiteSpace(filename))
                            return filename;
                    }
                }

                // Log all response headers for debugging
                System.Diagnostics.Debug.WriteLine("=== All Response Headers ===");
                foreach (var header in response.Headers)
                {
                    System.Diagnostics.Debug.WriteLine($"{header.Key}: {string.Join(", ", header.Value)}");
                }
                foreach (var header in response.Content.Headers)
                {
                    System.Diagnostics.Debug.WriteLine($"{header.Key}: {string.Join(", ", header.Value)}");
                }
                System.Diagnostics.Debug.WriteLine("=== End Headers ===");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error parsing Content-Disposition: {ex.Message}");
            }

            return null;
        }
    }
}
