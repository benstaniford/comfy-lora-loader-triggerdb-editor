using System.IO;
using Newtonsoft.Json;
using LoraDbEditor.Models;

namespace LoraDbEditor.Services
{
    public class LoraDatabase
    {
        private readonly string _databasePath;
        private readonly string _lorasBasePath;
        private Dictionary<string, LoraEntry> _entries = new();

        public LoraDatabase()
        {
            string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            _databasePath = Path.Combine(userProfile, "Documents", "ComfyUI", "user", "default", "user-db", "lora-triggers.json");
            _lorasBasePath = Path.Combine(userProfile, "Documents", "ComfyUI", "models", "loras");
        }

        public string DatabasePath => _databasePath;
        public string LorasBasePath => _lorasBasePath;

        public async Task LoadAsync()
        {
            if (!File.Exists(_databasePath))
            {
                _entries = new Dictionary<string, LoraEntry>();
                return;
            }

            try
            {
                string json = await File.ReadAllTextAsync(_databasePath);
                var loadedEntries = JsonConvert.DeserializeObject<Dictionary<string, LoraEntry>>(json);

                if (loadedEntries != null)
                {
                    _entries = loadedEntries;

                    // Populate Path and FullPath for each entry
                    foreach (var kvp in _entries)
                    {
                        kvp.Value.Path = kvp.Key;
                        kvp.Value.FullPath = Path.Combine(_lorasBasePath, kvp.Key + ".safetensors");
                        kvp.Value.FileExists = File.Exists(kvp.Value.FullPath);

                        // Validate file_id if file exists
                        if (kvp.Value.FileExists)
                        {
                            try
                            {
                                kvp.Value.CalculatedFileId = FileIdCalculator.CalculateFileId(kvp.Value.FullPath);
                                kvp.Value.FileIdValid = !string.IsNullOrEmpty(kvp.Value.FileId) &&
                                                       kvp.Value.FileId != "unknown" &&
                                                       kvp.Value.FileId == kvp.Value.CalculatedFileId;
                            }
                            catch
                            {
                                kvp.Value.FileIdValid = false;
                            }
                        }
                        else
                        {
                            kvp.Value.FileIdValid = false;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to load database: {ex.Message}", ex);
            }
        }

        public async Task SaveAsync()
        {
            try
            {
                // Ensure directory exists
                var directory = Path.GetDirectoryName(_databasePath);
                if (directory != null && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                // Create a dictionary without the JsonIgnore properties
                var saveDict = _entries.ToDictionary(
                    kvp => kvp.Key,
                    kvp => kvp.Value
                );

                string json = JsonConvert.SerializeObject(saveDict, Formatting.Indented);
                await File.WriteAllTextAsync(_databasePath, json);
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to save database: {ex.Message}", ex);
            }
        }

        public IEnumerable<LoraEntry> GetAllEntries()
        {
            return _entries.Values;
        }

        public LoraEntry? GetEntry(string path)
        {
            return _entries.TryGetValue(path, out var entry) ? entry : null;
        }

        public void UpdateFileId(string path, string newFileId)
        {
            if (_entries.TryGetValue(path, out var entry))
            {
                entry.FileId = newFileId;
                entry.FileIdValid = entry.CalculatedFileId == newFileId;
            }
        }

        public void AddEntry(string path, LoraEntry entry)
        {
            entry.Path = path;
            entry.FullPath = Path.Combine(_lorasBasePath, path + ".safetensors");
            _entries[path] = entry;
        }

        public void RemoveEntry(string path)
        {
            _entries.Remove(path);
        }
    }
}
