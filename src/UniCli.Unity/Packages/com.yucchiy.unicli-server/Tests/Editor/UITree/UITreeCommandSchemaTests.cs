using System;
using System.Linq;
using NUnit.Framework;
using UniCli.Protocol;
using UniCli.Server.Editor.Handlers;

namespace UniCli.Server.Editor.Tests
{
    [TestFixture]
    public class UITreeCommandSchemaTests
    {
        [Test]
        public void UITreeDump_Schema_IsStable()
        {
            var info = new UITreeDumpHandler().GetCommandInfo();
            AssertFieldNames(info.requestFields, "panel", "depth", "filter", "includeUnityClasses");
            AssertFieldNames(info.responseFields, "panelInfo", "lines", "matchedCount");
        }

        [Test]
        public void UITreeInspect_Schema_IsStable()
        {
            var info = new UITreeInspectHandler().GetCommandInfo();
            AssertFieldNames(info.requestFields, "panel", "selector", "includeResolvedStyle", "includeBindingInfo");
            AssertFieldNames(info.responseFields, "element", "layout", "content", "resolvedStyle", "binding");
        }

        [Test]
        public void UITreeClick_Schema_IsStable()
        {
            var info = new UITreeClickHandler().GetCommandInfo();
            AssertFieldNames(info.requestFields, "panel", "selector");
            AssertFieldNames(info.responseFields, "target", "result", "sideEffects");
        }

        [Test]
        public void UITreeFill_Schema_IsStable()
        {
            var info = new UITreeFillHandler().GetCommandInfo();
            AssertFieldNames(info.requestFields, "panel", "selector", "value");
            AssertFieldNames(info.responseFields, "target", "oldValue", "newValue", "sideEffects");
        }

        [Test]
        public void UITreeSelect_Schema_IsStable()
        {
            var info = new UITreeSelectHandler().GetCommandInfo();
            AssertFieldNames(info.requestFields, "panel", "selector", "choice");
            AssertFieldNames(info.responseFields, "target", "oldValue", "newValue", "choices", "sideEffects");
        }

        [Test]
        public void ScreenshotCaptureEditor_Schema_IsStable()
        {
            var info = new ScreenshotCaptureEditorHandler().GetCommandInfo();
            AssertFieldNames(info.requestFields, "panel", "selector", "filename", "diffBase");
            AssertFieldNames(info.responseFields, "imagePath", "width", "height", "diff");
        }

        [Test]
        public void ScreenshotCaptureCamera_Schema_IsStable()
        {
            var info = new ScreenshotCaptureCameraHandler().GetCommandInfo();
            AssertFieldNames(info.requestFields, "cameraName", "width", "height", "filename", "transparent");
            AssertFieldNames(info.responseFields, "imagePath", "cameraName", "width", "height");
        }

        private static void AssertFieldNames(CommandFieldInfo[] fields, params string[] expected)
        {
            var names = fields.Select(x => x.name).ToArray();
            CollectionAssert.AreEqual(expected, names);
        }
    }
}
