using System.Collections.ObjectModel;

namespace LoraDbEditor.Models
{
    public class TreeViewNode
    {
        public string Name { get; set; } = string.Empty;
        public string FullPath { get; set; } = string.Empty;
        public bool IsFile { get; set; }
        public ObservableCollection<TreeViewNode> Children { get; set; } = new();
    }
}
