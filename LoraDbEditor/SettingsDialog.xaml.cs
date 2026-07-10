using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using LoraDbEditor.Services;

namespace LoraDbEditor
{
    public partial class SettingsDialog : Window
    {
        private const string RegistryKeyPath = @"Software\LoraDbEditor";
        private const string ApiKeyValueName = "CivitaiApiKey";
        private const string DatabasePathValueName = "DatabasePath";
        private const string LorasPathValueName = "LorasPath";
        private const string GalleryPathValueName = "GalleryPath";
        private const string SshUploadPathValueName = "SshUploadPath";

        private string _originalDatabasePath = string.Empty;
        private string _originalLorasPath = string.Empty;

        public bool PathsChanged { get; private set; }

        public SettingsDialog()
        {
            InitializeComponent();
            LoadSettings();
        }

        private void LoadSettings()
        {
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(RegistryKeyPath))
                {
                    if (key != null)
                    {
                        var apiKey = key.GetValue(ApiKeyValueName) as string;
                        if (!string.IsNullOrEmpty(apiKey))
                        {
                            ApiKeyTextBox.Text = apiKey;
                            StatusTextBlock.Text = "Settings loaded from registry";
                        }

                        var databasePath = key.GetValue(DatabasePathValueName) as string;
                        if (!string.IsNullOrEmpty(databasePath))
                        {
                            DatabasePathTextBox.Text = databasePath;
                        }
                        else
                        {
                            // Discover a sensible default (Comfy Desktop 2 shared space, else legacy)
                            DatabasePathTextBox.Text = ComfyPathDiscovery.DiscoverDatabasePath();
                        }

                        var lorasPath = key.GetValue(LorasPathValueName) as string;
                        if (!string.IsNullOrEmpty(lorasPath))
                        {
                            LorasPathTextBox.Text = lorasPath;
                        }
                        else
                        {
                            // Discover a sensible default (Comfy Desktop 2 shared space, else legacy)
                            LorasPathTextBox.Text = ComfyPathDiscovery.DiscoverLorasPath();
                        }

                        var sshUploadPath = key.GetValue(SshUploadPathValueName) as string;
                        SshUploadPathTextBox.Text = sshUploadPath ?? "";
                    }
                    else
                    {
                        // No registry key yet, discover defaults
                        DatabasePathTextBox.Text = ComfyPathDiscovery.DiscoverDatabasePath();
                        LorasPathTextBox.Text = ComfyPathDiscovery.DiscoverLorasPath();
                    }

                    // Store original values to detect changes
                    _originalDatabasePath = DatabasePathTextBox.Text;
                    _originalLorasPath = LorasPathTextBox.Text;
                }
            }
            catch (Exception ex)
            {
                StatusTextBlock.Text = $"Error loading settings: {ex.Message}";
            }
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                using (var key = Registry.CurrentUser.CreateSubKey(RegistryKeyPath))
                {
                    if (key != null)
                    {
                        var apiKey = ApiKeyTextBox.Text.Trim();

                        if (string.IsNullOrEmpty(apiKey))
                        {
                            // Delete the value if empty
                            try
                            {
                                key.DeleteValue(ApiKeyValueName, false);
                            }
                            catch { }
                        }
                        else
                        {
                            key.SetValue(ApiKeyValueName, apiKey);
                        }

                        // Save database path
                        var databasePath = DatabasePathTextBox.Text.Trim();
                        if (!string.IsNullOrEmpty(databasePath))
                        {
                            key.SetValue(DatabasePathValueName, databasePath);
                        }

                        // Save loras path
                        var lorasPath = LorasPathTextBox.Text.Trim();
                        if (!string.IsNullOrEmpty(lorasPath))
                        {
                            key.SetValue(LorasPathValueName, lorasPath);
                        }

                        // Save SSH upload path
                        var sshUploadPath = SshUploadPathTextBox.Text.Trim();
                        if (!string.IsNullOrEmpty(sshUploadPath))
                        {
                            key.SetValue(SshUploadPathValueName, sshUploadPath);
                        }
                        else
                        {
                            try { key.DeleteValue(SshUploadPathValueName, false); } catch { }
                        }

                        // Check if paths changed
                        PathsChanged = databasePath != _originalDatabasePath || lorasPath != _originalLorasPath;

                        StatusTextBlock.Text = "Settings saved successfully!";
                        DialogResult = true;
                        Close();
                    }
                }
            }
            catch (Exception ex)
            {
                StatusTextBlock.Text = $"Error saving settings: {ex.Message}";
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void ApiKeyTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            StatusTextBlock.Text = "";
        }

        private void DetectButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var databasePath = ComfyPathDiscovery.DiscoverDatabasePath();
                var lorasPath = ComfyPathDiscovery.DiscoverLorasPath();

                DatabasePathTextBox.Text = databasePath;
                LorasPathTextBox.Text = lorasPath;

                bool found = Directory.Exists(lorasPath) || File.Exists(databasePath);
                StatusTextBlock.Text = found
                    ? "Detected ComfyUI paths. Review and click Save to apply."
                    : "No ComfyUI install found; filled with legacy defaults.";
            }
            catch (Exception ex)
            {
                StatusTextBlock.Text = $"Error detecting paths: {ex.Message}";
            }
        }

        public static string? GetCivitaiApiKey()
        {
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(RegistryKeyPath))
                {
                    if (key != null)
                    {
                        return key.GetValue(ApiKeyValueName) as string;
                    }
                }
            }
            catch
            {
                // Ignore errors when reading
            }

            return null;
        }

        public static string GetDatabasePath()
        {
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(RegistryKeyPath))
                {
                    if (key != null)
                    {
                        var path = key.GetValue(DatabasePathValueName) as string;
                        if (!string.IsNullOrEmpty(path))
                        {
                            return path;
                        }
                    }
                }
            }
            catch
            {
                // Ignore errors when reading
            }

            // Discover a default (Comfy Desktop 2 shared space, else legacy)
            return ComfyPathDiscovery.DiscoverDatabasePath();
        }

        public static string GetLorasPath()
        {
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(RegistryKeyPath))
                {
                    if (key != null)
                    {
                        var path = key.GetValue(LorasPathValueName) as string;
                        if (!string.IsNullOrEmpty(path))
                        {
                            return path;
                        }
                    }
                }
            }
            catch
            {
                // Ignore errors when reading
            }

            // Discover a default (Comfy Desktop 2 shared space, else legacy)
            return ComfyPathDiscovery.DiscoverLorasPath();
        }

        /// <summary>
        /// The gallery-pictures directory. Uses an explicit registry override if set, otherwise follows
        /// the database directory so the gallery always sits beside the configured lora-triggers.json.
        /// </summary>
        public static string GetGalleryPath()
        {
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(RegistryKeyPath))
                {
                    var path = key?.GetValue(GalleryPathValueName) as string;
                    if (!string.IsNullOrWhiteSpace(path))
                    {
                        return path;
                    }
                }
            }
            catch
            {
                // Ignore errors when reading
            }

            var dbDirectory = Path.GetDirectoryName(GetDatabasePath());
            if (!string.IsNullOrEmpty(dbDirectory))
            {
                return Path.Combine(dbDirectory, "lora-triggers-pictures");
            }

            return ComfyPathDiscovery.DiscoverGalleryPath();
        }

        public static string? GetSshUploadPath()
        {
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(RegistryKeyPath))
                {
                    if (key != null)
                    {
                        var path = key.GetValue(SshUploadPathValueName) as string;
                        if (!string.IsNullOrWhiteSpace(path))
                            return path;
                    }
                }
            }
            catch
            {
                // Ignore errors when reading
            }

            return null;
        }
    }
}
