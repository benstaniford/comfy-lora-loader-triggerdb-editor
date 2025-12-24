using System.IO;
using LoraDbEditor.Models;

namespace LoraDbEditor.Services
{
    /// <summary>
    /// Result of a file or folder operation
    /// </summary>
    public class FileOperationResult
    {
        public bool Success { get; set; }
        public string? NewPath { get; set; }
        public string? ErrorMessage { get; set; }
        public int AffectedFileCount { get; set; }
    }

    /// <summary>
    /// Handles file and folder operations (rename, delete, move, create)
    /// </summary>
    public class FileOperationsService
    {
        /// <summary>
        /// Renames a single LoRA file
        /// </summary>
        public async Task<FileOperationResult> RenameSingleFileAsync(
            string oldPath,
            string newName,
            LoraDatabase database,
            string galleryBasePath)
        {
            var oldFullPath = Path.Combine(database.LorasBasePath, oldPath + ".safetensors");

            // Check if file exists
            if (!File.Exists(oldFullPath))
            {
                return new FileOperationResult
                {
                    Success = false,
                    ErrorMessage = "The selected file does not exist on disk."
                };
            }

            // Get current name and directory
            var currentName = Path.GetFileName(oldPath);
            var directory = Path.GetDirectoryName(oldPath)?.Replace("\\", "/") ?? "";

            // Validate new name
            if (string.IsNullOrWhiteSpace(newName))
            {
                return new FileOperationResult
                {
                    Success = false,
                    ErrorMessage = "Name cannot be empty."
                };
            }

            if (newName == currentName)
            {
                // No change needed
                return new FileOperationResult { Success = true, NewPath = oldPath };
            }

            // Check for invalid characters
            var invalidChars = Path.GetInvalidFileNameChars();
            if (newName.IndexOfAny(invalidChars) >= 0)
            {
                return new FileOperationResult
                {
                    Success = false,
                    ErrorMessage = "Name contains invalid characters."
                };
            }

            // Build new path
            var newPath = string.IsNullOrEmpty(directory) ? newName : directory + "/" + newName;
            var newFullPath = Path.Combine(database.LorasBasePath, newPath + ".safetensors");

            // Check if target already exists
            if (File.Exists(newFullPath))
            {
                return new FileOperationResult
                {
                    Success = false,
                    ErrorMessage = $"A file already exists at: {newPath}"
                };
            }

            try
            {
                // Rename the file on disk
                File.Move(oldFullPath, newFullPath);

                // Update the database
                var entry = database.GetEntry(oldPath);
                if (entry != null)
                {
                    // Remove old entry
                    database.RemoveEntry(oldPath);

                    // Update entry paths
                    entry.Path = newPath;
                    entry.FullPath = newFullPath;

                    // Add with new path
                    database.AddEntry(newPath, entry);

                    // Update gallery image filenames if they exist
                    if (entry.Gallery != null && entry.Gallery.Count > 0)
                    {
                        UpdateGalleryFilenames(oldPath, newPath, entry, galleryBasePath);
                    }

                    // Save the database
                    await database.SaveAsync();
                }

                return new FileOperationResult
                {
                    Success = true,
                    NewPath = newPath
                };
            }
            catch (Exception ex)
            {
                return new FileOperationResult
                {
                    Success = false,
                    ErrorMessage = $"Error renaming file: {ex.Message}"
                };
            }
        }

