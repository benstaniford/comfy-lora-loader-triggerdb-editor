using System.Diagnostics;
using System.IO;

namespace LoraDbEditor.Services
{
    /// <summary>
    /// Handles git operations for version control integration
    /// </summary>
    public class GitService
    {
        /// <summary>
        /// Checks if git is available on the system
        /// </summary>
        public async Task<bool> IsGitAvailableAsync()
        {
            try
            {
                var gitVersion = await RunGitCommandAsync("--version", Directory.GetCurrentDirectory());
                return !string.IsNullOrEmpty(gitVersion);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Checks if a directory is a git repository
        /// </summary>
        public async Task<bool> IsGitRepositoryAsync(string workingDirectory)
        {
            try
            {
                var gitStatus = await RunGitCommandAsync("status --porcelain", workingDirectory);
                return gitStatus != null; // If git status works, we're in a repo
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Checks if there are uncommitted changes to specific files
        /// </summary>
        public async Task<bool> HasUncommittedChangesAsync(string workingDirectory, params string[] paths)
        {
            try
            {
                // Build the git status command with file paths
                var pathsArg = string.Join(" ", paths.Select(p => $"\"{p}\""));
                var status = await RunGitCommandAsync($"status --porcelain {pathsArg}", workingDirectory);

                // Enable button if there are changes
                return !string.IsNullOrWhiteSpace(status);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Commits changes to git
        /// </summary>
        public async Task<bool> CommitChangesAsync(string workingDirectory, string message, params string[] paths)
        {
            try
            {
                // Add all specified files
                foreach (var path in paths)
                {
                    await RunGitCommandAsync($"add \"{path}\"", workingDirectory);
                }

                // Commit with the specified message
                await RunGitCommandAsync($"commit -m \"{message}\"", workingDirectory);

                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Runs a git command and returns the output
        /// </summary>
        public async Task<string?> RunGitCommandAsync(string arguments, string workingDirectory)
        {
            try
            {
                var processInfo = new ProcessStartInfo
                {
                    FileName = "git",
                    Arguments = arguments,
                    WorkingDirectory = workingDirectory,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var process = new Process { StartInfo = processInfo };
                process.Start();

                var output = await process.StandardOutput.ReadToEndAsync();
                var error = await process.StandardError.ReadToEndAsync();

                await process.WaitForExitAsync();

                if (process.ExitCode != 0 && !string.IsNullOrEmpty(error))
                {
                    throw new Exception(error);
                }

                return output;
            }
            catch
            {
                return null;
            }
        }
    }
}
