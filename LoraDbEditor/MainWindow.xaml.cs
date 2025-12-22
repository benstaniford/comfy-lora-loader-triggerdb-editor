using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using LoraDbEditor.Models;
using LoraDbEditor.Services;

namespace LoraDbEditor
{
    public partial class MainWindow : Window
    {
        private readonly LoraDatabase _database;
        private readonly FileSystemScanner _scanner;
        private List<string> _allFilePaths = new();
        private ObservableCollection<TreeViewNode> _treeNodes = new();
        private LoraEntry? _currentEntry;
        private bool _hasUnsavedChanges = false;

        public MainWindow()
        {
            InitializeComponent();
            _database = new LoraDatabase();
            _scanner = new FileSystemScanner(_database.LorasBasePath);
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
            var root = new Dictionary<string, TreeViewNode>();

            foreach (var path in _allFilePaths)
            {
                var parts = path.Split('/');
                var currentLevel = root;
                string currentPath = "";

                for (int i = 0; i < parts.Length; i++)
                {
                    var part = parts[i];
                    currentPath = string.IsNullOrEmpty(currentPath) ? part : currentPath + "/" + part;
                    bool isFile = (i == parts.Length - 1);

                    if (!currentLevel.ContainsKey(part))
                    {
                        var node = new TreeViewNode
                        {
                            Name = part,
                            FullPath = currentPath,
                            IsFile = isFile
                        };
                        currentLevel[part] = node;
                    }

                    if (!isFile)
                    {
                        // Convert children to dictionary for next level
                        var nextLevel = new Dictionary<string, TreeViewNode>();
                        foreach (var child in currentLevel[part].Children)
                        {
                            nextLevel[child.Name] = child;
                        }
                        currentLevel = nextLevel;
                    }
                }
            }

            // Convert dictionary to observable collection
            var rootCollection = new ObservableCollection<TreeViewNode>();
            BuildTreeRecursive(root, rootCollection);
            FileTreeView.ItemsSource = rootCollection;
        }

        private void BuildTreeRecursive(Dictionary<string, TreeViewNode> dict, ObservableCollection<TreeViewNode> collection)
        {
            foreach (var kvp in dict.OrderBy(x => x.Value.IsFile).ThenBy(x => x.Key))
            {
                collection.Add(kvp.Value);

                if (!kvp.Value.IsFile && kvp.Value.Children != null)
                {
                    var childDict = new Dictionary<string, TreeViewNode>();
                    foreach (var child in kvp.Value.Children)
                    {
                        childDict[child.Name] = child;
                    }

                    // Recursively build children
                    var childPaths = _allFilePaths.Where(p => p.StartsWith(kvp.Value.FullPath + "/")).ToList();
                    var nextLevelDict = new Dictionary<string, TreeViewNode>();

                    foreach (var childPath in childPaths)
                    {
                        var remainingPath = childPath.Substring(kvp.Value.FullPath.Length + 1);
                        var firstPart = remainingPath.Split('/')[0];

                        if (!nextLevelDict.ContainsKey(firstPart))
                        {
                            var fullChildPath = kvp.Value.FullPath + "/" + firstPart;
                            bool isFile = !childPath.Contains('/') || childPath == fullChildPath;

                            nextLevelDict[firstPart] = new TreeViewNode
                            {
                                Name = firstPart,
                                FullPath = fullChildPath,
                                IsFile = isFile
                            };
                        }
                    }

                    BuildTreeRecursive(nextLevelDict, kvp.Value.Children);
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

        private void LoadLoraEntry(string path)
        {
            try
            {
                // Get or create entry
                var entry = _database.GetEntry(path);

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
                bool showWarning = false;
                UpdateFileIdButton.Visibility = Visibility.Collapsed;

                if (!entry.FileExists)
                {
                    FileIdWarningBorder.Visibility = Visibility.Visible;
                    FileIdWarningBorder.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F44747")!);
                    FileIdWarningText.Text = "WARNING: File not found on disk!";
                    FileIdWarningText.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F44747")!);
                    CurrentFileIdText.Text = entry.FileId ?? "(none)";
                    ExpectedFileIdText.Text = "(file missing)";
                    showWarning = true;
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
                    showWarning = true;
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
                    showWarning = true;
                }
                else
                {
                    FileIdWarningBorder.Visibility = Visibility.Collapsed;
                }

                // Display triggers
                ActiveTriggersText.Text = entry.ActiveTriggers ?? "(none)";

                // Convert \n to actual newlines for display
                if (!string.IsNullOrEmpty(entry.AllTriggers))
                {
                    AllTriggersText.Text = entry.AllTriggers.Replace("\\n", Environment.NewLine);
                }
                else
                {
                    AllTriggersText.Text = "(none)";
                }

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
                _database.UpdateFileId(_currentEntry.Path, _currentEntry.CalculatedFileId);
                _hasUnsavedChanges = true;
                SaveButton.IsEnabled = true;

                // Reload the entry to refresh UI
                LoadLoraEntry(_currentEntry.Path);

                StatusText.Text = $"Updated file ID for {_currentEntry.Path}. Don't forget to save!";
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
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error saving database: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                StatusText.Text = "Error saving database.";
                SaveButton.IsEnabled = true;
            }
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
    }
}