        /// <summary>
        /// Renames a folder and all its contents
        /// </summary>
        public async Task<FileOperationResult> RenameFolderAsync(
            string oldFolderPath,
            string newName,
            LoraDatabase database,
            string galleryBasePath,
            List<string> allFilePaths)
        {
            var oldFullPath = Path.Combine(database.LorasBasePath, oldFolderPath);

            // Check if folder exists
            if (!Directory.Exists(oldFullPath))
            {
                return new FileOperationResult
                {
                    Success = false,
                    ErrorMessage = "The selected folder does not exist on disk."
                };
            }

            // Get current name and parent directory
            var currentName = Path.GetFileName(oldFolderPath);
            var parentDirectory = Path.GetDirectoryName(oldFolderPath)?.Replace("\\", "/") ?? "";

            // Validate new name
            if (string.IsNullOrWhiteSpace(newName))
            {
                return new FileOperationResult
                {
                    Success = false,
                    ErrorMessage = "Folder name cannot be empty."
                };
            }

            if (newName == currentName)
            {
                // No change needed
                return new FileOperationResult { Success = true, NewPath = oldFolderPath };
            }

            // Check for invalid characters
            var invalidChars = Path.GetInvalidFileNameChars();
            if (newName.IndexOfAny(invalidChars) >= 0)
            {
                return new FileOperationResult
                {
                    Success = false,
                    ErrorMessage = "Folder name contains invalid characters."
                };
            }

            // Build new path
            var newFolderPath = string.IsNullOrEmpty(parentDirectory) ? newName : parentDirectory + "/" + newName;
            var newFullPath = Path.Combine(database.LorasBasePath, newFolderPath);

            // Check if target already exists
            if (Directory.Exists(newFullPath))
            {
                return new FileOperationResult
                {
                    Success = false,
                    ErrorMessage = $"A folder already exists at: {newFolderPath}"
                };
            }

            try
            {
                // Rename the folder on disk
                Directory.Move(oldFullPath, newFullPath);

                // Find all files that were in this folder
                var affectedFiles = allFilePaths
                    .Where(path => path.StartsWith(oldFolderPath + "/"))
                    .ToList();

                bool anyEntriesUpdated = false;

                // Update all database entries for files in this folder
                foreach (var oldPath in affectedFiles)
                {
                    var entry = database.GetEntry(oldPath);
                    if (entry != null)
                    {
                        // Calculate new path by replacing the old folder prefix
                        var relativePath = oldPath.Substring(oldFolderPath.Length + 1);
                        var newPath = newFolderPath + "/" + relativePath;
                        var newFilePath = Path.Combine(database.LorasBasePath, newPath + ".safetensors");

                        // Remove old entry
                        database.RemoveEntry(oldPath);

                        // Update entry paths
                        entry.Path = newPath;
                        entry.FullPath = newFilePath;

                        // Add with new path
                        database.AddEntry(newPath, entry);

                        // Update gallery image filenames if they exist
                        if (entry.Gallery != null && entry.Gallery.Count > 0)
                        {
                            UpdateGalleryFilenames(oldPath, newPath, entry, galleryBasePath);
                        }

                        anyEntriesUpdated = true;
                    }
                }

                // Save the database
                if (anyEntriesUpdated)
                {
                    await database.SaveAsync();
                }

                return new FileOperationResult
                {
                    Success = true,
                    NewPath = newFolderPath,
                    AffectedFileCount = affectedFiles.Count
                };
            }
            catch (Exception ex)
            {
                return new FileOperationResult
                {
                    Success = false,
                    ErrorMessage = $"Error renaming folder: {ex.Message}"
                };
            }
        }

        /// <summary>
        /// Deletes a single LoRA file
        /// </summary>
        public async Task<FileOperationResult> DeleteSingleFileAsync(
            string loraPath,
            LoraDatabase database,
            string galleryBasePath)
        {
            var fullPath = Path.Combine(database.LorasBasePath, loraPath + ".safetensors");

            try
            {
                // Get the database entry if it exists (to delete gallery images)
                var entry = database.GetEntry(loraPath);

                // Delete gallery images if they exist
                if (entry?.Gallery != null && entry.Gallery.Count > 0)
                {
                    var safePath = loraPath.Replace("/", "_").Replace("\\", "_");
                    foreach (var imageName in entry.Gallery)
                    {
                        try
                        {
                            var imagePath = Path.Combine(galleryBasePath, imageName);
                            if (File.Exists(imagePath))
                            {
                                File.Delete(imagePath);
                            }
                        }
                        catch (Exception ex)
                        {
                            // Log but continue - don't fail the whole operation for a gallery image
                            System.Diagnostics.Debug.WriteLine($"Failed to delete gallery image {imageName}: {ex.Message}");
                        }
                    }
                }

                // Delete the database entry if it exists
                if (entry != null)
                {
                    database.RemoveEntry(loraPath);
                }

                // Delete the .safetensors file if it exists
                if (File.Exists(fullPath))
                {
                    File.Delete(fullPath);
                }

                // Save the database
                if (entry != null)
                {
                    await database.SaveAsync();
                }

                return new FileOperationResult
                {
                    Success = true
                };
            }
            catch (Exception ex)
            {
                return new FileOperationResult
                {
                    Success = false,
                    ErrorMessage = $"Error deleting file: {ex.Message}"
                };
            }
        }

