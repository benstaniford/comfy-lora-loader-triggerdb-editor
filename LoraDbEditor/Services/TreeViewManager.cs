using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using LoraDbEditor.Models;

namespace LoraDbEditor.Services
{
    /// <summary>
    /// Manages TreeView building, sorting, selection, and navigation operations
    /// </summary>
    public class TreeViewManager
    {
        /// <summary>
        /// Builds a hierarchical tree structure from a flat list of file paths
        /// </summary>
        /// <param name="filePaths">List of file paths (without .safetensors extension)</param>
        /// <param name="lorasBasePath">Base directory path for LoRA files</param>
        /// <returns>Root collection of tree nodes</returns>
        public ObservableCollection<TreeViewNode> BuildTreeView(List<string> filePaths, string lorasBasePath)
        {
            // Build a flat dictionary first with full paths as keys
            var allNodes = new Dictionary<string, TreeViewNode>();

            // First, add all directories from the filesystem
            if (Directory.Exists(lorasBasePath))
            {
                AddAllDirectories(lorasBasePath, "", allNodes);
            }

            // Then, add all files from the scanned paths
            foreach (var path in filePaths)
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

            return rootCollection;
        }

        /// <summary>
        /// Recursively sorts tree nodes: folders first, then by name
        /// </summary>
        public void SortTreeNodes(ObservableCollection<TreeViewNode> nodes)
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

        /// <summary>
        /// Selects and expands a path in the TreeView, scrolling it into view
        /// </summary>
        public void SelectAndExpandPath(TreeView treeView, string path)
        {
            // Walk through the tree to find and select the node
            var parts = path.Split('/');
            var nodes = treeView.ItemsSource as ObservableCollection<TreeViewNode>;

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
                treeView.Dispatcher.BeginInvoke(new Action(() =>
                {
                    SelectTreeViewNode(treeView, currentNode);
                }), System.Windows.Threading.DispatcherPriority.Loaded);
            }
        }

        /// <summary>
        /// Gets the TreeViewNode at a specific point in the TreeView
        /// </summary>
        public TreeViewNode? GetTreeViewNodeAtPoint(TreeView treeView, Point point)
        {
            var hitTestResult = VisualTreeHelper.HitTest(treeView, point);
            if (hitTestResult == null)
                return null;

            var element = hitTestResult.VisualHit;
            while (element != null && element != treeView)
            {
                if (element is FrameworkElement fe && fe.DataContext is TreeViewNode node)
                {
                    return node;
                }
                element = VisualTreeHelper.GetParent(element);
            }

            return null;
        }

        /// <summary>
        /// Gets the TreeViewItem container at a specific point in the TreeView
        /// </summary>
        public TreeViewItem? GetTreeViewItemAtPoint(TreeView treeView, Point point)
        {
            var hitTestResult = VisualTreeHelper.HitTest(treeView, point);
            if (hitTestResult == null)
                return null;

            var element = hitTestResult.VisualHit;
            while (element != null && element != treeView)
            {
                if (element is TreeViewItem tvi)
                {
                    return tvi;
                }
                element = VisualTreeHelper.GetParent(element);
            }

            return null;
        }

        /// <summary>
        /// Finds a visual child of a specific type in the visual tree
        /// </summary>
        public T? FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
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
    }
}
