using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;

namespace LoraDbEditor
{
    public partial class SettingsDialog : Window
    {
        private const string RegistryKeyPath = @"Software\LoraDbEditor";
        private const string ApiKeyValueName = "CivitaiApiKey";

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
                            StatusTextBlock.Text = "API key loaded from registry";
                        }
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

                            MessageBox.Show("API key removed.", "Settings Saved",
                                MessageBoxButton.OK, MessageBoxImage.Information);
                        }
                        else
                        {
                            key.SetValue(ApiKeyValueName, apiKey);
                            MessageBox.Show("API key saved successfully!", "Settings Saved",
                                MessageBoxButton.OK, MessageBoxImage.Information);
                        }

                        DialogResult = true;
                        Close();
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error saving settings: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
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
    }
}
