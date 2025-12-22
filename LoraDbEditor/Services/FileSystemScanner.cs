using System.IO;

namespace LoraDbEditor.Services
{
    public class FileSystemScanner
    {
        private readonly string _basePath;

        public FileSystemScanner(string basePath)
        {
            _basePath = basePath;
        }

        /// <summary>
        /// Scans the base directory for all .safetensors files and returns their relative paths (without extension)
        /// </summary>
        public List<string> ScanForLoraFiles()
        {
            var result = new List<string>();

            if (!Directory.Exists(_basePath))
            {
                return result;
            }

            var files = Directory.GetFiles(_basePath, "*.safetensors", SearchOption.AllDirectories);

            foreach (var file in files)
            {
                // Get relative path from base
                string relativePath = Path.GetRelativePath(_basePath, file);

                // Remove .safetensors extension
                if (relativePath.EndsWith(".safetensors", StringComparison.OrdinalIgnoreCase))
                {
                    relativePath = relativePath.Substring(0, relativePath.Length - ".safetensors".Length);
                }

                // Convert backslashes to forward slashes for consistency with JSON
                relativePath = relativePath.Replace('\\', '/');

                result.Add(relativePath);
            }

            return result.OrderBy(x => x).ToList();
        }

        /// <summary>
        /// Performs fuzzy search on a list of paths
        /// </summary>
        public static List<string> FuzzySearch(List<string> paths, string searchText)
        {
            if (string.IsNullOrWhiteSpace(searchText))
            {
                return paths;
            }

            searchText = searchText.ToLowerInvariant();

            // Score each path based on fuzzy match
            var scored = paths.Select(path =>
            {
                var lowerPath = path.ToLowerInvariant();
                int score = 0;

                // Exact match gets highest score
                if (lowerPath == searchText)
                    score = 10000;
                // Starts with search text
                else if (lowerPath.StartsWith(searchText))
                    score = 5000;
                // Contains search text
                else if (lowerPath.Contains(searchText))
                    score = 2000;
                else
                {
                    // Fuzzy match: all characters of search text appear in order
                    int searchIndex = 0;
                    for (int i = 0; i < lowerPath.Length && searchIndex < searchText.Length; i++)
                    {
                        if (lowerPath[i] == searchText[searchIndex])
                        {
                            searchIndex++;
                            score += 10; // Award points for each matching character
                        }
                    }

                    // If not all characters matched, score is 0
                    if (searchIndex < searchText.Length)
                        score = 0;
                }

                return new { Path = path, Score = score };
            })
            .Where(x => x.Score > 0)
            .OrderByDescending(x => x.Score)
            .Select(x => x.Path)
            .ToList();

            return scored;
        }
    }
}