        /// <summary>
        /// Deletes a folder and all its contents
        /// </summary>
        public async Task<FileOperationResult> DeleteFolderAsync(
            string folderPath,
            LoraDatabase database,
            string galleryBasePath,
            List<string> allFilePaths)
        {
            // Find all files in this folder
            var filesInFolder = allFilePaths
                .Where(path => path.StartsWith(folderPath + "/") || path == folderPath)
                .ToList();

            if (filesInFolder.Count == 0)
            {
                // Empty folder - just delete the directory
                var folderFullPath = Path.Combine(database.LorasBasePath, folderPath);
                if (Directory.Exists(folderFullPath))
                {
                    try
                    {
                        Directory.Delete(folderFullPath, true);

                        return new FileOperationResult
                        {
                            Success = true
                        };
                    }
                    catch (Exception ex)
                    {
                        return new FileOperationResult
                        {
                            Success = false,
                            ErrorMessage = $"Error deleting folder: {ex.Message}"
                        };
                    }
                }
                return new FileOperationResult { Success = true };
            }

            try
            {
                bool anyEntriesDeleted = false;

                // Delete all files in the folder
                foreach (var loraPath in filesInFolder)
                {
                    var fullPath = Path.Combine(database.LorasBasePath, loraPath + ".safetensors");

                    // Get the database entry if it exists (to delete gallery images)
                    var entry = database.GetEntry(loraPath);

                    // Delete gallery images if they exist
                    if (entry?.Gallery != null && entry.Gallery.Count > 0)
                    {
                        foreach (var imageName in entry.Gallery)
                        {
                            try
                            {
                                var imagePath = Path.Combine(galleryBasePath, imageName);
                                if (File.Exists(imagePath))
                                {
                                    File.Delete(imagePath);
                                }
                            }
                            catch (Exception ex)
                            {
                                System.Diagnostics.Debug.WriteLine($"Failed to delete gallery image {imageName}: {ex.Message}");
                            }
                        }
                    }

                    // Delete the database entry if it exists
                    if (entry != null)
                    {
                        database.RemoveEntry(loraPath);
                        anyEntriesDeleted = true;
                    }

                    // Delete the .safetensors file if it exists
                    if (File.Exists(fullPath))
                    {
                        File.Delete(fullPath);
                    }
                }

                // Delete the folder itself
                var folderFullPath = Path.Combine(database.LorasBasePath, folderPath);
                if (Directory.Exists(folderFullPath))
                {
                    Directory.Delete(folderFullPath, true);
                }

                // Save the database if any entries were deleted
                if (anyEntriesDeleted)
                {
                    await database.SaveAsync();
                }

                return new FileOperationResult
                {
                    Success = true,
                    AffectedFileCount = filesInFolder.Count
                };
            }
            catch (Exception ex)
            {
                return new FileOperationResult
                {
                    Success = false,
                    ErrorMessage = $"Error deleting folder: {ex.Message}"
                };
            }
        }

        /// <summary>
        /// Moves a LoRA file to a different folder
        /// </summary>
        public async Task<FileOperationResult> MoveLoraToFolderAsync(
            string sourcePath,
            string targetDirectory,
            LoraDatabase database,
            string galleryBasePath)
        {
            var oldPath = sourcePath;
            var oldFullPath = Path.Combine(database.LorasBasePath, oldPath + ".safetensors");

            // Check if file exists
            if (!File.Exists(oldFullPath))
            {
                return new FileOperationResult
                {
                    Success = false,
                    ErrorMessage = "The file does not exist on disk."
                };
            }

            // Get the filename
            var fileName = Path.GetFileName(oldPath);

            // Build new path
            var newPath = string.IsNullOrEmpty(targetDirectory) ? fileName : targetDirectory + "/" + fileName;
            var newFullPath = Path.Combine(database.LorasBasePath, newPath + ".safetensors");

            // Check if source and target are the same
            if (oldPath == newPath)
            {
                // No move needed
                return new FileOperationResult { Success = true, NewPath = newPath };
            }

            // Check if target already exists
            if (File.Exists(newFullPath))
            {
                return new FileOperationResult
                {
                    Success = false,
                    ErrorMessage = $"A file with the same name already exists in the target folder: {newPath}"
                };
            }

            try
            {
                // Ensure target directory exists
                var targetDir = Path.GetDirectoryName(newFullPath);
                if (!string.IsNullOrEmpty(targetDir) && !Directory.Exists(targetDir))
                {
                    Directory.CreateDirectory(targetDir);
                }

                // Move the file on disk
                File.Move(oldFullPath, newFullPath);

                // Update the database
                var entry = database.GetEntry(oldPath);
                if (entry != null)
                {
                    // Remove old entry
                    database.RemoveEntry(oldPath);

                    // Update entry paths
                    entry.Path = newPath;
                    entry.FullPath = newFullPath;

                    // Add with new path
                    database.AddEntry(newPath, entry);

                    // Update gallery image filenames if they exist
                    if (entry.Gallery != null && entry.Gallery.Count > 0)
                    {
                        UpdateGalleryFilenames(oldPath, newPath, entry, galleryBasePath);
                    }

                    // Save the database
                    await database.SaveAsync();
                }

                return new FileOperationResult
                {
                    Success = true,
                    NewPath = newPath
                };
            }
            catch (Exception ex)
            {
                return new FileOperationResult
                {
                    Success = false,
                    ErrorMessage = $"Error moving file: {ex.Message}"
                };
            }
        }

