using System;
using System.Collections;
using System.IO;
using NUnit.Framework;
using UniCli.Server.Editor.Handlers;
using UnityEngine;
using UnityEngine.TestTools;

namespace UniCli.Server.Editor.Tests
{
    [TestFixture]
    public class ScreenshotCaptureEditorHandlerTests : UITreeWindowTestBase
    {
        [UnityTest]
        public IEnumerator Execute_CapturePanelAndDiff_WritesImages()
        {
            if (Application.isBatchMode)
            {
                Assert.Ignore("Editor window screenshot capture requires visible editor UI.");
            }

            var tempDir = Path.Combine(Path.GetTempPath(), "unicli_editor_capture_tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);

            var basePath = Path.Combine(tempDir, "base.png");
            var changedPath = Path.Combine(tempDir, "changed.png");

            try
            {
                yield return null;
                yield return null;

                var captureHandler = new ScreenshotCaptureEditorHandler();
                var first = UITreeTestHelpers.Execute<ScreenshotCaptureEditorRequest, ScreenshotCaptureEditorResponse>(captureHandler, new ScreenshotCaptureEditorRequest
                {
                    panel = UITreeTestEditorWindow.Title,
                    filename = basePath
                });

                Assert.That(File.Exists(first.imagePath), Is.True);
                Assert.That(first.width, Is.GreaterThan(400));
                Assert.That(first.height, Is.GreaterThan(200));

                var firstTexture = LoadTexture(first.imagePath);
                try
                {
                    var topLeft = firstTexture.GetPixel(20, firstTexture.height - 20);
                    Assert.That(topLeft.r, Is.LessThan(0.35f));
                    Assert.That(topLeft.g, Is.LessThan(0.35f));
                    Assert.That(topLeft.b, Is.LessThan(0.40f));
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(firstTexture);
                }

                var clickHandler = new UITreeClickHandler();
                UITreeTestHelpers.Execute<UITreeClickRequest, UITreeClickResponse>(clickHandler, new UITreeClickRequest
                {
                    panel = UITreeTestEditorWindow.Title,
                    selector = "#run-button"
                });

                var fillHandler = new UITreeFillHandler();
                UITreeTestHelpers.Execute<UITreeFillRequest, UITreeFillResponse>(fillHandler, new UITreeFillRequest
                {
                    panel = UITreeTestEditorWindow.Title,
                    selector = "#name-field",
                    value = "Changed from screenshot test"
                });

                var selectHandler = new UITreeSelectHandler();
                UITreeTestHelpers.Execute<UITreeSelectRequest, UITreeSelectResponse>(selectHandler, new UITreeSelectRequest
                {
                    panel = UITreeTestEditorWindow.Title,
                    selector = "#mode-dropdown",
                    choice = "Gamma"
                });

                yield return null;
                yield return null;

                ScreenshotCaptureEditorResponse second = null;
                for (var attempt = 0; attempt < 5; attempt++)
                {
                    second = UITreeTestHelpers.Execute<ScreenshotCaptureEditorRequest, ScreenshotCaptureEditorResponse>(captureHandler, new ScreenshotCaptureEditorRequest
                    {
                        panel = UITreeTestEditorWindow.Title,
                        filename = changedPath,
                        diffBase = first.imagePath
                    });

                    if (second.diff != null && second.diff.changedPixels > 0)
                    {
                        break;
                    }

                    yield return null;
                    yield return null;
                }

                Assert.That(File.Exists(second.imagePath), Is.True);
                Assert.That(second.diff, Is.Not.Null);
                Assert.That(File.Exists(second.diff.diffImagePath), Is.True);
                Assert.That(second.diff.changedPixels, Is.GreaterThan(0));

                var crop = UITreeTestHelpers.Execute<ScreenshotCaptureEditorRequest, ScreenshotCaptureEditorResponse>(captureHandler, new ScreenshotCaptureEditorRequest
                {
                    panel = UITreeTestEditorWindow.Title,
                    selector = "#preview-swatch",
                    filename = Path.Combine(tempDir, "crop.png")
                });

                Assert.That(crop.width, Is.LessThan(first.width));
                Assert.That(crop.height, Is.LessThan(first.height));
                Assert.That(crop.height, Is.GreaterThan(60));

                var cropTexture = LoadTexture(crop.imagePath);
                try
                {
                    var swatch = cropTexture.GetPixel(cropTexture.width / 2, cropTexture.height / 2);
                    Assert.That(swatch.g, Is.GreaterThan(0.45f));
                    Assert.That(swatch.g, Is.GreaterThan(swatch.r));
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(cropTexture);
                }
            }
            finally
            {
                if (Directory.Exists(tempDir))
                {
                    Directory.Delete(tempDir, true);
                }
            }
        }

        private static Texture2D LoadTexture(string path)
        {
            var bytes = File.ReadAllBytes(path);
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            texture.LoadImage(bytes);
            return texture;
        }
    }
}
