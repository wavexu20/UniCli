using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace UniCli.Server.Editor.Handlers
{
    public sealed class ScreenshotCaptureEditorHandler : CommandHandler<ScreenshotCaptureEditorRequest, ScreenshotCaptureEditorResponse>
    {
        private readonly PanelResolver _panelResolver = new();
        private readonly ElementResolver _elementResolver = new();
        private readonly EditorWindowCapture _capture = new();
        private readonly PixelDiff _pixelDiff = new();

        public override string CommandName => "Screenshot.CaptureEditor";
        public override string Description => "Capture screenshot for an Editor panel (optionally cropped by selector)";

        protected override bool TryWriteFormatted(ScreenshotCaptureEditorResponse response, bool success, IFormatWriter writer)
        {
            if (!success)
            {
                writer.WriteLine("Editor screenshot capture failed.");
                return true;
            }

            writer.WriteLine($"Image: {response.imagePath}");
            writer.WriteLine($"Size: {response.width}x{response.height}");
            if (response.diff != null)
            {
                writer.WriteLine($"Diff: {response.diff.changedPixels} changed pixels ({response.diff.percentage:F3}%)");
                writer.WriteLine($"Diff image: {response.diff.diffImagePath}");
            }

            return true;
        }

        protected override ValueTask<ScreenshotCaptureEditorResponse> ExecuteAsync(ScreenshotCaptureEditorRequest request, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(request.panel))
            {
                throw new CommandFailedException("panel is required.", new UITreeErrorResponse
                {
                    error = "panel is required.",
                    candidates = Array.Empty<string>()
                });
            }

            var panel = _panelResolver.Resolve(request.panel, cancellationToken);

            Rect? cropRect = null;
            if (!string.IsNullOrWhiteSpace(request.selector))
            {
                var element = _elementResolver.ResolveSingle(panel.root, request.selector, cancellationToken);
                cropRect = element.worldBound;
            }

            var outputPath = ResolvePath(request.filename);
            if (string.IsNullOrWhiteSpace(outputPath))
            {
                outputPath = ResolvePath(Path.Combine("Screenshots",
                    $"editor_{SanitizeName(panel.window.GetType().Name)}_{DateTime.Now:yyyyMMdd_HHmmss}.png"));
            }

            EditorWindowCapture.CaptureResult captureResult;
            try
            {
                captureResult = _capture.Capture(panel.window, panel.root, cropRect, outputPath, cancellationToken);
            }
            catch (Exception ex)
            {
                throw new CommandFailedException(ex.Message, new UITreeErrorResponse
                {
                    error = ex.Message,
                    candidates = new[] { panel.panelInfo.name }
                });
            }

            ScreenshotDiffInfo diff = null;
            if (!string.IsNullOrWhiteSpace(request.diffBase))
            {
                var baselinePath = ResolvePath(request.diffBase);
                var diffPath = BuildDiffPath(captureResult.imagePath);

                try
                {
                    diff = _pixelDiff.Compute(baselinePath, captureResult.imagePath, diffPath, 24, cancellationToken);
                }
                catch (Exception ex)
                {
                    throw new CommandFailedException(ex.Message, new UITreeErrorResponse
                    {
                        error = ex.Message,
                        candidates = new[] { baselinePath, captureResult.imagePath }
                    });
                }
            }

            return new ValueTask<ScreenshotCaptureEditorResponse>(new ScreenshotCaptureEditorResponse
            {
                imagePath = captureResult.imagePath,
                width = captureResult.width,
                height = captureResult.height,
                diff = diff
            });
        }

        private static string BuildDiffPath(string imagePath)
        {
            var directory = Path.GetDirectoryName(imagePath) ?? string.Empty;
            var fileName = Path.GetFileNameWithoutExtension(imagePath);
            return Path.Combine(directory, fileName + "_diff.png");
        }

        private static string SanitizeName(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return "panel";
            }

            foreach (var invalid in Path.GetInvalidFileNameChars())
            {
                value = value.Replace(invalid, '_');
            }

            return value;
        }
    }
}
