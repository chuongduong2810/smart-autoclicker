using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Structure;
using System.Drawing;
using AutomationTool.Models;
using Microsoft.Extensions.Logging;

namespace AutomationTool.Services
{
    public interface IImageRecognitionService
    {
        Task<MatchResult> FindImageAsync(byte[] screenData, byte[] templateData, double threshold = 0.8);
        Task<MatchResult> FindImageAsync(byte[] screenData, TemplateImage template);
        Task<List<MatchResult>> FindAllImagesAsync(byte[] screenData, byte[] templateData, double threshold = 0.8);
        Task<bool> IsImagePresentAsync(byte[] screenData, byte[] templateData, double threshold = 0.8);
        Task<MatchResult> WaitForImageAsync(byte[] templateData, TimeSpan timeout, double threshold = 0.8);
        Task<MatchResult> WaitForImageDisappearAsync(byte[] templateData, TimeSpan timeout, double threshold = 0.8);
    }

    public class ImageRecognitionService : IImageRecognitionService
    {
        private readonly IScreenshotService _screenshotService;
        private readonly ILogger<ImageRecognitionService> _logger;

        public ImageRecognitionService(IScreenshotService screenshotService, ILogger<ImageRecognitionService> logger)
        {
            _screenshotService = screenshotService;
            _logger = logger;
        }

        public async Task<MatchResult> FindImageAsync(byte[] screenData, byte[] templateData, double threshold = 0.8)
        {
            return await Task.Run(() =>
            {
                var stopwatch = System.Diagnostics.Stopwatch.StartNew();
                
                try
                {
                    using var screenMat = LoadImageFromBytes(screenData);
                    using var templateMat = LoadImageFromBytes(templateData);
                    
                    if (screenMat == null || templateMat == null)
                    {
                        _logger.LogWarning("Failed to load screen or template image");
                        return new MatchResult { Found = false, SearchTime = stopwatch.Elapsed };
                    }

                    // Perform template matching
                    using var result = new Mat();
                    CvInvoke.MatchTemplate(screenMat, templateMat, result, TemplateMatchingType.CcoeffNormed);

                    // Find the best match
                    double minVal = 0, maxVal = 0;
                    Point minLoc = new Point(), maxLoc = new Point();
                    CvInvoke.MinMaxLoc(result, ref minVal, ref maxVal, ref minLoc, ref maxLoc);

                    stopwatch.Stop();

                    var matchResult = new MatchResult
                    {
                        Found = maxVal >= threshold,
                        Location = maxLoc,
                        Confidence = maxVal,
                        BoundingBox = new Rectangle(maxLoc.X, maxLoc.Y, templateMat.Width, templateMat.Height),
                        SearchTime = stopwatch.Elapsed
                    };

                    _logger.LogDebug("Image matching completed. Found: {Found}, Confidence: {Confidence:F3}, Time: {Time}ms", 
                        matchResult.Found, matchResult.Confidence, matchResult.SearchTime.TotalMilliseconds);

                    return matchResult;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error during image matching");
                    return new MatchResult { Found = false, SearchTime = stopwatch.Elapsed };
                }
            });
        }

        public async Task<MatchResult> FindImageAsync(byte[] screenData, TemplateImage template)
        {
            return await FindImageAsync(screenData, template.ImageData, template.MatchThreshold);
        }

        public async Task<List<MatchResult>> FindAllImagesAsync(byte[] screenData, byte[] templateData, double threshold = 0.8)
        {
            // For simplicity, just return the single best match
            var singleResult = await FindImageAsync(screenData, templateData, threshold);
            return singleResult.Found ? new List<MatchResult> { singleResult } : new List<MatchResult>();
        }

        public async Task<bool> IsImagePresentAsync(byte[] screenData, byte[] templateData, double threshold = 0.8)
        {
            var result = await FindImageAsync(screenData, templateData, threshold);
            return result.Found;
        }

