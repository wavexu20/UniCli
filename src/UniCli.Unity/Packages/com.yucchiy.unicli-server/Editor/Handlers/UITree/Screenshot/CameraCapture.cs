using System;
using System.IO;
using System.Threading;
using UnityEngine;

namespace UniCli.Server.Editor.Handlers
{
    internal sealed class CameraCapture
    {
        private static RenderTextureReadWrite CaptureReadWrite =>
            QualitySettings.activeColorSpace == ColorSpace.Linear
                ? RenderTextureReadWrite.Linear
                : RenderTextureReadWrite.Default;

        internal sealed class CaptureResult
        {
            public string imagePath;
            public int width;
            public int height;
        }

        public CaptureResult Capture(Camera camera, int width, int height, bool transparent, string outputPath, CancellationToken cancellationToken)
        {
            if (camera == null)
            {
                throw new InvalidOperationException("Camera not found.");
            }

            if (width <= 0 || height <= 0)
            {
                throw new InvalidOperationException("Invalid resolution.");
            }

            cancellationToken.ThrowIfCancellationRequested();

            var renderTexture = RenderTexture.GetTemporary(width, height, 24, RenderTextureFormat.ARGB32, CaptureReadWrite);
            var previousTargetTexture = camera.targetTexture;
            var previousActiveTexture = RenderTexture.active;
            var previousClearFlags = camera.clearFlags;
            var previousBackgroundColor = camera.backgroundColor;

            Texture2D texture = null;
            try
            {
                if (transparent)
                {
                    camera.clearFlags = CameraClearFlags.SolidColor;
                    camera.backgroundColor = new Color(0f, 0f, 0f, 0f);
                }

                camera.targetTexture = renderTexture;
                camera.Render();

                RenderTexture.active = renderTexture;
                texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
                texture.ReadPixels(new Rect(0f, 0f, width, height), 0, 0);
                texture.Apply();

                var pngBytes = texture.EncodeToPNG();
                EnsureDirectory(outputPath);
                File.WriteAllBytes(outputPath, pngBytes);

                return new CaptureResult
                {
                    imagePath = Path.GetFullPath(outputPath),
                    width = width,
                    height = height
                };
            }
            finally
            {
                camera.targetTexture = previousTargetTexture;
                camera.clearFlags = previousClearFlags;
                camera.backgroundColor = previousBackgroundColor;
                RenderTexture.active = previousActiveTexture;

                if (texture != null)
                {
                    UnityEngine.Object.DestroyImmediate(texture);
                }

                RenderTexture.ReleaseTemporary(renderTexture);
            }
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
