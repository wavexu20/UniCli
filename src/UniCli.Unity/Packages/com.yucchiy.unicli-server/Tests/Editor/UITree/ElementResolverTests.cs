using System.Threading;
using NUnit.Framework;
using UniCli.Server.Editor.Handlers;

namespace UniCli.Server.Editor.Tests
{
    [TestFixture]
    public class ElementResolverTests : UITreeWindowTestBase
    {
        [Test]
        public void Query_ByNameAndClass_ReturnsExpectedElement()
        {
            var resolver = new ElementResolver();
            var matches = resolver.Query(Window.rootVisualElement, "TextField#name-field.editable", CancellationToken.None);

            Assert.That(matches.Count, Is.EqualTo(1));
            Assert.That(matches[0].name, Is.EqualTo("name-field"));
        }

        [Test]
        public void ResolveSingle_WithMultipleMatches_Throws()
        {
            var resolver = new ElementResolver();

            var ex = Assert.Throws<CommandFailedException>(() => resolver.ResolveSingle(Window.rootVisualElement, ".editable", CancellationToken.None));
            StringAssert.Contains("multiple", ex.Message);
        }

        [Test]
        public void ResolveSingle_WithNoMatch_Throws()
        {
            var resolver = new ElementResolver();

            var ex = Assert.Throws<CommandFailedException>(() => resolver.ResolveSingle(Window.rootVisualElement, "#does-not-exist", CancellationToken.None));
            StringAssert.Contains("No element matched", ex.Message);
        }
    }
}