        public async Task<MatchResult> WaitForImageAsync(byte[] templateData, TimeSpan timeout, double threshold = 0.8)
        {
            var startTime = DateTime.Now;
            var checkInterval = TimeSpan.FromMilliseconds(500);

            while (DateTime.Now - startTime < timeout)
            {
                try
                {
                    var screenData = await _screenshotService.CaptureFullScreenAsync();
                    var result = await FindImageAsync(screenData, templateData, threshold);
                    
                    if (result.Found)
                    {
                        _logger.LogInformation("Image found after waiting {Duration}ms", (DateTime.Now - startTime).TotalMilliseconds);
                        return result;
                    }

                    await Task.Delay(checkInterval);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error during image wait");
                    await Task.Delay(checkInterval);
                }
            }

            _logger.LogWarning("Image not found within timeout period of {Timeout}ms", timeout.TotalMilliseconds);
            return new MatchResult { Found = false, SearchTime = DateTime.Now - startTime };
        }

        public async Task<MatchResult> WaitForImageDisappearAsync(byte[] templateData, TimeSpan timeout, double threshold = 0.8)
        {
            var startTime = DateTime.Now;
            var checkInterval = TimeSpan.FromMilliseconds(500);

            while (DateTime.Now - startTime < timeout)
            {
                try
                {
                    var screenData = await _screenshotService.CaptureFullScreenAsync();
                    var result = await FindImageAsync(screenData, templateData, threshold);
                    
                    if (!result.Found)
                    {
                        _logger.LogInformation("Image disappeared after waiting {Duration}ms", (DateTime.Now - startTime).TotalMilliseconds);
                        return new MatchResult 
                        { 
                            Found = true, // Found = true means we found the disappearance
                            SearchTime = DateTime.Now - startTime 
                        };
                    }

                    await Task.Delay(checkInterval);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error during image disappear wait");
                    await Task.Delay(checkInterval);
                }
            }

            _logger.LogWarning("Image did not disappear within timeout period of {Timeout}ms", timeout.TotalMilliseconds);
            return new MatchResult { Found = false, SearchTime = DateTime.Now - startTime };
        }

        private Mat? LoadImageFromBytes(byte[] imageData)
        {
            try
            {
                // Load image directly using CvInvoke
                var mat = new Mat();
                CvInvoke.Imdecode(imageData, ImreadModes.Grayscale, mat);
                return mat;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load image from bytes");
                return null;
            }
        }

        private static bool RectanglesOverlap(Rectangle rect1, Rectangle rect2)
        {
            return rect1.IntersectsWith(rect2);
        }
    }

    // Alternative simpler implementation for basic template matching
    public class SimpleImageRecognitionService : IImageRecognitionService
    {
        private readonly IScreenshotService _screenshotService;
        private readonly ILogger<SimpleImageRecognitionService> _logger;

        public SimpleImageRecognitionService(IScreenshotService screenshotService, ILogger<SimpleImageRecognitionService> logger)
        {
            _screenshotService = screenshotService;
            _logger = logger;
        }

        public async Task<MatchResult> FindImageAsync(byte[] screenData, byte[] templateData, double threshold = 0.8)
        {
            return await Task.Run(() =>
            {
                var stopwatch = System.Diagnostics.Stopwatch.StartNew();
                
                try
                {
                    using var screenBitmap = new Bitmap(new MemoryStream(screenData));
                    using var templateBitmap = new Bitmap(new MemoryStream(templateData));
                    
                    var result = FindBitmapInBitmap(screenBitmap, templateBitmap, threshold);
                    result.SearchTime = stopwatch.Elapsed;
                    
                    return result;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error during simple image matching");
                    return new MatchResult { Found = false, SearchTime = stopwatch.Elapsed };
                }
            });
        }

        public async Task<MatchResult> FindImageAsync(byte[] screenData, TemplateImage template)
        {
            return await FindImageAsync(screenData, template.ImageData, template.MatchThreshold);
        }

        public async Task<List<MatchResult>> FindAllImagesAsync(byte[] screenData, byte[] templateData, double threshold = 0.8)
        {
            // Simple implementation - just return first match
            var result = await FindImageAsync(screenData, templateData, threshold);
            return result.Found ? new List<MatchResult> { result } : new List<MatchResult>();
        }

