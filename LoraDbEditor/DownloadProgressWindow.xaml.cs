using System.Windows;

namespace LoraDbEditor
{
    public partial class DownloadProgressWindow : Window
    {
        public DownloadProgressWindow()
        {
            InitializeComponent();
        }

        public void UpdateProgress(int percentage, long bytesDownloaded, long totalBytes)
        {
            Dispatcher.Invoke(() =>
            {
                ProgressBar.Value = percentage;
                ProgressText.Text = $"{percentage}%";

                if (totalBytes > 0)
                {
                    SizeText.Text = $"{FormatBytes(bytesDownloaded)} / {FormatBytes(totalBytes)}";
                }
                else
                {
                    SizeText.Text = $"{FormatBytes(bytesDownloaded)} downloaded";
                }
            });
        }

        public void UpdateStatus(string status)
        {
            Dispatcher.Invoke(() =>
            {
                StatusText.Text = status;
            });
        }

        private string FormatBytes(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB", "TB" };
            double len = bytes;
            int order = 0;

            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len = len / 1024;
            }

            return $"{len:0.##} {sizes[order]}";
        }
    }
}
