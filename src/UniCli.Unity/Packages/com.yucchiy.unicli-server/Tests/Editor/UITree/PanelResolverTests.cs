using System.Threading;
using NUnit.Framework;
using UniCli.Server.Editor.Handlers;

namespace UniCli.Server.Editor.Tests
{
    [TestFixture]
    public class PanelResolverTests : UITreeWindowTestBase
    {
        [Test]
        public void ListOpenPanels_ContainsFixtureWindow()
        {
            var resolver = new PanelResolver();
            var panels = resolver.ListOpenPanels(CancellationToken.None);

            Assert.That(panels.Length, Is.GreaterThan(0));
            Assert.That(System.Array.Exists(panels, p => p.panelInfo.title == UITreeTestEditorWindow.Title), Is.True);
        }

        [Test]
        public void Resolve_ByTitle_ReturnsPanelInfo()
        {
            var resolver = new PanelResolver();
            var panel = resolver.Resolve(UITreeTestEditorWindow.Title, CancellationToken.None);

            Assert.That(panel.panelInfo.title, Is.EqualTo(UITreeTestEditorWindow.Title));
            Assert.That(panel.panelInfo.elementCount, Is.GreaterThan(0));
            Assert.That(panel.root, Is.Not.Null);
        }

        [Test]
        public void Resolve_UnknownPanel_ThrowsCommandFailedException()
        {
            var resolver = new PanelResolver();

            var ex = Assert.Throws<CommandFailedException>(() => resolver.Resolve("PanelDoesNotExist", CancellationToken.None));
            StringAssert.Contains("not found", ex.Message);
        }
    }
}
