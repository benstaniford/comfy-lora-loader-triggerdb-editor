using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace LoraDbEditor.Services
{
    public static class FileIdCalculator
    {
        private const int ChunkSize = 1024 * 1024; // 1MB

        /// <summary>
        /// Generate a SHA1 hash from:
        /// - File size
        /// - First 1MB of file
        /// - Last 1MB of file (if file is larger than 1MB)
        /// </summary>
        public static string CalculateFileId(string filePath)
        {
            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException("File not found", filePath);
            }

            var fileInfo = new FileInfo(filePath);
            long fileSize = fileInfo.Length;

            using var sha1 = SHA1.Create();

            // Hash file size
            byte[] sizeBytes = Encoding.UTF8.GetBytes(fileSize.ToString());
            sha1.TransformBlock(sizeBytes, 0, sizeBytes.Length, null, 0);

            using (var stream = File.OpenRead(filePath))
            {
                // Read first 1MB
                byte[] firstChunk = new byte[ChunkSize];
                int firstBytesRead = stream.Read(firstChunk, 0, ChunkSize);
                sha1.TransformBlock(firstChunk, 0, firstBytesRead, null, 0);

                // Read last 1MB if file is larger than 1MB
                if (fileSize > ChunkSize)
                {
                    stream.Seek(-ChunkSize, SeekOrigin.End);
                    byte[] lastChunk = new byte[ChunkSize];
                    int lastBytesRead = stream.Read(lastChunk, 0, ChunkSize);
                    sha1.TransformBlock(lastChunk, 0, lastBytesRead, null, 0);
                }
            }

            // Finalize the hash
            sha1.TransformFinalBlock(Array.Empty<byte>(), 0, 0);

            // Convert to hex string
            return BitConverter.ToString(sha1.Hash!).Replace("-", "").ToLowerInvariant();
        }
    }
}
