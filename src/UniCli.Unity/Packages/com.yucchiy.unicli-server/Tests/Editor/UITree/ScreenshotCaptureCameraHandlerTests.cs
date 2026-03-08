using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UniCli.Server.Editor.Handlers;
using UnityEngine;

namespace UniCli.Server.Editor.Tests
{
    [TestFixture]
    public class ScreenshotCaptureCameraHandlerTests
    {
        [Test]
        public void Execute_CapturesCameraImage()
        {
            var tempDir = Path.Combine(Path.GetTempPath(), "unicli_camera_capture_tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);
            var imagePath = Path.Combine(tempDir, "camera_a.png");
            var changedPath = Path.Combine(tempDir, "camera_b.png");
            var materials = new System.Collections.Generic.List<Material>();

            var root = new GameObject("UITreeCameraFixtureRoot");
            var cameraGo = new GameObject("UITreeTestCamera");
            cameraGo.transform.SetParent(root.transform);
            var camera = cameraGo.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.16f, 0.17f, 0.24f);
            camera.transform.position = new Vector3(0f, 1.8f, -5.5f);
            camera.transform.LookAt(new Vector3(0f, 0.9f, 0f));

            var lightGo = new GameObject("UITreeTestLight");
            lightGo.transform.SetParent(root.transform);
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.2f;
            light.transform.rotation = Quaternion.Euler(40f, -35f, 0f);

            var plane = GameObject.CreatePrimitive(PrimitiveType.Plane);
            plane.transform.SetParent(root.transform);
            plane.transform.localScale = new Vector3(0.5f, 1f, 0.5f);
            plane.GetComponent<Renderer>().sharedMaterial = CreateMaterial(new Color(0.28f, 0.30f, 0.34f), materials);

            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.transform.SetParent(root.transform);
            cube.transform.position = new Vector3(-1.2f, 0.6f, 0f);
            cube.GetComponent<Renderer>().sharedMaterial = CreateMaterial(new Color(0.91f, 0.33f, 0.22f), materials);

            var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphere.transform.SetParent(root.transform);
            sphere.transform.position = new Vector3(1.1f, 0.75f, 0.4f);
            sphere.GetComponent<Renderer>().sharedMaterial = CreateMaterial(new Color(0.23f, 0.58f, 0.93f), materials);

            try
            {
                var handler = new ScreenshotCaptureCameraHandler();
                var response = UITreeTestHelpers.Execute<ScreenshotCaptureCameraRequest, ScreenshotCaptureCameraResponse>(handler, new ScreenshotCaptureCameraRequest
                {
                    cameraName = "UITreeTestCamera",
                    width = 320,
                    height = 180,
                    filename = imagePath,
                    transparent = false
                });

                Assert.That(response.cameraName, Is.EqualTo("UITreeTestCamera"));
                Assert.That(response.width, Is.EqualTo(320));
                Assert.That(response.height, Is.EqualTo(180));
                Assert.That(File.Exists(response.imagePath), Is.True);
                Assert.That(new FileInfo(response.imagePath).Length, Is.GreaterThan(1000));

                var firstTexture = LoadTexture(response.imagePath);
                try
                {
                    var background = firstTexture.GetPixel(10, firstTexture.height - 10);
                    Assert.That(background.r, Is.LessThan(0.15f));
                    Assert.That(background.g, Is.LessThan(0.18f));
                    Assert.That(background.b, Is.LessThan(0.25f));

                    Assert.That(ContainsDominantPixel(firstTexture, 0, firstTexture.width / 2,
                        color => color.r > 0.45f && color.r > color.b + 0.10f), Is.True);
                    Assert.That(ContainsDominantPixel(firstTexture, firstTexture.width / 2, firstTexture.width,
                        color => color.b > 0.45f && color.b > color.r + 0.10f), Is.True);
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(firstTexture);
                }

                cube.transform.position = new Vector3(-0.1f, 1.4f, -0.2f);
                sphere.GetComponent<Renderer>().sharedMaterial.color = new Color(0.95f, 0.78f, 0.25f);

                var changed = UITreeTestHelpers.Execute<ScreenshotCaptureCameraRequest, ScreenshotCaptureCameraResponse>(handler, new ScreenshotCaptureCameraRequest
                {
                    cameraName = "UITreeTestCamera",
                    width = 320,
                    height = 180,
                    filename = changedPath,
                    transparent = false
                });

                Assert.That(File.Exists(changed.imagePath), Is.True);
                Assert.That(File.ReadAllBytes(response.imagePath).SequenceEqual(File.ReadAllBytes(changed.imagePath)), Is.False);

                var originalClearFlags = camera.clearFlags;
                var originalBackground = camera.backgroundColor;
                var transparent = UITreeTestHelpers.Execute<ScreenshotCaptureCameraRequest, ScreenshotCaptureCameraResponse>(handler, new ScreenshotCaptureCameraRequest
                {
                    cameraName = "UITreeTestCamera",
                    width = 320,
                    height = 180,
                    filename = Path.Combine(tempDir, "camera_transparent.png"),
                    transparent = true
                });

                Assert.That(File.Exists(transparent.imagePath), Is.True);
                Assert.That(camera.clearFlags, Is.EqualTo(originalClearFlags));
                Assert.That(camera.backgroundColor, Is.EqualTo(originalBackground));

                var transparentTexture = LoadTexture(transparent.imagePath);
                try
                {
                    var transparentBackground = transparentTexture.GetPixel(5, transparentTexture.height - 5);
                    Assert.That(transparentBackground.a, Is.LessThan(0.05f));
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(transparentTexture);
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
                foreach (var material in materials)
                {
                    if (material != null)
                    {
                        UnityEngine.Object.DestroyImmediate(material);
                    }
                }
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

        private static Material CreateMaterial(Color color, System.Collections.Generic.ICollection<Material> materials)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var material = new Material(shader);
            material.color = color;
            materials.Add(material);
            return material;
        }
    }
}
