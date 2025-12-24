using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using LoraDbEditor.Models;

namespace LoraDbEditor.Services
{
    /// <summary>
    /// Manages drag and drop operations including URL extraction, file validation, and visual feedback
    /// </summary>
    public class DragDropManager
    {
        private readonly TreeViewManager _treeViewManager;
        private TreeViewItem? _dragHoverItem;
        private Border? _dragHoverBorder;
        private Brush? _dragHoverOriginalBackground;

        public DragDropManager(TreeViewManager treeViewManager)
        {
            _treeViewManager = treeViewManager;
        }

        /// <summary>
        /// Determines the appropriate drag effect for a drag operation
        /// </summary>
        /// <param name="treeView">The TreeView control</param>
        /// <param name="e">Drag event args</param>
        /// <param name="targetNode">The node being dragged over (can be null for root)</param>
        /// <param name="draggedNode">The node being dragged (null for external drops)</param>
        /// <returns>The appropriate drag effect</returns>
        public DragDropEffects GetDragEffectForTreeView(TreeView treeView, DragEventArgs e, TreeViewNode? targetNode, TreeViewNode? draggedNode)
        {
            // Check if this is a URL drop (download)
            bool isUrlDrop = e.Data.GetDataPresent(DataFormats.Text) ||
                            e.Data.GetDataPresent(DataFormats.UnicodeText) ||
                            e.Data.GetDataPresent(DataFormats.Html);

            // Check if this is a file move
            bool isFileDrop = e.Data.GetDataPresent("TreeViewNode");

            // Check if this is a local .safetensors file drop
            bool isSafetensorsFileDrop = false;
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                var files = (string[])e.Data.GetData(DataFormats.FileDrop);
                isSafetensorsFileDrop = files != null && files.Length > 0 && IsSafetensorsFile(files[0]);
            }

            if (!isUrlDrop && !isFileDrop && !isSafetensorsFileDrop)
            {
                return DragDropEffects.None;
            }

            // Handle file move
            if (isFileDrop && !isUrlDrop)
            {
                if (draggedNode == null || !draggedNode.IsFile)
                {
                    return DragDropEffects.None;
                }

                if (targetNode == null)
                {
                    // Allow drop to root
                    return DragDropEffects.Move;
                }
                else if (targetNode == draggedNode)
                {
                    // Can't drop on itself
                    return DragDropEffects.None;
                }
                else if (!targetNode.IsFile)
                {
                    // Can drop on folders
                    return DragDropEffects.Move;
                }
                else
                {
                    // Can't drop on other files
                    return DragDropEffects.None;
                }
            }

            // Handle URL drop (download) or .safetensors file drop
            if (isUrlDrop || isSafetensorsFileDrop)
            {
                if (targetNode == null)
                {
                    // Allow drop to root
                    return DragDropEffects.Copy;
                }
                else if (!targetNode.IsFile)
                {
                    // Can drop on folders
                    return DragDropEffects.Copy;
                }
                else
                {
                    // Can't drop on files
                    return DragDropEffects.None;
                }
            }

            return DragDropEffects.None;
        }

