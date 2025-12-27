using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
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
        // Services
        private readonly LoraDatabase _database;
        private readonly FileSystemScanner _scanner;
        private readonly TreeViewManager _treeViewManager;
        private readonly DragDropManager _dragDropManager;
        private readonly FileOperationsService _fileOperations;
        private readonly GalleryManager _galleryManager;
        private readonly DownloadService _downloadService;
        private readonly GitService _gitService;
        private readonly DialogService _dialogService;

        // Paths
        private readonly string _galleryBasePath;

        // State
        private List<string> _allFilePaths = new();
        private LoraEntry? _currentEntry;
        private bool _isNewEntry = false;
        private bool _isLoadingEntry = false;
        private bool _hasUnsavedChanges = false;
        private bool _isGitAvailable = false;
        private bool _isGitRepo = false;
        private Point _dragStartPoint;
        private TreeViewNode? _draggedNode;

        public MainWindow()
        {
            InitializeComponent();

            // Initialize services
            _database = new LoraDatabase();
            _scanner = new FileSystemScanner(_database.LorasBasePath);
            _treeViewManager = new TreeViewManager();
            _dragDropManager = new DragDropManager(_treeViewManager);
            _fileOperations = new FileOperationsService();
            _galleryManager = new GalleryManager();
            _downloadService = new DownloadService();
            _gitService = new GitService();
            _dialogService = new DialogService();

            // Set up gallery base path
            string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            _galleryBasePath = Path.Combine(userProfile, "Documents", "ComfyUI", "user", "default", "user-db", "lora-triggers-pictures");

            // Ensure gallery directory exists
            if (!Directory.Exists(_galleryBasePath))
            {
                Directory.CreateDirectory(_galleryBasePath);
            }
        }

        private void UpdateStatus(string message)
        {
            StatusText.Text = message;
        }

        #region Window Lifecycle

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
                RebuildTreeView();

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
                UpdateStatus($"Error loading: {ex.Message}");
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

        #endregion

        #region TreeView Management

        private void RebuildTreeView()
        {
            var treeNodes = _treeViewManager.BuildTreeView(_allFilePaths, _database.LorasBasePath);
            FileTreeView.ItemsSource = treeNodes;
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

        #endregion

        #region Search

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

        #endregion

        #region Drag and Drop

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
            var draggedNode = e.Data.GetData("TreeViewNode") as TreeViewNode;
            var targetNode = _treeViewManager.GetTreeViewNodeAtPoint(FileTreeView, e.GetPosition(FileTreeView));

            // Get drag effect
            e.Effects = _dragDropManager.GetDragEffectForTreeView(FileTreeView, e, targetNode, draggedNode);

            // Update status
            StatusText.Text = _dragDropManager.GetDragStatusMessage(e, targetNode, draggedNode);

            // Highlight target if appropriate
            if (e.Effects != DragDropEffects.None && targetNode != null && !targetNode.IsFile)
            {
                _dragDropManager.HighlightDragTarget(FileTreeView, targetNode, e.GetPosition(FileTreeView));
            }
            else
            {
                _dragDropManager.ClearDragHighlight();
            }

            e.Handled = true;
        }

        private void FileTreeView_DragLeave(object sender, DragEventArgs e)
        {
            _dragDropManager.ClearDragHighlight();
        }

        private async void FileTreeView_Drop(object sender, DragEventArgs e)
        {
            _dragDropManager.ClearDragHighlight();

            // Get target node
            var targetNode = _treeViewManager.GetTreeViewNodeAtPoint(FileTreeView, e.GetPosition(FileTreeView));
            string targetDirectory = (targetNode != null && !targetNode.IsFile) ? targetNode.FullPath : "";

            // Handle file move (internal drag)
            if (_dragDropManager.IsTreeNodeDrop(e))
            {
                var draggedNode = e.Data.GetData("TreeViewNode") as TreeViewNode;
                if (draggedNode != null && draggedNode.IsFile)
                {
                    await MoveLoraToFolderAsync(draggedNode.FullPath, targetDirectory);
                }
                e.Handled = true;
                return;
            }

            // Handle .safetensors file drop
            if (_dragDropManager.IsSafetensorsFileDrop(e))
            {
                var sourceFile = _dragDropManager.GetFirstFileFromDrop(e);
                if (sourceFile != null)
                {
                    await CopyAndAddLoraAsync(sourceFile, targetDirectory);
                }
                e.Handled = true;
                return;
            }

            // Handle URL drop (download)
            if (_dragDropManager.IsUrlDrop(e))
            {
                var url = _dragDropManager.ExtractUrlFromDragData(e.Data);
                if (!string.IsNullOrWhiteSpace(url) && Uri.TryCreate(url, UriKind.Absolute, out _))
                {
                    await DownloadAndAddLoraAsync(url, targetDirectory);
                }
                else
                {
                    UpdateStatus("Invalid URL format.");
                }
                e.Handled = true;
                return;
            }

            e.Handled = true;
        }

        private void AddLoraZone_PreviewDragOver(object sender, DragEventArgs e)
        {
            // Accept URLs or .safetensors files
            if (_dragDropManager.IsUrlDrop(e) || _dragDropManager.IsSafetensorsFileDrop(e))
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
                // Handle .safetensors file drop
                if (_dragDropManager.IsSafetensorsFileDrop(e))
                {
                    var sourceFile = _dragDropManager.GetFirstFileFromDrop(e);
                    if (sourceFile != null)
                    {
                        var targetFolder = _dialogService.ShowFolderSelectionDialog(this, _allFilePaths);
                        if (targetFolder != null)
                        {
                            await CopyAndAddLoraAsync(sourceFile, targetFolder);
                        }
                    }
                    e.Handled = true;
                    return;
                }

                // Handle URL drop
                var url = _dragDropManager.ExtractUrlFromDragData(e.Data);
                if (string.IsNullOrWhiteSpace(url) || !Uri.TryCreate(url, UriKind.Absolute, out _))
                {
                    UpdateStatus("No valid URL or file found in dropped data.");
                    return;
                }

                var targetPath = _dialogService.ShowFolderSelectionDialog(this, _allFilePaths);
                if (targetPath != null)
                {
                    await DownloadAndAddLoraAsync(url, targetPath);
                }

                e.Handled = true;
            }
            catch (Exception ex)
            {
                UpdateStatus($"Error processing dropped data: {ex.Message}");
            }
        }

        #endregion

        #region File Operations

        private async void RenameMenuItem_Click(object sender, RoutedEventArgs e)
        {
            await RenameSelectedLora();
        }

        private async void DeleteMenuItem_Click(object sender, RoutedEventArgs e)
        {
            await DeleteSelectedLora();
        }

        private void NewFolderMenuItem_Click(object sender, RoutedEventArgs e)
        {
            CreateNewFolder();
        }

        private async Task RenameSelectedLora()
        {
            if (FileTreeView.SelectedItem is not TreeViewNode node)
            {
                UpdateStatus("Please select a LoRA file or folder to rename.");
                return;
            }

            if (node.IsFile)
            {
                await RenameSingleFileAsync(node.FullPath);
            }
            else
            {
                await RenameFolderAsync(node.FullPath);
            }
        }

        private async Task RenameSingleFileAsync(string oldPath)
        {
            var currentName = Path.GetFileName(oldPath);
            var newName = _dialogService.ShowRenameSingleFileDialog(this, currentName);

            if (newName == null)
                return;

            var result = await _fileOperations.RenameSingleFileAsync(oldPath, newName, _database, _galleryBasePath);

            if (result.Success)
            {
                await RefreshAfterFileOperationAsync(result.NewPath!);
                UpdateStatus($"Successfully renamed from {oldPath} to {result.NewPath}.");
            }
            else
            {
                UpdateStatus(result.ErrorMessage!);
            }
        }

        private async Task RenameFolderAsync(string oldFolderPath)
        {
            var currentName = Path.GetFileName(oldFolderPath);
            var newName = _dialogService.ShowRenameFolderDialog(this, currentName);

            if (newName == null)
                return;

            var result = await _fileOperations.RenameFolderAsync(oldFolderPath, newName, _database, _galleryBasePath, _allFilePaths);

            if (result.Success)
            {
                await RefreshAfterFileOperationAsync(result.NewPath!);
                UpdateStatus($"Successfully renamed folder from {oldFolderPath} to {result.NewPath} (affected {result.AffectedFileCount} file(s)).");
            }
            else
            {
                UpdateStatus(result.ErrorMessage!);
            }
        }

        private async Task DeleteSelectedLora()
        {
            if (FileTreeView.SelectedItem is not TreeViewNode node)
            {
                UpdateStatus("Please select a LoRA file or folder to delete.");
                return;
            }

            if (node.IsFile)
            {
                await DeleteSingleFileAsync(node.FullPath);
            }
            else
            {
                await DeleteFolderAsync(node.FullPath);
            }
        }

        private async Task DeleteSingleFileAsync(string loraPath)
        {
            if (!_dialogService.ShowConfirmDialog(
                this,
                $"Are you sure you want to delete this LoRA?\n\n{loraPath}\n\nThis will delete:\n- The .safetensors file\n- The database entry (if it exists)\n- All associated gallery images\n\nThis action cannot be undone!",
                "Confirm Delete"))
            {
                return;
            }

            // Capture parent path before deletion to maintain tree navigation
            string? parentPath = null;
            int lastSlashIndex = loraPath.LastIndexOf('/');
            if (lastSlashIndex > 0)
            {
                parentPath = loraPath.Substring(0, lastSlashIndex);
            }

            var result = await _fileOperations.DeleteSingleFileAsync(loraPath, _database, _galleryBasePath);

            if (result.Success)
            {
                // Clear the details panel if this was the currently loaded entry
                if (_currentEntry?.Path == loraPath)
                {
                    _currentEntry = null;
                    DetailsPanel.Visibility = Visibility.Collapsed;
                }

                await RefreshAfterFileOperationAsync(parentPath);
                UpdateStatus($"Successfully deleted {loraPath}.");
            }
            else
            {
                UpdateStatus(result.ErrorMessage!);
            }
        }

        private async Task DeleteFolderAsync(string folderPath)
        {
            var filesInFolder = _allFilePaths
                .Where(path => path.StartsWith(folderPath + "/") || path == folderPath)
                .ToList();

            string message;
            if (filesInFolder.Count == 0)
            {
                message = $"Are you sure you want to delete this empty folder?\n\n{folderPath}\n\nThis action cannot be undone!";
            }
            else
            {
                message = $"Are you sure you want to delete this folder and all its contents?\n\n{folderPath}\n\nThis will delete:\n- {filesInFolder.Count} LoRA file(s)\n- All database entries\n- All associated gallery images\n- The folder itself\n\nThis action cannot be undone!";
            }

            if (!_dialogService.ShowConfirmDialog(this, message, "Confirm Delete Folder"))
            {
                return;
            }

            // Capture parent path before deletion to maintain tree navigation
            string? parentPath = null;
            int lastSlashIndex = folderPath.LastIndexOf('/');
            if (lastSlashIndex > 0)
            {
                parentPath = folderPath.Substring(0, lastSlashIndex);
            }

            var result = await _fileOperations.DeleteFolderAsync(folderPath, _database, _galleryBasePath, _allFilePaths);

            if (result.Success)
            {
                // Clear the details panel if current entry was in this folder
                if (_currentEntry != null && filesInFolder.Contains(_currentEntry.Path))
                {
                    _currentEntry = null;
                    DetailsPanel.Visibility = Visibility.Collapsed;
                }

                await RefreshAfterFileOperationAsync(parentPath);
                UpdateStatus($"Successfully deleted folder {folderPath} and {result.AffectedFileCount} file(s).");
            }
            else
            {
                UpdateStatus(result.ErrorMessage!);
            }
        }

        private async Task MoveLoraToFolderAsync(string sourcePath, string targetDirectory)
        {
            var result = await _fileOperations.MoveLoraToFolderAsync(sourcePath, targetDirectory, _database, _galleryBasePath);

            if (result.Success)
            {
                await RefreshAfterFileOperationAsync(result.NewPath!);
                UpdateStatus($"Successfully moved {sourcePath} to {result.NewPath}.");
            }
            else
            {
                UpdateStatus(result.ErrorMessage!);
            }
        }

        private void CreateNewFolder()
        {
            // Determine parent directory based on selection
            string parentDirectory = "";
            if (FileTreeView.SelectedItem is TreeViewNode node)
            {
                if (node.IsFile)
                {
                    var parentPath = Path.GetDirectoryName(node.FullPath)?.Replace("\\", "/");
                    parentDirectory = parentPath ?? "";
                }
                else
                {
                    parentDirectory = node.FullPath;
                }
            }

            var folderName = _dialogService.ShowCreateFolderDialog(this, parentDirectory);
            if (folderName == null)
                return;

            var result = _fileOperations.CreateFolder(parentDirectory, folderName, _database.LorasBasePath);

            if (result.Success)
            {
                // Refresh file list and tree view
                _allFilePaths = _scanner.ScanForLoraFiles();
                RebuildTreeView();
                SearchComboBox.ItemsSource = _allFilePaths;

                // Try to select the new folder in the tree
                _treeViewManager.SelectAndExpandPath(FileTreeView, result.NewPath!);

                UpdateStatus($"Successfully created folder: {result.NewPath}");
            }
            else
            {
                UpdateStatus(result.ErrorMessage!);
            }
        }

        private async Task RefreshAfterFileOperationAsync(string? pathToSelect)
        {
            _hasUnsavedChanges = false;
            SaveButton.IsEnabled = false;

            // Update git button state
            if (_isGitAvailable && _isGitRepo)
            {
                await UpdateCommitButtonStateAsync();
            }

            // Refresh file list and tree view
            _allFilePaths = _scanner.ScanForLoraFiles();
            RebuildTreeView();
            SearchComboBox.ItemsSource = _allFilePaths;

            // Select the path if provided
            if (pathToSelect != null)
            {
                _treeViewManager.SelectAndExpandPath(FileTreeView, pathToSelect);

                // Only load the entry if it's a file (exists in _allFilePaths), not a folder
                if (_allFilePaths.Contains(pathToSelect))
                {
                    LoadLoraEntry(pathToSelect);
                }
            }
        }

        #endregion

        #region Entry Loading and UI Binding

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
                        FullPath = Path.Combine(_database.LorasBasePath, path + ".safetensors"),
                        FileExists = File.Exists(Path.Combine(_database.LorasBasePath, path + ".safetensors"))
                    };

                    if (entry.FileExists)
                    {
                        entry.CalculatedFileId = FileIdCalculator.CalculateFileId(entry.FullPath);
                    }
                }
                else
                {
                    // Entry exists in database - ensure runtime properties are populated
                    entry.FileExists = File.Exists(entry.FullPath);

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
                UpdateFileIdDisplay();

                // Display all fields (suppress TextChanged events while loading)
                _isLoadingEntry = true;
                try
                {
                    DescriptionText.Text = entry.Description ?? "";
                    ActiveTriggersText.Text = !string.IsNullOrEmpty(entry.ActiveTriggers)
                        ? entry.ActiveTriggers.Replace("\n", Environment.NewLine)
                        : "";
                    AllTriggersText.Text = !string.IsNullOrEmpty(entry.AllTriggers)
                        ? entry.AllTriggers.Replace("\n", Environment.NewLine)
                        : "";
                    SourceUrlText.Text = entry.SourceUrl ?? "";
                    SuggestedStrengthText.Text = entry.SuggestedStrength ?? "";
                    NotesText.Text = !string.IsNullOrEmpty(entry.Notes)
                        ? entry.Notes.Replace("\n", Environment.NewLine)
                        : "";
                }
                finally
                {
                    _isLoadingEntry = false;
                }

                // Update the source URL link visibility
                UpdateSourceUrlLink();

                // Load gallery images
                LoadGallery();

                StatusText.Text = $"Loaded: {path}";
            }
            catch (Exception ex)
            {
                UpdateStatus($"Error loading entry: {ex.Message}");
            }
        }

        private void UpdateFileIdDisplay()
        {
            if (_currentEntry == null)
                return;

            // Display File ID
            if (!string.IsNullOrEmpty(_currentEntry.FileId))
            {
                FileIdText.Text = _currentEntry.FileId;
            }
            else
            {
                FileIdText.Text = "(not set)";
            }

            // Validate File ID
            UpdateFileIdButton.Visibility = Visibility.Collapsed;

            if (_isNewEntry && _currentEntry.FileExists)
            {
                // New entry - file exists but not in database
                FileIdWarningBorder.Visibility = Visibility.Visible;
                FileIdWarningBorder.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFA500")!);
                FileIdWarningText.Text = "WARNING: File exists but no database entry found!";
                FileIdWarningText.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFA500")!);
                CurrentFileIdText.Text = "(no entry)";
                ExpectedFileIdText.Text = _currentEntry.CalculatedFileId ?? "(calculating...)";
                UpdateFileIdButton.Visibility = Visibility.Visible;
                UpdateFileIdButton.Content = "Create new record";
            }
            else if (!_currentEntry.FileExists)
            {
                FileIdWarningBorder.Visibility = Visibility.Visible;
                FileIdWarningBorder.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F44747")!);
                FileIdWarningText.Text = "WARNING: File not found on disk!";
                FileIdWarningText.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F44747")!);
                CurrentFileIdText.Text = _currentEntry.FileId ?? "(none)";
                ExpectedFileIdText.Text = "(file missing)";
            }
            else if (string.IsNullOrEmpty(_currentEntry.FileId) || _currentEntry.FileId == "unknown")
            {
                FileIdWarningBorder.Visibility = Visibility.Visible;
                FileIdWarningBorder.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFA500")!);
                FileIdWarningText.Text = "WARNING: File ID is missing or unknown!";
                FileIdWarningText.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFA500")!);
                CurrentFileIdText.Text = _currentEntry.FileId ?? "(none)";
                ExpectedFileIdText.Text = _currentEntry.CalculatedFileId ?? "(calculating...)";
                UpdateFileIdButton.Visibility = Visibility.Visible;
                UpdateFileIdButton.Content = "Update File ID";
            }
            else if (!_currentEntry.FileIdValid)
            {
                FileIdWarningBorder.Visibility = Visibility.Visible;
                FileIdWarningBorder.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F44747")!);
                FileIdWarningText.Text = "WARNING: File ID mismatch!";
                FileIdWarningText.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F44747")!);
                CurrentFileIdText.Text = _currentEntry.FileId;
                ExpectedFileIdText.Text = _currentEntry.CalculatedFileId ?? "(error calculating)";
                UpdateFileIdButton.Visibility = Visibility.Visible;
                UpdateFileIdButton.Content = "Update File ID";
            }
            else
            {
                FileIdWarningBorder.Visibility = Visibility.Collapsed;
            }
        }

        private void UpdateFileIdButton_Click(object sender, RoutedEventArgs e)
        {
            if (_currentEntry == null || string.IsNullOrEmpty(_currentEntry.CalculatedFileId))
            {
                UpdateStatus("Cannot update file ID.");
                return;
            }

            try
            {
                if (_isNewEntry)
                {
                    // Set the file ID and add to database if not already added
                    _currentEntry.FileId = _currentEntry.CalculatedFileId;

                    if (_database.GetEntry(_currentEntry.Path) == null)
                    {
                        _database.AddEntry(_currentEntry.Path, _currentEntry);
                    }
                    else
                    {
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
                UpdateStatus($"Error updating file ID: {ex.Message}");
            }
        }

        #endregion

        #region Text Field Changes

        private void DescriptionText_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isLoadingEntry || _currentEntry == null)
                return;

            _currentEntry.Description = string.IsNullOrWhiteSpace(DescriptionText.Text) ? null : DescriptionText.Text;
            MarkAsModified();
        }

        private void ActiveTriggersText_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isLoadingEntry || _currentEntry == null)
                return;

            var textWithEncodedNewlines = ActiveTriggersText.Text.Replace(Environment.NewLine, "\n");
            _currentEntry.ActiveTriggers = textWithEncodedNewlines;
            MarkAsModified();
        }

        private void AllTriggersText_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isLoadingEntry || _currentEntry == null)
                return;

            var textWithEncodedNewlines = AllTriggersText.Text.Replace(Environment.NewLine, "\n");
            _currentEntry.AllTriggers = textWithEncodedNewlines;
            MarkAsModified();
        }

        private void SourceUrlText_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isLoadingEntry || _currentEntry == null)
                return;

            _currentEntry.SourceUrl = string.IsNullOrWhiteSpace(SourceUrlText.Text) ? null : SourceUrlText.Text;
            UpdateSourceUrlLink();
            MarkAsModified();
        }

        private void SuggestedStrengthText_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isLoadingEntry || _currentEntry == null)
                return;

            _currentEntry.SuggestedStrength = string.IsNullOrWhiteSpace(SuggestedStrengthText.Text) ? null : SuggestedStrengthText.Text;
            MarkAsModified();
        }

        private void NotesText_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isLoadingEntry || _currentEntry == null)
                return;

            var textWithEncodedNewlines = NotesText.Text.Replace(Environment.NewLine, "\n");
            _currentEntry.Notes = string.IsNullOrWhiteSpace(textWithEncodedNewlines) ? null : textWithEncodedNewlines;
            MarkAsModified();
        }

        private void MarkAsModified()
        {
            // If this is a new entry, add it to the database
            if (_isNewEntry && _currentEntry != null && _currentEntry.FileExists)
            {
                _database.AddEntry(_currentEntry.Path, _currentEntry);
                _isNewEntry = false;
            }

            _hasUnsavedChanges = true;
            SaveButton.IsEnabled = true;
            StatusText.Text = $"Modified: {_currentEntry?.Path}. Don't forget to save!";
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
                var psi = new ProcessStartInfo
                {
                    FileName = e.Uri.AbsoluteUri,
                    UseShellExecute = true
                };
                Process.Start(psi);
                e.Handled = true;
            }
            catch (Exception ex)
            {
                UpdateStatus($"Error opening URL: {ex.Message}");
            }
        }

        private void SourceUrlText_PreviewDragOver(object sender, DragEventArgs e)
        {
            if (_dragDropManager.IsUrlDrop(e))
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
                var url = _dragDropManager.ExtractUrlFromDragData(e.Data);
                if (!string.IsNullOrWhiteSpace(url))
                {
                    SourceUrlText.Text = url;
                }
                e.Handled = true;
            }
            catch (Exception ex)
            {
                UpdateStatus($"Error processing dropped URL: {ex.Message}");
            }
        }

        #endregion

        #region Gallery

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

            var images = _galleryManager.LoadGalleryImages(_currentEntry, _galleryBasePath);

            // Load each image
            foreach (var galleryImage in images)
            {
                if (galleryImage.Exists)
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
                            Tag = galleryImage.FullPath
                        };

                        var image = new System.Windows.Controls.Image
                        {
                            Source = new System.Windows.Media.Imaging.BitmapImage(new Uri(galleryImage.FullPath)),
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
                        Debug.WriteLine($"Failed to load image {galleryImage.FullPath}: {ex.Message}");
                    }
                }
            }
        }

        private void GalleryImage_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is Border border && border.Tag is string imagePath)
            {
                try
                {
                    var psi = new ProcessStartInfo
                    {
                        FileName = imagePath,
                        UseShellExecute = true
                    };
                    Process.Start(psi);
                }
                catch (Exception ex)
                {
                    UpdateStatus($"Error opening image: {ex.Message}");
                }
            }
        }

        private void AddImageBox_DragOver(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                var files = (string[])e.Data.GetData(DataFormats.FileDrop);
                if (files != null && files.Length > 0 && _galleryManager.IsImageFile(files[0]))
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
                    if (files != null && files.Length > 0 && _galleryManager.IsImageFile(files[0]))
                    {
                        var sourceFile = files[0];

                        // Add image to gallery
                        _galleryManager.AddImageToGallery(sourceFile, _currentEntry, _galleryBasePath);

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

                        // Update git commit button state
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
                UpdateStatus($"Error adding image: {ex.Message}");
            }
        }

        #endregion

        #region Download and Copy

        private async Task CopyAndAddLoraAsync(string sourceFile, string targetFolder)
        {
            try
            {
                StatusText.Text = "Copying file...";

                // Check if file already exists
                var filename = Path.GetFileNameWithoutExtension(sourceFile);
                string relativePath = string.IsNullOrEmpty(targetFolder) ? filename : targetFolder + "/" + filename;
                string fullPath = Path.Combine(_database.LorasBasePath, relativePath + ".safetensors");

                if (File.Exists(fullPath))
                {
                    var overwrite = _dialogService.ShowConfirmDialog(
                        this,
                        $"File already exists at {relativePath}.safetensors. Overwrite?",
                        "File Exists");

                    if (!overwrite)
                    {
                        StatusText.Text = "Copy cancelled.";
                        return;
                    }
                }

                // Copy the file
                var result = await _downloadService.CopyLoraAsync(sourceFile, targetFolder, _database.LorasBasePath);

                if (result.Success)
                {
                    // Create new entry
                    var newEntry = new LoraEntry
                    {
                        Path = result.RelativePath,
                        FullPath = result.FullPath,
                        FileId = result.FileId,
                        FileExists = true,
                        CalculatedFileId = result.FileId,
                        FileIdValid = true,
                        ActiveTriggers = "",
                        AllTriggers = ""
                    };

                    _database.AddEntry(result.RelativePath, newEntry);
                    await _database.SaveAsync();
                    await RefreshAfterFileOperationAsync(result.RelativePath);

                    UpdateStatus($"LoRA file copied successfully to {result.RelativePath} (File ID: {result.FileId})");
                }
                else
                {
                    UpdateStatus(result.ErrorMessage);
                }
            }
            catch (Exception ex)
            {
                UpdateStatus($"Error copying file: {ex.Message}");
            }
        }

        private async Task DownloadAndAddLoraAsync(string url, string folderPath)
        {
            var progressWindow = new DownloadProgressWindow();
            progressWindow.Owner = this;

            try
            {
                progressWindow.Show();

                var progress = new Progress<DownloadProgress>(p =>
                {
                    progressWindow.UpdateProgress(p.Percentage, p.BytesDownloaded, p.TotalBytes);
                    progressWindow.UpdateStatus(p.Status);
                });

                var result = await _downloadService.DownloadLoraAsync(url, folderPath, _database.LorasBasePath, progress);

                if (result.Success)
                {
                    // Check if file already exists (with updated filename from server)
                    if (File.Exists(result.FullPath))
                    {
                        var entry = _database.GetEntry(result.RelativePath);
                        if (entry != null)
                        {
                            progressWindow.Close();
                            var overwrite = _dialogService.ShowConfirmDialog(
                                this,
                                $"File already exists at {result.RelativePath}.safetensors. Overwrite?",
                                "File Exists");

                            if (!overwrite)
                            {
                                // Delete the downloaded file
                                File.Delete(result.FullPath);
                                return;
                            }
                            progressWindow.Show();
                        }
                    }

                    // Create new entry
                    var newEntry = new LoraEntry
                    {
                        Path = result.RelativePath,
                        FullPath = result.FullPath,
                        FileId = result.FileId,
                        FileExists = true,
                        CalculatedFileId = result.FileId,
                        FileIdValid = true,
                        ActiveTriggers = "",
                        AllTriggers = ""
                    };

                    _database.AddEntry(result.RelativePath, newEntry);
                    await _database.SaveAsync();

                    progressWindow.Close();

                    await RefreshAfterFileOperationAsync(result.RelativePath);
                    UpdateStatus($"LoRA file downloaded successfully to {result.RelativePath} (File ID: {result.FileId})");
                }
                else
                {
                    progressWindow.Close();
                    UpdateStatus(result.ErrorMessage);
                }
            }
            catch (Exception ex)
            {
                progressWindow.Close();
                UpdateStatus($"Error downloading file: {ex.Message}");
            }
        }

        #endregion

        #region Save and Git

        private async void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                StatusText.Text = "Saving database...";
                StatusText.Foreground = (SolidColorBrush)FindResource("TextBrush");
                SaveButton.IsEnabled = false;

                await _database.SaveAsync();

                _hasUnsavedChanges = false;
                StatusText.Text = "Database saved successfully.";
                StatusText.Foreground = (SolidColorBrush)FindResource("SuccessBrush");

                // Check if there are git changes after saving
                if (_isGitAvailable && _isGitRepo)
                {
                    await UpdateCommitButtonStateAsync();
                }
            }
            catch (Exception ex)
            {
                StatusText.Text = $"Error saving database: {ex.Message}";
                StatusText.Foreground = (SolidColorBrush)FindResource("ErrorBrush");
                SaveButton.IsEnabled = true;
            }
        }

        private async Task CheckGitAvailabilityAsync()
        {
            try
            {
                _isGitAvailable = await _gitService.IsGitAvailableAsync();

                if (_isGitAvailable)
                {
                    var dbDirectory = Path.GetDirectoryName(_database.DatabasePath)!;
                    _isGitRepo = await _gitService.IsGitRepositoryAsync(dbDirectory);

                    if (_isGitRepo)
                    {
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
                var dbFileName = Path.GetFileName(_database.DatabasePath);
                var dbDirectory = Path.GetDirectoryName(_database.DatabasePath)!;
                var galleryFolderName = Path.GetFileName(_galleryBasePath);

                bool hasChanges = await _gitService.HasUncommittedChangesAsync(dbDirectory, dbFileName, galleryFolderName);
                CommitButton.IsEnabled = hasChanges;
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
                StatusText.Foreground = (SolidColorBrush)FindResource("TextBrush");
                CommitButton.IsEnabled = false;

                var dbDirectory = Path.GetDirectoryName(_database.DatabasePath)!;
                var dbFileName = Path.GetFileName(_database.DatabasePath);
                var galleryFolderName = Path.GetFileName(_galleryBasePath);

                bool success = await _gitService.CommitChangesAsync(
                    dbDirectory,
                    "Updated by Lora Db Editor",
                    dbFileName,
                    galleryFolderName);

                if (success)
                {
                    StatusText.Text = "Database and gallery images committed to git successfully.";
                    StatusText.Foreground = (SolidColorBrush)FindResource("SuccessBrush");
                }
                else
                {
                    StatusText.Text = "Error committing to git.";
                    StatusText.Foreground = (SolidColorBrush)FindResource("ErrorBrush");
                }

                await UpdateCommitButtonStateAsync();
            }
            catch (Exception ex)
            {
                StatusText.Text = $"Error committing to git: {ex.Message}";
                StatusText.Foreground = (SolidColorBrush)FindResource("ErrorBrush");
                await UpdateCommitButtonStateAsync();
            }
        }

        #endregion

        #region Settings

        private async void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            var settingsDialog = new SettingsDialog();
            settingsDialog.Owner = this;

            if (settingsDialog.ShowDialog() == true)
            {
                if (settingsDialog.PathsChanged)
                {
                    await ReloadAfterPathChange();
                }
            }
        }

        private async Task ReloadAfterPathChange()
        {
            try
            {
                var result = MessageBox.Show(
                    "The application needs to restart to apply the new paths. Any unsaved changes will be lost. Restart now?",
                    "Restart Required",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    Process.Start(Process.GetCurrentProcess().MainModule!.FileName!);
                    Application.Current.Shutdown();
                }
            }
            catch (Exception ex)
            {
                UpdateStatus($"Error restarting application: {ex.Message}");
            }
        }

        #endregion
    }
}
