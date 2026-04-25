using System;
using System.IO;
using System.Threading.Tasks;

namespace Wdpl2.Services
{
    /// <summary>
    /// Simple image optimization service for website images
    /// </summary>
    public sealed class ImageOptimizationService
    {
        /// <summary>
        /// Load and optionally resize an image file
        /// </summary>
        public async Task<(byte[] data, string base64)> LoadAndOptimizeImageAsync(
            string filePath, 
            int maxWidth = 300, 
            int maxHeight = 150)
        {
            try
            {
                // Read the file
                var bytes = await File.ReadAllBytesAsync(filePath);
                
                // For now, just return the bytes and base64
                // In a full implementation, you would resize using SkiaSharp or similar
                var base64 = Convert.ToBase64String(bytes);
                
                return (bytes, base64);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Image optimization error: {ex.Message}");
                throw;
            }
        }
        
        /// <summary>
        /// Convert image bytes to base64 data URL for embedding in HTML
        /// </summary>
        public string ToDataUrl(byte[] imageData, string mimeType = "image/png")
        {
            var base64 = Convert.ToBase64String(imageData);
            return $"data:{mimeType};base64,{base64}";
        }
        
        /// <summary>
        /// Get MIME type from file extension
        /// </summary>
        public string GetMimeType(string fileName)
        {
            var extension = Path.GetExtension(fileName).ToLowerInvariant();
            return extension switch
            {
                ".jpg" or ".jpeg" => "image/jpeg",
                ".png" => "image/png",
                ".gif" => "image/gif",
                ".bmp" => "image/bmp",
                ".webp" => "image/webp",
                ".svg" => "image/svg+xml",
                _ => "image/png"
            };
        }
        
        /// <summary>
        /// Validate image file
        /// </summary>
        public bool IsValidImageFile(string fileName)
        {
            var extension = Path.GetExtension(fileName).ToLowerInvariant();
            return extension is ".jpg" or ".jpeg" or ".png" or ".gif" or ".bmp" or ".webp" or ".svg";
        }
        
        /// <summary>
        /// Get image dimensions from byte array (simplified - returns 0,0 for now)
        /// In production, use SkiaSharp or similar to read actual dimensions
        /// </summary>
        public async Task<(int width, int height)> GetImageDimensionsAsync(byte[] imageData)
        {
            await Task.CompletedTask;
            // TODO: Implement proper image dimension reading with SkiaSharp
            // For now, return default values
            return (800, 600);
        }
    }
}
