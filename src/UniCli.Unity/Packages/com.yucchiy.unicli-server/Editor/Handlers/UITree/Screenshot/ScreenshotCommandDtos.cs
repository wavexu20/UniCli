using System;

namespace UniCli.Server.Editor.Handlers
{
    [Serializable]
    public class ScreenshotCaptureEditorRequest
    {
        public string panel;
        public string selector;
        public string filename;
        public string diffBase;
    }

    [Serializable]
    public class ScreenshotCaptureEditorResponse
    {
        public string imagePath;
        public int width;
        public int height;
        public ScreenshotDiffInfo diff;
    }

    [Serializable]
    public class ScreenshotCaptureCameraRequest
    {
        public string cameraName;
        public int width = 1920;
        public int height = 1080;
        public string filename;
        public bool transparent;
    }

    [Serializable]
    public class ScreenshotCaptureCameraResponse
    {
        public string imagePath;
        public string cameraName;
        public int width;
        public int height;
    }

    [Serializable]
    public class ScreenshotDiffInfo
    {
        public int changedPixels;
        public float percentage;
        public string diffImagePath;
        public ScreenshotChangedRegion[] changedRegions;
    }

    [Serializable]
    public class ScreenshotChangedRegion
    {
        public int x;
        public int y;
        public int width;
        public int height;
    }
}
