using System.IO;
using System.Net.Http;
using System.Text.RegularExpressions;

namespace LoraDbEditor.Services
{
    /// <summary>
    /// Result of a download operation
    /// </summary>
    public class DownloadResult
    {
        public bool Success { get; set; }
        public string RelativePath { get; set; } = string.Empty;
        public string FullPath { get; set; } = string.Empty;
        public string FileId { get; set; } = string.Empty;
        public string ErrorMessage { get; set; } = string.Empty;
    }

    /// <summary>
    /// Progress information for a download
    /// </summary>
    public class DownloadProgress
    {
        public long BytesDownloaded { get; set; }
        public long TotalBytes { get; set; }
        public int Percentage { get; set; }
        public string Status { get; set; } = string.Empty;
    }

    /// <summary>
    /// Handles HTTP downloads and file copy operations for LoRA files
    /// </summary>
    public class DownloadService
    {
        /// <summary>
        /// Downloads a LoRA file from a URL
        /// </summary>
        public async Task<DownloadResult> DownloadLoraAsync(
            string url,
            string targetPath,
            string lorasBasePath,
            IProgress<DownloadProgress>? progress = null)
        {
            try
            {
                // Apply Civitai API key if applicable
                url = ApplyCivitaiApiKey(url);

                // Extract filename from URL (will be improved after getting Content-Disposition header)
                string filename = GetFilenameFromUrl(url);

                // Build the relative path by combining folder and filename
                string relativePath = string.IsNullOrEmpty(targetPath)
                    ? filename
                    : targetPath + "/" + filename;

                // Build the full file path
                string fullPath = Path.Combine(lorasBasePath, relativePath + ".safetensors");

                // Ensure directory exists
                var directory = Path.GetDirectoryName(fullPath);
                if (directory != null && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                // Download the file
                using (var httpClient = new HttpClient())
                {
                    httpClient.Timeout = TimeSpan.FromMinutes(30); // Long timeout for large files

                    // Set Chrome user agent and other headers to avoid being blocked
                    httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0.0.0 Safari/537.36");
                    httpClient.DefaultRequestHeaders.Add("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,image/apng,*/*;q=0.8,application/signed-exchange;v=b3;q=0.7");
                    httpClient.DefaultRequestHeaders.Add("Accept-Language", "en-US,en;q=0.9");
                    httpClient.DefaultRequestHeaders.Add("Accept-Encoding", "gzip, deflate, br");
                    httpClient.DefaultRequestHeaders.Add("DNT", "1");
                    httpClient.DefaultRequestHeaders.Add("Connection", "keep-alive");
                    httpClient.DefaultRequestHeaders.Add("Upgrade-Insecure-Requests", "1");
                    httpClient.DefaultRequestHeaders.Add("Sec-Fetch-Dest", "document");
                    httpClient.DefaultRequestHeaders.Add("Sec-Fetch-Mode", "navigate");
                    httpClient.DefaultRequestHeaders.Add("Sec-Fetch-Site", "none");
                    httpClient.DefaultRequestHeaders.Add("Sec-Fetch-User", "?1");
                    httpClient.DefaultRequestHeaders.Add("sec-ch-ua", "\"Google Chrome\";v=\"131\", \"Chromium\";v=\"131\", \"Not_A Brand\";v=\"24\"");
                    httpClient.DefaultRequestHeaders.Add("sec-ch-ua-mobile", "?0");
                    httpClient.DefaultRequestHeaders.Add("sec-ch-ua-platform", "\"Windows\"");

                    using (var response = await httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead))
                    {
                        response.EnsureSuccessStatusCode();

                        // Try to get filename from Content-Disposition header
                        string? headerFilename = GetFilenameFromContentDisposition(response);
                        if (!string.IsNullOrEmpty(headerFilename))
                        {
                            // Update filename from header
                            filename = Path.GetFileNameWithoutExtension(headerFilename);

                            // Rebuild paths with the correct filename
                            relativePath = string.IsNullOrEmpty(targetPath)
                                ? filename
                                : targetPath + "/" + filename;
                            fullPath = Path.Combine(lorasBasePath, relativePath + ".safetensors");

                            progress?.Report(new DownloadProgress { Status = $"Downloading: {filename}.safetensors [from server]" });
                            System.Diagnostics.Debug.WriteLine($"Using server-provided filename: {filename}.safetensors");
                        }
                        else
                        {
                            progress?.Report(new DownloadProgress { Status = $"Downloading: {filename}.safetensors [from URL - no server filename]" });
                            System.Diagnostics.Debug.WriteLine($"WARNING: No Content-Disposition header found, using URL-based filename: {filename}.safetensors");
                        }

                        var totalBytes = response.Content.Headers.ContentLength ?? 0;

                        using (var contentStream = await response.Content.ReadAsStreamAsync())
                        using (var fileStream = new FileStream(fullPath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true))
                        {
                            var buffer = new byte[8192];
                            long totalBytesRead = 0;
                            int bytesRead;

                            while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                            {
                                await fileStream.WriteAsync(buffer, 0, bytesRead);
                                totalBytesRead += bytesRead;

                                if (totalBytes > 0)
                                {
                                    var percentage = (int)((totalBytesRead * 100) / totalBytes);
                                    progress?.Report(new DownloadProgress
                                    {
                                        BytesDownloaded = totalBytesRead,
                                        TotalBytes = totalBytes,
                                        Percentage = percentage,
                                        Status = $"Downloading: {percentage}%"
                                    });
                                }
                            }
                        }
                    }
                }

                progress?.Report(new DownloadProgress { Status = "Calculating file ID..." });

                // Calculate file ID
                string fileId = FileIdCalculator.CalculateFileId(fullPath);

                return new DownloadResult
                {
                    Success = true,
                    RelativePath = relativePath,
                    FullPath = fullPath,
                    FileId = fileId
                };
            }
            catch (Exception ex)
            {
                return new DownloadResult
                {
                    Success = false,
                    ErrorMessage = $"Error downloading file: {ex.Message}"
                };
            }
        }

