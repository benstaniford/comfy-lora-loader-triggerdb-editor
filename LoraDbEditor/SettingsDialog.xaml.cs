using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;

namespace LoraDbEditor
{
    public partial class SettingsDialog : Window
    {
        private const string RegistryKeyPath = @"Software\LoraDbEditor";
        private const string ApiKeyValueName = "CivitaiApiKey";
        private const string DatabasePathValueName = "DatabasePath";
        private const string LorasPathValueName = "LorasPath";

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
                            // Set default path
                            string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                            DatabasePathTextBox.Text = Path.Combine(userProfile, "Documents", "ComfyUI", "user", "default", "user-db", "lora-triggers.json");
                        }

                        var lorasPath = key.GetValue(LorasPathValueName) as string;
                        if (!string.IsNullOrEmpty(lorasPath))
                        {
                            LorasPathTextBox.Text = lorasPath;
                        }
                        else
                        {
                            // Set default path
                            string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                            LorasPathTextBox.Text = Path.Combine(userProfile, "Documents", "ComfyUI", "models", "loras");
                        }
                    }
                    else
                    {
                        // No registry key yet, set defaults
                        string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                        DatabasePathTextBox.Text = Path.Combine(userProfile, "Documents", "ComfyUI", "user", "default", "user-db", "lora-triggers.json");
                        LorasPathTextBox.Text = Path.Combine(userProfile, "Documents", "ComfyUI", "models", "loras");
                    }
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

            // Return default path
            string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            return Path.Combine(userProfile, "Documents", "ComfyUI", "user", "default", "user-db", "lora-triggers.json");
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

            // Return default path
            string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            return Path.Combine(userProfile, "Documents", "ComfyUI", "models", "loras");
        }
    }
}
