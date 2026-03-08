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
    public class ScreenshotCaptureEditorPureImguiWindowTests
    {
        [SetUp]
        public void SetUp()
        {
            PureImguiCaptureTestEditorWindow.Open();
        }

        [TearDown]
        public void TearDown()
        {
            var windows = Resources.FindObjectsOfTypeAll<PureImguiCaptureTestEditorWindow>();
            foreach (var window in windows)
            {
                if (window != null)
                {
                    window.Close();
                }
            }
        }

        [UnityTest]
        public IEnumerator Execute_CapturePureImguiWindow_IncludesLegacyContent()
        {
            if (Application.isBatchMode)
            {
                Assert.Ignore("Editor window screenshot capture requires visible editor UI.");
            }

            var tempDir = Path.Combine(Path.GetTempPath(), "unicli_editor_capture_pure_imgui_tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);

            try
            {
                yield return null;
                yield return null;

                var handler = new ScreenshotCaptureEditorHandler();
                var response = UITreeTestHelpers.Execute<ScreenshotCaptureEditorRequest, ScreenshotCaptureEditorResponse>(handler, new ScreenshotCaptureEditorRequest
                {
                    panel = PureImguiCaptureTestEditorWindow.Title,
                    filename = Path.Combine(tempDir, "pure-imgui.png")
                });

                Assert.That(File.Exists(response.imagePath), Is.True);

                var texture = LoadTexture(response.imagePath);
                try
                {
                    Assert.That(ContainsDominantPixel(texture, 0, texture.width / 2,
                        color => color.r > 0.50f && color.r > color.g + 0.20f), Is.True);
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(texture);
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

        private static bool ContainsDominantPixel(Texture2D texture, int xMin, int xMax, Func<Color, bool> predicate)
        {
            for (var y = 0; y < texture.height; y += 4)
            {
                for (var x = xMin; x < xMax; x += 4)
                {
                    if (predicate(texture.GetPixel(x, y)))
                    {
                        return true;
                    }
                }
            }

            return false;
        }
    }
}
