using NUnit.Framework;
using UniCli.Server.Editor.Handlers;

namespace UniCli.Server.Editor.Tests
{
    [TestFixture]
    public class UITreeDumpHandlerTests : UITreeWindowTestBase
    {
        [Test]
        public void Execute_WithPanel_DumpsTree()
        {
            var handler = new UITreeDumpHandler();
            var response = UITreeTestHelpers.Execute<UITreeDumpRequest, UITreeDumpResponse>(handler, new UITreeDumpRequest
            {
                panel = UITreeTestEditorWindow.Title,
                depth = 8
            });

            Assert.That(response.panelInfo, Is.Not.Null);
            Assert.That(response.lines, Is.Not.Empty);
            Assert.That(response.lines[0], Does.Contain("VisualElement"));
        }

        [Test]
        public void Execute_WithoutPanel_ListsPanels()
        {
            var handler = new UITreeDumpHandler();
            var response = UITreeTestHelpers.Execute<UITreeDumpRequest, UITreeDumpResponse>(handler, new UITreeDumpRequest());

            Assert.That(response.panelInfo, Is.Null);
            Assert.That(response.lines, Is.Not.Empty);
            Assert.That(string.Join("\n", response.lines), Does.Contain(UITreeTestEditorWindow.Title));
        }

        [Test]
        public void Execute_WithFilter_ReturnsMatchedCount()
        {
            var handler = new UITreeDumpHandler();
            var response = UITreeTestHelpers.Execute<UITreeDumpRequest, UITreeDumpResponse>(handler, new UITreeDumpRequest
            {
                panel = UITreeTestEditorWindow.Title,
                filter = "#run-button"
            });

            Assert.That(response.matchedCount, Is.EqualTo(1));
            Assert.That(response.lines.Length, Is.EqualTo(1));
        }
    }
}