        /// <summary>
        /// Creates a new folder
        /// </summary>
        public FileOperationResult CreateFolder(string parentDirectory, string folderName, string lorasBasePath)
        {
            // Validate folder name
            if (string.IsNullOrWhiteSpace(folderName))
            {
                return new FileOperationResult
                {
                    Success = false,
                    ErrorMessage = "Folder name cannot be empty."
                };
            }

            // Check for invalid characters
            var invalidChars = Path.GetInvalidFileNameChars();
            if (folderName.IndexOfAny(invalidChars) >= 0 || folderName.Contains("/") || folderName.Contains("\\"))
            {
                return new FileOperationResult
                {
                    Success = false,
                    ErrorMessage = "Folder name contains invalid characters."
                };
            }

            // Build full folder path
            var folderPath = string.IsNullOrEmpty(parentDirectory)
                ? folderName
                : parentDirectory + "/" + folderName;
            var fullDiskPath = Path.Combine(lorasBasePath, folderPath);

            // Check if folder already exists
            if (Directory.Exists(fullDiskPath))
            {
                return new FileOperationResult
                {
                    Success = false,
                    ErrorMessage = $"A folder already exists at: {folderPath}"
                };
            }

            try
            {
                // Create the directory
                Directory.CreateDirectory(fullDiskPath);

                return new FileOperationResult
                {
                    Success = true,
                    NewPath = folderPath
                };
            }
            catch (Exception ex)
            {
                return new FileOperationResult
                {
                    Success = false,
                    ErrorMessage = $"Error creating folder: {ex.Message}"
                };
            }
        }

        /// <summary>
        /// Updates gallery image filenames when a LoRA is renamed or moved
        /// </summary>
        public void UpdateGalleryFilenames(string oldPath, string newPath, LoraEntry entry, string galleryBasePath)
        {
            if (entry.Gallery == null)
                return;

            var oldSafePath = oldPath.Replace("/", "_").Replace("\\", "_");
            var newSafePath = newPath.Replace("/", "_").Replace("\\", "_");

            var updatedGallery = new List<string>();

            foreach (var oldFileName in entry.Gallery)
            {
                // Check if the filename starts with the old safe path
                if (oldFileName.StartsWith(oldSafePath + "_"))
                {
                    var oldFilePath = Path.Combine(galleryBasePath, oldFileName);
                    if (File.Exists(oldFilePath))
                    {
                        // Create new filename by replacing the prefix
                        var newFileName = newSafePath + oldFileName.Substring(oldSafePath.Length);
                        var newFilePath = Path.Combine(galleryBasePath, newFileName);

                        try
                        {
                            // Rename the gallery image file
                            File.Move(oldFilePath, newFilePath);
                            updatedGallery.Add(newFileName);
                        }
                        catch
                        {
                            // If rename fails, keep old filename
                            updatedGallery.Add(oldFileName);
                        }
                    }
                    else
                    {
                        // File doesn't exist, update the name anyway
                        var newFileName = newSafePath + oldFileName.Substring(oldSafePath.Length);
                        updatedGallery.Add(newFileName);
                    }
                }
                else
                {
                    // Filename doesn't match pattern, keep as is
                    updatedGallery.Add(oldFileName);
                }
            }

            entry.Gallery = updatedGallery;
        }
    }
}