        /// <summary>
        /// Extracts a URL from drag and drop data
        /// </summary>
        public string? ExtractUrlFromDragData(IDataObject dataObject)
        {
            string? url = null;

            // Try to get URL from various data formats
            if (dataObject.GetDataPresent(DataFormats.Text))
            {
                url = dataObject.GetData(DataFormats.Text) as string;
            }
            else if (dataObject.GetDataPresent(DataFormats.UnicodeText))
            {
                url = dataObject.GetData(DataFormats.UnicodeText) as string;
            }
            else if (dataObject.GetDataPresent(DataFormats.Html))
            {
                // Extract URL from HTML content
                var html = dataObject.GetData(DataFormats.Html) as string;
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

            return url?.Trim();
        }

        /// <summary>
        /// Checks if a file path points to a .safetensors file
        /// </summary>
        public bool IsSafetensorsFile(string filePath)
        {
            var extension = Path.GetExtension(filePath)?.ToLowerInvariant();
            return extension == ".safetensors";
        }

        /// <summary>
        /// Checks if a file path points to an image file
        /// </summary>
        public bool IsImageFile(string filePath)
        {
            var extension = Path.GetExtension(filePath)?.ToLowerInvariant();
            return extension == ".jpg" || extension == ".jpeg" || extension == ".png" ||
                   extension == ".bmp" || extension == ".gif" || extension == ".webp";
        }

        /// <summary>
        /// Highlights a drag target node in the tree view
        /// </summary>
        public void HighlightDragTarget(TreeView treeView, TreeViewNode targetNode, Point position)
        {
            var treeViewItem = _treeViewManager.GetTreeViewItemAtPoint(treeView, position);

            if (treeViewItem == null)
            {
                return;
            }

            if (treeViewItem.DataContext != targetNode)
            {
                return;
            }

            if (treeViewItem == _dragHoverItem)
            {
                // Already highlighted, no need to update
                return;
            }

            // Clear previous highlight
            ClearDragHighlight();

            // Store the TreeViewItem
            _dragHoverItem = treeViewItem;

            // Try multiple approaches to make the highlight visible

            // Approach 1: Set TreeViewItem background directly with a bright, opaque color
            var highlightColor = (Color)ColorConverter.ConvertFromString("#007ACC")!;
            _dragHoverItem.Background = new SolidColorBrush(Color.FromArgb(180, highlightColor.R, highlightColor.G, highlightColor.B));
            _dragHoverItem.BorderBrush = new SolidColorBrush(highlightColor);
            _dragHoverItem.BorderThickness = new Thickness(2);

            // Approach 2: Also try to find and highlight the Border in the visual tree
            var border = _treeViewManager.FindVisualChild<Border>(_dragHoverItem);
            if (border != null)
            {
                _dragHoverBorder = border;
                _dragHoverOriginalBackground = border.Background;
                border.Background = new SolidColorBrush(Color.FromArgb(180, highlightColor.R, highlightColor.G, highlightColor.B));
            }
        }

        /// <summary>
        /// Clears any drag target highlighting
        /// </summary>
        public void ClearDragHighlight()
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

        /// <summary>
        /// Gets the status message for a drag operation over the tree view
        /// </summary>
        public string GetDragStatusMessage(DragEventArgs e, TreeViewNode? targetNode, TreeViewNode? draggedNode)
        {
            bool isUrlDrop = e.Data.GetDataPresent(DataFormats.Text) ||
                            e.Data.GetDataPresent(DataFormats.UnicodeText) ||
                            e.Data.GetDataPresent(DataFormats.Html);

            bool isFileDrop = e.Data.GetDataPresent("TreeViewNode");

            bool isSafetensorsFileDrop = false;
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                var files = (string[])e.Data.GetData(DataFormats.FileDrop);
                isSafetensorsFileDrop = files != null && files.Length > 0 && IsSafetensorsFile(files[0]);
            }

            // Handle file move
            if (isFileDrop && !isUrlDrop)
            {
                if (targetNode == null)
                {
                    return "DragOver: Root (null target)";
                }
                else if (targetNode == draggedNode)
                {
                    return $"DragOver: Can't drop on self ({targetNode.Name})";
                }
                else if (!targetNode.IsFile)
                {
                    return $"DragOver: Folder target - {targetNode.Name}";
                }
                else
                {
                    return $"DragOver: File target (not allowed) - {targetNode.Name}";
                }
            }

            // Handle URL drop (download) or .safetensors file drop
            if (isUrlDrop || isSafetensorsFileDrop)
            {
                string action = isSafetensorsFileDrop ? "copy file" : "download";

                if (targetNode == null)
                {
                    return $"Drop here to {action} to root folder";
                }
                else if (!targetNode.IsFile)
                {
                    return $"Drop here to {action} to: {targetNode.FullPath}";
                }
                else
                {
                    return $"Cannot {action} to a file, drop on a folder instead";
                }
            }

            return "DragOver: No valid data";
        }

        /// <summary>
        /// Checks if drag data contains a URL
        /// </summary>
        public bool IsUrlDrop(DragEventArgs e)
        {
            return e.Data.GetDataPresent(DataFormats.Text) ||
                   e.Data.GetDataPresent(DataFormats.UnicodeText) ||
                   e.Data.GetDataPresent(DataFormats.Html);
        }

        /// <summary>
        /// Checks if drag data contains a TreeViewNode (internal move)
        /// </summary>
        public bool IsTreeNodeDrop(DragEventArgs e)
        {
            return e.Data.GetDataPresent("TreeViewNode");
        }

        /// <summary>
        /// Checks if drag data contains a .safetensors file
        /// </summary>
        public bool IsSafetensorsFileDrop(DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                var files = (string[])e.Data.GetData(DataFormats.FileDrop);
                return files != null && files.Length > 0 && IsSafetensorsFile(files[0]);
            }
            return false;
        }

        /// <summary>
        /// Gets the first file from a file drop
        /// </summary>
        public string? GetFirstFileFromDrop(DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                var files = (string[])e.Data.GetData(DataFormats.FileDrop);
                if (files != null && files.Length > 0)
                {
                    return files[0];
                }
            }
            return null;
        }
    }
}
