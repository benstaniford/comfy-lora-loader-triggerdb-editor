using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace LoraDbEditor.Services
{
    /// <summary>
    /// Creates and shows modal dialogs
    /// </summary>
    public class DialogService
    {
        /// <summary>
        /// Shows a dialog to rename a single file
        /// </summary>
        /// <param name="owner">Owner window</param>
        /// <param name="currentName">Current file name (without extension)</param>
        /// <returns>New name if OK was clicked, null if cancelled</returns>
        public string? ShowRenameSingleFileDialog(Window owner, string currentName)
        {
            var dialog = CreateDialog(owner, "Rename LoRA", 500, 220);
            var (grid, textBox) = CreateInputDialog(
                dialog,
                "New name (without .safetensors extension):",
                currentName
            );

            dialog.Content = grid;
            textBox.SelectAll();
            textBox.Focus();

            var result = dialog.ShowDialog();
            return result == true ? textBox.Text.Trim() : null;
        }

        /// <summary>
        /// Shows a dialog to rename a folder
        /// </summary>
        /// <param name="owner">Owner window</param>
        /// <param name="currentName">Current folder name</param>
        /// <returns>New name if OK was clicked, null if cancelled</returns>
        public string? ShowRenameFolderDialog(Window owner, string currentName)
        {
            var dialog = CreateDialog(owner, "Rename Folder", 500, 220);
            var (grid, textBox) = CreateInputDialog(
                dialog,
                "New folder name:",
                currentName
            );

            dialog.Content = grid;
            textBox.SelectAll();
            textBox.Focus();

            var result = dialog.ShowDialog();
            return result == true ? textBox.Text.Trim() : null;
        }

        /// <summary>
        /// Shows a dialog to create a new folder
        /// </summary>
        /// <param name="owner">Owner window</param>
        /// <param name="parentDirectory">Parent directory path for context</param>
        /// <returns>Folder name if OK was clicked, null if cancelled</returns>
        public string? ShowCreateFolderDialog(Window owner, string parentDirectory)
        {
            var dialog = CreateDialog(owner, "Create New Folder", 500, 220);

            var grid = new Grid { Margin = new Thickness(15) };
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var locationText = string.IsNullOrEmpty(parentDirectory)
                ? "Location: (root)"
                : $"Location: {parentDirectory}";

            var locationLabel = new TextBlock
            {
                Text = locationText,
                Foreground = (SolidColorBrush)owner.FindResource("TextBrush"),
                Margin = new Thickness(0, 0, 0, 15),
                Opacity = 0.7
            };
            Grid.SetRow(locationLabel, 0);
            grid.Children.Add(locationLabel);

            var label = new TextBlock
            {
                Text = "Folder name:",
                Foreground = (SolidColorBrush)owner.FindResource("TextBrush"),
                Margin = new Thickness(0, 0, 0, 10)
            };
            Grid.SetRow(label, 1);
            grid.Children.Add(label);

            var textBox = new TextBox
            {
                Text = "",
                Foreground = (SolidColorBrush)owner.FindResource("TextBrush"),
                Background = (SolidColorBrush)owner.FindResource("SurfaceBrush"),
                BorderBrush = (SolidColorBrush)owner.FindResource("BorderBrush"),
                Padding = new Thickness(8),
                FontSize = 14,
                Height = 40,
                VerticalContentAlignment = VerticalAlignment.Center
            };
            Grid.SetRow(textBox, 2);
            grid.Children.Add(textBox);

            var buttonPanel = CreateButtonPanel(dialog);
            Grid.SetRow(buttonPanel, 3);
            grid.Children.Add(buttonPanel);

            dialog.Content = grid;
            textBox.Focus();

            var result = dialog.ShowDialog();
            return result == true ? textBox.Text.Trim() : null;
        }

        /// <summary>
        /// Shows a confirmation dialog
        /// </summary>
        /// <param name="owner">Owner window</param>
        /// <param name="message">Message to display</param>
        /// <param name="title">Dialog title</param>
        /// <returns>True if Yes was clicked</returns>
        public bool ShowConfirmDialog(Window owner, string message, string title)
        {
            var result = MessageBox.Show(message, title, MessageBoxButton.YesNo, MessageBoxImage.Warning);
            return result == MessageBoxResult.Yes;
        }

        /// <summary>
        /// Shows a folder selection dialog
        /// </summary>
        /// <param name="owner">Owner window</param>
        /// <param name="allFilePaths">List of all file paths for folder tree</param>
        /// <returns>Selected folder path, or null if cancelled</returns>
        public string? ShowFolderSelectionDialog(Window owner, List<string> allFilePaths)
        {
            var dialog = new FolderSelectionDialog(allFilePaths);
            dialog.Owner = owner;

            if (dialog.ShowDialog() == true)
            {
                return dialog.SelectedPath;
            }

            return null;
        }

        private Window CreateDialog(Window owner, string title, double width, double height)
        {
            return new Window
            {
                Title = title,
                Width = width,
                Height = height,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = owner,
                Background = (SolidColorBrush)owner.FindResource("BackgroundBrush"),
                ResizeMode = ResizeMode.NoResize
            };
        }

        private (Grid grid, TextBox textBox) CreateInputDialog(Window dialog, string labelText, string initialValue)
        {
            var grid = new Grid { Margin = new Thickness(15) };
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var label = new TextBlock
            {
                Text = labelText,
                Foreground = Brushes.White,
                Margin = new Thickness(0, 0, 0, 10)
            };
            Grid.SetRow(label, 0);
            grid.Children.Add(label);

            var textBox = new TextBox
            {
                Text = initialValue,
                Foreground = Brushes.White,
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2D2D30")!),
                BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#3F3F46")!),
                Padding = new Thickness(8),
                FontSize = 14,
                Height = 40,
                VerticalContentAlignment = VerticalAlignment.Center
            };
            Grid.SetRow(textBox, 2);
            grid.Children.Add(textBox);

            var suggestButton = new Button
            {
                Content = "Suggest",
                Width = 80,
                Height = 30,
                Margin = new Thickness(0, 10, 0, 0),
                HorizontalAlignment = HorizontalAlignment.Left
            };
            suggestButton.Click += (s, e) =>
            {
                textBox.Text = textBox.Text.ToLower().Replace('_', '-');
            };
            Grid.SetRow(suggestButton, 1);
            grid.Children.Add(suggestButton);

            var buttonPanel = CreateButtonPanel(dialog);
            Grid.SetRow(buttonPanel, 3);
            grid.Children.Add(buttonPanel);

            return (grid, textBox);
        }

        private StackPanel CreateButtonPanel(Window dialog)
        {
            var buttonPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 15, 0, 0)
            };

            var okButton = new Button
            {
                Content = "OK",
                Width = 80,
                Height = 30,
                Margin = new Thickness(0, 0, 10, 0),
                IsDefault = true
            };

            var cancelButton = new Button
            {
                Content = "Cancel",
                Width = 80,
                Height = 30,
                IsCancel = true
            };

            if (dialog != null)
            {
                okButton.Click += (s, e) =>
                {
                    dialog.DialogResult = true;
                    dialog.Close();
                };

                cancelButton.Click += (s, e) =>
                {
                    dialog.Close();
                };
            }

            buttonPanel.Children.Add(okButton);
            buttonPanel.Children.Add(cancelButton);

            return buttonPanel;
        }
    }
}
