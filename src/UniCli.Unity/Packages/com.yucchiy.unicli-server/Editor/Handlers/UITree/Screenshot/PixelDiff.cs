using System;
using System.IO;
using System.Threading;
using UnityEngine;

namespace UniCli.Server.Editor.Handlers
{
    internal sealed class PixelDiff
    {
        public ScreenshotDiffInfo Compute(string baselinePath, string currentPath, string diffOutputPath, int threshold, CancellationToken cancellationToken)
        {
            if (!File.Exists(baselinePath))
            {
                throw new InvalidOperationException($"Baseline image not found: {baselinePath}");
            }

            if (!File.Exists(currentPath))
            {
                throw new InvalidOperationException($"Current image not found: {currentPath}");
            }

            Texture2D baselineTexture = null;
            Texture2D currentTexture = null;
            Texture2D diffTexture = null;
            try
            {
                baselineTexture = LoadTexture(baselinePath);
                currentTexture = LoadTexture(currentPath);

                if (baselineTexture.width != currentTexture.width || baselineTexture.height != currentTexture.height)
                {
                    throw new InvalidOperationException(
                        $"Baseline dimension mismatch. baseline={baselineTexture.width}x{baselineTexture.height}, current={currentTexture.width}x{currentTexture.height}");
                }

                var width = currentTexture.width;
                var height = currentTexture.height;
                var totalPixels = width * height;

                var baselinePixels = baselineTexture.GetPixels32();
                var currentPixels = currentTexture.GetPixels32();
                var diffPixels = new Color32[totalPixels];

                var changedPixels = 0;
                var minX = width;
                var minY = height;
                var maxX = -1;
                var maxY = -1;

                for (var index = 0; index < totalPixels; index++)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var baseColor = baselinePixels[index];
                    var currentColor = currentPixels[index];
                    var delta = Math.Abs(currentColor.r - baseColor.r)
                        + Math.Abs(currentColor.g - baseColor.g)
                        + Math.Abs(currentColor.b - baseColor.b);

                    if (delta > threshold)
                    {
                        changedPixels++;
                        var x = index % width;
                        var y = index / width;
                        if (x < minX) minX = x;
                        if (y < minY) minY = y;
                        if (x > maxX) maxX = x;
                        if (y > maxY) maxY = y;
                        diffPixels[index] = new Color32(255, 0, 0, 255);
                    }
                    else
                    {
                        diffPixels[index] = new Color32(baseColor.r, baseColor.g, baseColor.b, 90);
                    }
                }

                diffTexture = new Texture2D(width, height, TextureFormat.RGBA32, false);
                diffTexture.SetPixels32(diffPixels);
                diffTexture.Apply();

                EnsureDirectory(diffOutputPath);
                File.WriteAllBytes(diffOutputPath, diffTexture.EncodeToPNG());

                var region = changedPixels > 0
                    ? new[]
                    {
                        new ScreenshotChangedRegion
                        {
                            x = minX,
                            y = minY,
                            width = maxX - minX + 1,
                            height = maxY - minY + 1
                        }
                    }
                    : Array.Empty<ScreenshotChangedRegion>();

                return new ScreenshotDiffInfo
                {
                    changedPixels = changedPixels,
                    percentage = totalPixels > 0 ? (changedPixels * 100f) / totalPixels : 0f,
                    diffImagePath = Path.GetFullPath(diffOutputPath),
                    changedRegions = region
                };
            }
            finally
            {
                if (diffTexture != null)
                {
                    UnityEngine.Object.DestroyImmediate(diffTexture);
                }

                if (currentTexture != null)
                {
                    UnityEngine.Object.DestroyImmediate(currentTexture);
                }

                if (baselineTexture != null)
                {
                    UnityEngine.Object.DestroyImmediate(baselineTexture);
                }
            }
        }

        private static Texture2D LoadTexture(string path)
        {
            var bytes = File.ReadAllBytes(path);
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!texture.LoadImage(bytes))
            {
                UnityEngine.Object.DestroyImmediate(texture);
                throw new InvalidOperationException($"Failed to load image: {path}");
            }

            return texture;
        }

        private static void EnsureDirectory(string path)
        {
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
        }
    }
}
