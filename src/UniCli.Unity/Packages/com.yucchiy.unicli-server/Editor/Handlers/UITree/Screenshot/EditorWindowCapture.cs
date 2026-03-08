using System;
using System.IO;
using System.Reflection;
using System.Threading;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.UIElements;

namespace UniCli.Server.Editor.Handlers
{
    internal sealed class EditorWindowCapture
    {
        private static readonly BindingFlags InstanceMembers = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private const RenderTextureReadWrite CaptureReadWrite = RenderTextureReadWrite.Linear;

        internal sealed class CaptureResult
        {
            public string imagePath;
            public int width;
            public int height;
        }

        public CaptureResult Capture(EditorWindow window, VisualElement root, Rect? elementRect, string outputPath, CancellationToken cancellationToken)
        {
            if (window == null)
            {
                throw new InvalidOperationException("Editor window is null.");
            }

            cancellationToken.ThrowIfCancellationRequested();

            window.Focus();
            window.Repaint();
            root?.MarkDirtyRepaint();
            InternalEditorUtility.RepaintAllViews();
            RepaintImmediately(window);

            Texture2D sourceTexture = null;
            Texture2D outputTexture = null;
            try
            {
                sourceTexture = CaptureSourceTexture(window, root, cancellationToken);

                outputTexture = sourceTexture;
                if (elementRect.HasValue)
                {
                    var cropRect = ToTextureRect(elementRect.Value, root != null ? root.worldBound.position : Vector2.zero, sourceTexture.width, sourceTexture.height);
                    outputTexture = CropTexture(sourceTexture, cropRect);
                }

                var pngBytes = outputTexture.EncodeToPNG();
                EnsureDirectory(outputPath);
                File.WriteAllBytes(outputPath, pngBytes);

                var fullPath = Path.GetFullPath(outputPath);
                return new CaptureResult
                {
                    imagePath = fullPath,
                    width = outputTexture.width,
                    height = outputTexture.height
                };
            }
            finally
            {
                if (outputTexture != null && !ReferenceEquals(outputTexture, sourceTexture))
                {
                    UnityEngine.Object.DestroyImmediate(outputTexture);
                }

                if (sourceTexture != null)
                {
                    UnityEngine.Object.DestroyImmediate(sourceTexture);
                }
            }
        }

        private static Texture2D CaptureSourceTexture(EditorWindow window, VisualElement root, CancellationToken cancellationToken)
        {
            Exception panelCaptureException = null;
            if (root != null && root.panel != null)
            {
                try
                {
                    return CapturePanelTexture(root, cancellationToken);
                }
                catch (Exception ex) when (!(ex is OperationCanceledException))
                {
                    panelCaptureException = ex;
                }
            }

            return CaptureLegacyWindowTexture(window, root, cancellationToken, panelCaptureException);
        }

        private static void RepaintImmediately(EditorWindow window)
        {
            var parentField = typeof(EditorWindow).GetField("m_Parent", InstanceMembers);
            var parent = parentField != null ? parentField.GetValue(window) : null;
            if (parent == null)
            {
                return;
            }

            var repaintImmediately = parent.GetType().GetMethod("RepaintImmediately", InstanceMembers, null, Type.EmptyTypes, null);
            repaintImmediately?.Invoke(parent, null);
        }

        private static Texture2D CapturePanelTexture(VisualElement root, CancellationToken cancellationToken)
        {
            if (root == null)
            {
                throw new InvalidOperationException("Editor window root visual element is missing.");
            }

            var panel = root.panel;
            if (panel == null)
            {
                throw new InvalidOperationException("Editor window panel is not available for capture.");
            }

            var sourceBounds = root.worldBound;
            var captureWidth = Math.Max(1, Mathf.RoundToInt(sourceBounds.width));
            var captureHeight = Math.Max(1, Mathf.RoundToInt(sourceBounds.height));

            var renderTexture = new RenderTexture(captureWidth, captureHeight, 24, RenderTextureFormat.ARGB32, CaptureReadWrite);
            var previousRenderTexture = RenderTexture.active;

            try
            {
                cancellationToken.ThrowIfCancellationRequested();

                RenderTexture.active = renderTexture;
                GL.Clear(true, true, Color.clear);

                InvokePanelMethod(panel, "UpdateForRepaint");
                InvokePanelRepaint(panel);
                InvokePanelMethod(panel, "Render");

                var texture = new Texture2D(captureWidth, captureHeight, TextureFormat.RGBA32, false);
                texture.ReadPixels(new Rect(0f, 0f, captureWidth, captureHeight), 0, 0);
                texture.Apply();
                return texture;
            }
            catch (TargetInvocationException ex) when (ex.InnerException != null)
            {
                throw new InvalidOperationException($"Panel capture failed: {ex.InnerException.Message}", ex.InnerException);
            }
            finally
            {
                RenderTexture.active = previousRenderTexture;
                renderTexture.Release();
                UnityEngine.Object.DestroyImmediate(renderTexture);
            }
        }

