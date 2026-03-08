using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace UniCli.Server.Editor.Handlers
{
    public sealed class ScreenshotCaptureCameraHandler : CommandHandler<ScreenshotCaptureCameraRequest, ScreenshotCaptureCameraResponse>
    {
        private readonly CameraCapture _capture = new();

        public override string CommandName => "Screenshot.CaptureCamera";
        public override string Description => "Capture screenshot from a scene camera render";

        protected override bool TryWriteFormatted(ScreenshotCaptureCameraResponse response, bool success, IFormatWriter writer)
        {
            if (!success)
            {
                writer.WriteLine("Camera screenshot capture failed.");
                return true;
            }

            writer.WriteLine($"Camera: {response.cameraName}");
            writer.WriteLine($"Image: {response.imagePath}");
            writer.WriteLine($"Size: {response.width}x{response.height}");
            return true;
        }

        protected override ValueTask<ScreenshotCaptureCameraResponse> ExecuteAsync(ScreenshotCaptureCameraRequest request, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(request.cameraName))
            {
                throw new CommandFailedException("cameraName is required.", new UITreeErrorResponse
                {
                    error = "cameraName is required.",
                    candidates = Array.Empty<string>()
                });
            }

            if (request.width <= 0 || request.height <= 0)
            {
                throw new CommandFailedException("Invalid resolution.", new UITreeErrorResponse
                {
                    error = "Invalid resolution.",
                    candidates = Array.Empty<string>()
                });
            }

            var camera = ResolveCamera(request.cameraName);
            if (camera == null)
            {
                throw new CommandFailedException($"Camera '{request.cameraName}' not found.", new UITreeErrorResponse
                {
                    error = $"Camera '{request.cameraName}' not found.",
                    candidates = Resources.FindObjectsOfTypeAll<Camera>()
                        .Where(c => c != null && c.gameObject != null)
                        .Take(20)
                        .Select(c => c.name)
                        .ToArray()
                });
            }

            var outputPath = ResolvePath(request.filename);
            if (string.IsNullOrWhiteSpace(outputPath))
            {
                outputPath = ResolvePath(Path.Combine("Screenshots",
                    $"camera_{SanitizeName(camera.name)}_{DateTime.Now:yyyyMMdd_HHmmss}.png"));
            }

            CameraCapture.CaptureResult captureResult;
            try
            {
                captureResult = _capture.Capture(camera, request.width, request.height, request.transparent, outputPath, cancellationToken);
            }
            catch (Exception ex)
            {
                throw new CommandFailedException(ex.Message, new UITreeErrorResponse
                {
                    error = ex.Message,
                    candidates = new[] { camera.name }
                });
            }

            return new ValueTask<ScreenshotCaptureCameraResponse>(new ScreenshotCaptureCameraResponse
            {
                imagePath = captureResult.imagePath,
                cameraName = camera.name,
                width = captureResult.width,
                height = captureResult.height
            });
        }

        private static Camera ResolveCamera(string cameraName)
        {
            var cameras = Resources.FindObjectsOfTypeAll<Camera>()
                .Where(c => c != null && c.gameObject != null && c.gameObject.scene.IsValid())
                .ToArray();

            var exact = cameras.FirstOrDefault(c => string.Equals(c.name, cameraName, StringComparison.Ordinal));
            if (exact != null)
            {
                return exact;
            }

            var ignoreCase = cameras.FirstOrDefault(c => string.Equals(c.name, cameraName, StringComparison.OrdinalIgnoreCase));
            if (ignoreCase != null)
            {
                return ignoreCase;
            }

            return cameras.FirstOrDefault(c => c.name.IndexOf(cameraName, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private static string SanitizeName(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return "camera";
            }

            foreach (var invalid in Path.GetInvalidFileNameChars())
            {
                value = value.Replace(invalid, '_');
            }

            return value;
        }
    }
}