        /// <summary>
        /// Copies a local LoRA file to the LoRA directory
        /// </summary>
        public async Task<DownloadResult> CopyLoraAsync(
            string sourceFile,
            string targetPath,
            string lorasBasePath)
        {
            try
            {
                // Get the filename without extension
                var filename = Path.GetFileNameWithoutExtension(sourceFile);

                // Build the relative path by combining folder and filename
                string relativePath = string.IsNullOrEmpty(targetPath)
                    ? filename
                    : targetPath + "/" + filename;

                // Build the full file path
                string fullPath = Path.Combine(lorasBasePath, relativePath + ".safetensors");

                // Ensure directory exists
                var directory = Path.GetDirectoryName(fullPath);
                if (directory != null && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                // Copy the file
                File.Copy(sourceFile, fullPath, overwrite: true);

                // Calculate file ID
                string fileId = FileIdCalculator.CalculateFileId(fullPath);

                return new DownloadResult
                {
                    Success = true,
                    RelativePath = relativePath,
                    FullPath = fullPath,
                    FileId = fileId
                };
            }
            catch (Exception ex)
            {
                return new DownloadResult
                {
                    Success = false,
                    ErrorMessage = $"Error copying file: {ex.Message}"
                };
            }
        }

        /// <summary>
        /// Extracts a filename from a URL
        /// </summary>
        public string GetFilenameFromUrl(string url)
        {
            try
            {
                var uri = new Uri(url);
                var path = uri.AbsolutePath;

                // If the path ends with a slash, it's a directory, not a file
                if (path.EndsWith('/'))
                {
                    return "downloaded-lora";
                }

                // Get the last segment of the path
                var segments = path.Split('/');
                var lastSegment = segments.LastOrDefault(s => !string.IsNullOrWhiteSpace(s));

                if (!string.IsNullOrEmpty(lastSegment))
                {
                    // Remove extension if it's .safetensors
                    if (lastSegment.EndsWith(".safetensors", StringComparison.OrdinalIgnoreCase))
                    {
                        return Path.GetFileNameWithoutExtension(lastSegment);
                    }

                    // Check if it looks like a filename (has an extension)
                    if (lastSegment.Contains('.'))
                    {
                        return Path.GetFileNameWithoutExtension(lastSegment);
                    }

                    // Use as-is
                    return lastSegment;
                }
            }
            catch
            {
                // Fall through to default
            }

            // Default fallback
            return "downloaded-lora";
        }

        /// <summary>
        /// Extracts filename from Content-Disposition header
        /// </summary>
        public string? GetFilenameFromContentDisposition(HttpResponseMessage response)
        {
            try
            {
                string? rawHeaderValue = null;

                // FIRST: Try to get the raw header value from all possible locations
                // Check response.Content.Headers first
                if (response.Content.Headers.TryGetValues("Content-Disposition", out var contentHeaderValues))
                {
                    rawHeaderValue = contentHeaderValues.FirstOrDefault();
                    System.Diagnostics.Debug.WriteLine($"Found Content-Disposition in Content.Headers: {rawHeaderValue}");
                }
                // Then check response.Headers (some servers put it here instead)
                else if (response.Headers.TryGetValues("Content-Disposition", out var responseHeaderValues))
                {
                    rawHeaderValue = responseHeaderValues.FirstOrDefault();
                    System.Diagnostics.Debug.WriteLine($"Found Content-Disposition in Headers: {rawHeaderValue}");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("No Content-Disposition header found in response");
                }

                // If we found a raw header, parse it manually with multiple patterns
                if (!string.IsNullOrEmpty(rawHeaderValue))
                {
                    // Pattern 1: filename*=UTF-8''encoded%20name.ext (RFC 5987)
                    var match = Regex.Match(rawHeaderValue, @"filename\*\s*=\s*(?:UTF-8''|utf-8'')(.+?)(?:;|$)", RegexOptions.IgnoreCase);
                    if (match.Success)
                    {
                        var encoded = match.Groups[1].Value.Trim();
                        try
                        {
                            var decoded = Uri.UnescapeDataString(encoded).Trim('"', ' ', '\'');
                            System.Diagnostics.Debug.WriteLine($"Extracted filename from filename*= (encoded): {decoded}");
                            if (!string.IsNullOrWhiteSpace(decoded))
                                return decoded;
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"Failed to decode UTF-8 filename: {ex.Message}");
                        }
                    }

                    // Pattern 2: filename="name with spaces.ext"
                    match = Regex.Match(rawHeaderValue, @"filename\s*=\s*""([^""]+)""", RegexOptions.IgnoreCase);
                    if (match.Success)
                    {
                        var filename = match.Groups[1].Value.Trim();
                        System.Diagnostics.Debug.WriteLine($"Extracted filename from filename=\"...\": {filename}");
                        if (!string.IsNullOrWhiteSpace(filename))
                            return filename;
                    }

                    // Pattern 3: filename='name with spaces.ext' (single quotes)
                    match = Regex.Match(rawHeaderValue, @"filename\s*=\s*'([^']+)'", RegexOptions.IgnoreCase);
                    if (match.Success)
                    {
                        var filename = match.Groups[1].Value.Trim();
                        System.Diagnostics.Debug.WriteLine($"Extracted filename from filename='...': {filename}");
                        if (!string.IsNullOrWhiteSpace(filename))
                            return filename;
                    }

                    // Pattern 4: filename=name.ext (no quotes)
                    match = Regex.Match(rawHeaderValue, @"filename\s*=\s*([^;""\s]+)", RegexOptions.IgnoreCase);
                    if (match.Success)
                    {
                        var filename = match.Groups[1].Value.Trim();
                        System.Diagnostics.Debug.WriteLine($"Extracted filename from filename=... (no quotes): {filename}");
                        if (!string.IsNullOrWhiteSpace(filename))
                            return filename;
                    }

                    System.Diagnostics.Debug.WriteLine($"Could not extract filename from header: {rawHeaderValue}");
                }

                // SECOND: Try the parsed ContentDisposition properties as fallback
                if (response.Content.Headers.ContentDisposition != null)
                {
                    // Check FileName property
                    if (!string.IsNullOrWhiteSpace(response.Content.Headers.ContentDisposition.FileName))
                    {
                        var filename = response.Content.Headers.ContentDisposition.FileName.Trim('"', ' ', '\'');
                        System.Diagnostics.Debug.WriteLine($"Extracted filename from ContentDisposition.FileName: {filename}");
                        if (!string.IsNullOrWhiteSpace(filename))
                            return filename;
                    }

                    // Check FileNameStar property (RFC 5987)
                    if (!string.IsNullOrWhiteSpace(response.Content.Headers.ContentDisposition.FileNameStar))
                    {
                        var filename = response.Content.Headers.ContentDisposition.FileNameStar.Trim('"', ' ', '\'');
                        System.Diagnostics.Debug.WriteLine($"Extracted filename from ContentDisposition.FileNameStar: {filename}");
                        if (!string.IsNullOrWhiteSpace(filename))
                            return filename;
                    }
                }

                // Log all response headers for debugging
                System.Diagnostics.Debug.WriteLine("=== All Response Headers ===");
                foreach (var header in response.Headers)
                {
                    System.Diagnostics.Debug.WriteLine($"{header.Key}: {string.Join(", ", header.Value)}");
                }
                foreach (var header in response.Content.Headers)
                {
                    System.Diagnostics.Debug.WriteLine($"{header.Key}: {string.Join(", ", header.Value)}");
                }
                System.Diagnostics.Debug.WriteLine("=== End Headers ===");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error parsing Content-Disposition: {ex.Message}");
            }

            return null;
        }

        /// <summary>
        /// Applies Civitai API key to a URL if applicable
        /// </summary>
        public string ApplyCivitaiApiKey(string url)
        {
            try
            {
                // Check if this is a Civitai URL
                var uri = new Uri(url);
                if (uri.Host.Contains("civitai.com", StringComparison.OrdinalIgnoreCase))
                {
                    // Get the API key from settings
                    var apiKey = SettingsDialog.GetCivitaiApiKey();
                    if (!string.IsNullOrWhiteSpace(apiKey))
                    {
                        // Check if URL already has the token parameter
                        if (url.Contains("?token=", StringComparison.OrdinalIgnoreCase) ||
                            url.Contains("&token=", StringComparison.OrdinalIgnoreCase))
                        {
                            System.Diagnostics.Debug.WriteLine("URL already contains a token parameter");
                            return url;
                        }

                        // Append the API key
                        var separator = url.Contains('?') ? "&" : "?";
                        var authenticatedUrl = $"{url}{separator}token={apiKey}";
                        System.Diagnostics.Debug.WriteLine($"Applied Civitai API key to URL");
                        return authenticatedUrl;
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine("No Civitai API key configured");
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error applying API key: {ex.Message}");
            }

            return url;
        }
    }
}
