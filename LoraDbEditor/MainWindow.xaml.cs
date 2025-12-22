using System.Collections.ObjectModel;
using System.IO;
using System.Net.Http;
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

        private void AddImageBox_Drop(object sender, DragEventArgs e)
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

        private async Task DownloadAndAddLoraAsync(string url, string relativePath)
        {
            var progressWindow = new DownloadProgressWindow();
            progressWindow.Owner = this;

            try
            {
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

                    using (var response = await httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead))
                    {
                        response.EnsureSuccessStatusCode();

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

                progressWindow.UpdateStatus("Updating file list...");

                // Refresh file list and tree view
                _allFilePaths = _scanner.ScanForLoraFiles();
                BuildTreeView();
                SearchComboBox.ItemsSource = _allFilePaths;

                // Load the new entry in the details panel
                LoadLoraEntry(relativePath);

                progressWindow.Close();

                StatusText.Text = $"Successfully added {relativePath}. Don't forget to save!";
                MessageBox.Show($"LoRA file downloaded successfully!\n\nPath: {relativePath}\nFile ID: {fileId}",
                    "Success", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                progressWindow.Close();
                MessageBox.Show($"Error downloading file: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
