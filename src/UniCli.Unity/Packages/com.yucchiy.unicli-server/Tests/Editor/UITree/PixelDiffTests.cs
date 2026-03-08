using System;
using System.IO;
using System.Threading;
using NUnit.Framework;
using UniCli.Server.Editor.Handlers;
using UnityEngine;

namespace UniCli.Server.Editor.Tests
{
    [TestFixture]
    public class PixelDiffTests
    {
        [Test]
        public void Compute_WithDifferentPixels_ReturnsDiffMetrics()
        {
            var tempDir = Path.Combine(Path.GetTempPath(), "unicli_pixel_diff_tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);

            var baselinePath = Path.Combine(tempDir, "baseline.png");
            var currentPath = Path.Combine(tempDir, "current.png");
            var diffPath = Path.Combine(tempDir, "diff.png");

            try
            {
                WriteImage(baselinePath, 8, 8, new Color32(0, 0, 0, 255), null);
                WriteImage(currentPath, 8, 8, new Color32(0, 0, 0, 255), (2, 3, new Color32(255, 255, 255, 255)));

                var pixelDiff = new PixelDiff();
                var result = pixelDiff.Compute(baselinePath, currentPath, diffPath, 10, CancellationToken.None);

                Assert.That(result.changedPixels, Is.GreaterThan(0));
                Assert.That(result.percentage, Is.GreaterThan(0f));
                Assert.That(File.Exists(result.diffImagePath), Is.True);
                Assert.That(result.changedRegions.Length, Is.EqualTo(1));
            }
            finally
            {
                if (Directory.Exists(tempDir))
                {
                    Directory.Delete(tempDir, true);
                }
            }
        }

        private static void WriteImage(string path, int width, int height, Color32 fill, (int x, int y, Color32 color)? overridePixel)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            try
            {
                var pixels = new Color32[width * height];
                for (var i = 0; i < pixels.Length; i++)
                {
                    pixels[i] = fill;
                }

                if (overridePixel.HasValue)
                {
                    var value = overridePixel.Value;
                    pixels[value.y * width + value.x] = value.color;
                }

                texture.SetPixels32(pixels);
                texture.Apply();
                File.WriteAllBytes(path, texture.EncodeToPNG());
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }
    }
}
