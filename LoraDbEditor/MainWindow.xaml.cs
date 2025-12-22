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
        private bool _isNewEntry = false;
        private bool _isLoadingEntry = false;
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
                bool showWarning = false;
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
                    showWarning = true;
                }
                else if (!entry.FileExists)
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
                    UpdateFileIdButton.Content = "Update File ID";
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
                    UpdateFileIdButton.Content = "Update File ID";
                    showWarning = true;
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
                        ActiveTriggersText.Text = entry.ActiveTriggers.Replace("\\n", Environment.NewLine);
                    }
                    else
                    {
                        ActiveTriggersText.Text = "";
                    }

                    // Convert \n to actual newlines for display
                    if (!string.IsNullOrEmpty(entry.AllTriggers))
                    {
                        AllTriggersText.Text = entry.AllTriggers.Replace("\\n", Environment.NewLine);
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
                        NotesText.Text = entry.Notes.Replace("\\n", Environment.NewLine);
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
            var textWithEncodedNewlines = ActiveTriggersText.Text.Replace(Environment.NewLine, "\\n");
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
            var textWithEncodedNewlines = AllTriggersText.Text.Replace(Environment.NewLine, "\\n");
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
            var textWithEncodedNewlines = NotesText.Text.Replace(Environment.NewLine, "\\n");
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