        public async Task<bool> IsImagePresentAsync(byte[] screenData, byte[] templateData, double threshold = 0.8)
        {
            var result = await FindImageAsync(screenData, templateData, threshold);
            return result.Found;
        }

        public async Task<MatchResult> WaitForImageAsync(byte[] templateData, TimeSpan timeout, double threshold = 0.8)
        {
            var startTime = DateTime.Now;
            var checkInterval = TimeSpan.FromMilliseconds(1000);

            while (DateTime.Now - startTime < timeout)
            {
                try
                {
                    var screenData = await _screenshotService.CaptureFullScreenAsync();
                    var result = await FindImageAsync(screenData, templateData, threshold);
                    
                    if (result.Found)
                    {
                        return result;
                    }

                    await Task.Delay(checkInterval);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error during simple image wait");
                    await Task.Delay(checkInterval);
                }
            }

            return new MatchResult { Found = false, SearchTime = DateTime.Now - startTime };
        }

        public async Task<MatchResult> WaitForImageDisappearAsync(byte[] templateData, TimeSpan timeout, double threshold = 0.8)
        {
            var startTime = DateTime.Now;
            var checkInterval = TimeSpan.FromMilliseconds(1000);

            while (DateTime.Now - startTime < timeout)
            {
                try
                {
                    var screenData = await _screenshotService.CaptureFullScreenAsync();
                    var result = await FindImageAsync(screenData, templateData, threshold);
                    
                    if (!result.Found)
                    {
                        return new MatchResult { Found = true, SearchTime = DateTime.Now - startTime };
                    }

                    await Task.Delay(checkInterval);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error during simple image disappear wait");
                    await Task.Delay(checkInterval);
                }
            }

            return new MatchResult { Found = false, SearchTime = DateTime.Now - startTime };
        }

        private MatchResult FindBitmapInBitmap(Bitmap screen, Bitmap template, double threshold)
        {
            // Simple pixel-by-pixel comparison implementation
            // This is a basic fallback if OpenCV fails
            
            if (template.Width > screen.Width || template.Height > screen.Height)
            {
                return new MatchResult { Found = false };
            }

            double bestMatch = 0;
            Point bestLocation = Point.Empty;

            for (int x = 0; x <= screen.Width - template.Width; x += 2) // Skip pixels for performance
            {
                for (int y = 0; y <= screen.Height - template.Height; y += 2)
                {
                    double similarity = CalculatePixelSimilarity(screen, template, x, y);
                    if (similarity > bestMatch)
                    {
                        bestMatch = similarity;
                        bestLocation = new Point(x, y);
                    }
                }
            }

            return new MatchResult
            {
                Found = bestMatch >= threshold,
                Location = bestLocation,
                Confidence = bestMatch,
                BoundingBox = new Rectangle(bestLocation.X, bestLocation.Y, template.Width, template.Height)
            };
        }

        private double CalculatePixelSimilarity(Bitmap screen, Bitmap template, int offsetX, int offsetY)
        {
            int matchingPixels = 0;
            int totalPixels = 0;
            int skipFactor = Math.Max(1, Math.Min(template.Width, template.Height) / 20); // Sample pixels

            for (int x = 0; x < template.Width; x += skipFactor)
            {
                for (int y = 0; y < template.Height; y += skipFactor)
                {
                    if (offsetX + x < screen.Width && offsetY + y < screen.Height)
                    {
                        var screenPixel = screen.GetPixel(offsetX + x, offsetY + y);
                        var templatePixel = template.GetPixel(x, y);
                        
                        var diff = Math.Abs(screenPixel.R - templatePixel.R) +
                                  Math.Abs(screenPixel.G - templatePixel.G) +
                                  Math.Abs(screenPixel.B - templatePixel.B);
                        
                        if (diff < 30) // Tolerance for pixel differences
                        {
                            matchingPixels++;
                        }
                        totalPixels++;
                    }
                }
            }

            return totalPixels > 0 ? (double)matchingPixels / totalPixels : 0;
        }
    }
}