        private static Texture2D CaptureLegacyWindowTexture(EditorWindow window, VisualElement root, CancellationToken cancellationToken, Exception panelCaptureException)
        {
            var sourceBounds = root != null ? root.worldBound : window.position;
            var captureWidth = Math.Max(1, Mathf.RoundToInt(sourceBounds.width));
            var captureHeight = Math.Max(1, Mathf.RoundToInt(sourceBounds.height));
            var renderTexture = new RenderTexture(captureWidth, captureHeight, 24, RenderTextureFormat.ARGB32, CaptureReadWrite);
            var previousRenderTexture = RenderTexture.active;

            try
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (!InternalEditorUtility.CaptureEditorWindow(window, renderTexture))
                {
                    var panelMessage = panelCaptureException != null
                        ? $" Panel capture also failed: {panelCaptureException.Message}"
                        : string.Empty;
                    throw new InvalidOperationException($"Failed to capture the editor window via HostView.{panelMessage}");
                }

                RenderTexture.active = renderTexture;
                var texture = new Texture2D(captureWidth, captureHeight, TextureFormat.RGBA32, false);
                texture.ReadPixels(new Rect(0f, 0f, captureWidth, captureHeight), 0, 0);
                texture.Apply();
                return texture;
            }
            finally
            {
                RenderTexture.active = previousRenderTexture;
                renderTexture.Release();
                UnityEngine.Object.DestroyImmediate(renderTexture);
            }
        }

        private static void InvokePanelMethod(object panel, string methodName)
        {
            var method = panel.GetType().GetMethod(methodName, InstanceMembers, null, Type.EmptyTypes, null);
            if (method == null)
            {
                throw new InvalidOperationException($"Panel method '{methodName}' was not found.");
            }

            method.Invoke(panel, null);
        }

        private static void InvokePanelRepaint(object panel)
        {
            var repaintMethod = panel.GetType().GetMethod("Repaint", InstanceMembers, null, new[] { typeof(Event) }, null);
            if (repaintMethod == null)
            {
                throw new InvalidOperationException("Panel repaint method was not found.");
            }

            var repaintEvent = new Event { type = EventType.Repaint };
            repaintMethod.Invoke(panel, new object[] { repaintEvent });
        }

        private static RectInt ToTextureRect(Rect panelRect, Vector2 captureOrigin, int textureWidth, int textureHeight)
        {
            var x = Mathf.RoundToInt(panelRect.x - captureOrigin.x);
            var yTop = Mathf.RoundToInt(panelRect.y - captureOrigin.y);
            var width = Math.Max(1, Mathf.RoundToInt(panelRect.width));
            var height = Math.Max(1, Mathf.RoundToInt(panelRect.height));

            x = Mathf.Clamp(x, 0, Math.Max(0, textureWidth - 1));
            yTop = Mathf.Clamp(yTop, 0, Math.Max(0, textureHeight - 1));
            width = Mathf.Clamp(width, 1, textureWidth - x);
            height = Mathf.Clamp(height, 1, textureHeight - yTop);

            var y = textureHeight - yTop - height;
            y = Mathf.Clamp(y, 0, Math.Max(0, textureHeight - height));

            return new RectInt(x, y, width, height);
        }

        private static Texture2D CropTexture(Texture2D source, RectInt rect)
        {
            var pixels = source.GetPixels(rect.x, rect.y, rect.width, rect.height);
            var cropped = new Texture2D(rect.width, rect.height, TextureFormat.RGBA32, false);
            cropped.SetPixels(pixels);
            cropped.Apply();
            return cropped;
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
