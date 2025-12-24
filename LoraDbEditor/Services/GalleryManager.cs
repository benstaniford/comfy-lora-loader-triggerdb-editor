using System.IO;
using LoraDbEditor.Models;

namespace LoraDbEditor.Services
{
    /// <summary>
    /// Represents a gallery image
    /// </summary>
    public class GalleryImage
    {
        public string FileName { get; set; } = string.Empty;
        public string FullPath { get; set; } = string.Empty;
        public bool Exists { get; set; }
    }

    /// <summary>
    /// Manages gallery images for LoRA entries
    /// </summary>
    public class GalleryManager
    {
        /// <summary>
        /// Loads all gallery images for a LoRA entry
        /// </summary>
        public List<GalleryImage> LoadGalleryImages(LoraEntry? entry, string galleryBasePath)
        {
            var images = new List<GalleryImage>();

            if (entry?.Gallery == null || entry.Gallery.Count == 0)
                return images;

            foreach (var imageName in entry.Gallery)
            {
                var imagePath = Path.Combine(galleryBasePath, imageName);
                images.Add(new GalleryImage
                {
                    FileName = imageName,
                    FullPath = imagePath,
                    Exists = File.Exists(imagePath)
                });
            }

            return images;
        }

        /// <summary>
        /// Adds an image to the gallery for a LoRA entry
        /// </summary>
        /// <param name="sourceImagePath">Path to the source image file</param>
        /// <param name="entry">The LoRA entry</param>
        /// <param name="galleryBasePath">Base path for gallery images</param>
        /// <returns>The new image filename</returns>
        public string AddImageToGallery(string sourceImagePath, LoraEntry entry, string galleryBasePath)
        {
            var extension = Path.GetExtension(sourceImagePath);

            // Create a unique filename using the lora path and timestamp
            var safePath = entry.Path.Replace("/", "_").Replace("\\", "_");
            var timestamp = DateTime.Now.ToString("yyyyMMddHHmmss");
            var fileName = $"{safePath}_{timestamp}{extension}";
            var destPath = Path.Combine(galleryBasePath, fileName);

            // Copy the file
            File.Copy(sourceImagePath, destPath, overwrite: false);

            // Add to gallery list
            if (entry.Gallery == null)
            {
                entry.Gallery = new List<string>();
            }

            entry.Gallery.Add(fileName);

            return fileName;
        }

        /// <summary>
        /// Deletes gallery images from disk
        /// </summary>
        /// <param name="imageNames">List of image filenames to delete</param>
        /// <param name="galleryBasePath">Base path for gallery images</param>
        /// <returns>True if all deletions succeeded</returns>
        public bool DeleteGalleryImages(List<string> imageNames, string galleryBasePath)
        {
            bool allSucceeded = true;

            foreach (var imageName in imageNames)
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
                    allSucceeded = false;
                }
            }

            return allSucceeded;
        }

        /// <summary>
        /// Validates if a file is a supported image format
        /// </summary>
        public bool IsImageFile(string filePath)
        {
            var extension = Path.GetExtension(filePath)?.ToLowerInvariant();
            return extension == ".jpg" || extension == ".jpeg" || extension == ".png" ||
                   extension == ".bmp" || extension == ".gif" || extension == ".webp";
        }

        /// <summary>
        /// Generates a safe path string by replacing slashes with underscores
        /// </summary>
        public string GenerateSafePath(string path)
        {
            return path.Replace("/", "_").Replace("\\", "_");
        }
    }
}
