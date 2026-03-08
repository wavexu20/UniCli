using System.Linq;
using System.Threading;
using NUnit.Framework;
using UniCli.Server.Editor.Handlers;

namespace UniCli.Server.Editor.Tests
{
    [TestFixture]
    public class TreeSerializerTests : UITreeWindowTestBase
    {
        [Test]
        public void Serialize_DefaultDepth_IncludesFixtureElements()
        {
            var serializer = new TreeSerializer(new ElementResolver());
            var result = serializer.Serialize(Window.rootVisualElement, 10, null, false, CancellationToken.None);

            Assert.That(result.lines.Length, Is.GreaterThan(1));
            Assert.That(result.lines.Any(line => line.Contains("#run-button")), Is.True);
            Assert.That(result.lines.Any(line => line.Contains("#name-field")), Is.True);
        }

        [Test]
        public void Serialize_DepthZero_ReturnsOnlyRootLine()
        {
            var serializer = new TreeSerializer(new ElementResolver());
            var result = serializer.Serialize(Window.rootVisualElement, 0, null, false, CancellationToken.None);

            Assert.That(result.lines.Length, Is.EqualTo(1));
        }

        [Test]
        public void Serialize_Filter_ReturnsMatchedCount()
        {
            var serializer = new TreeSerializer(new ElementResolver());
            var result = serializer.Serialize(Window.rootVisualElement, 10, "#run-button", false, CancellationToken.None);

            Assert.That(result.matchedCount, Is.EqualTo(1));
            Assert.That(result.lines.Length, Is.EqualTo(1));
            Assert.That(result.lines[0], Does.Contain("#run-button"));
        }
    }
}
