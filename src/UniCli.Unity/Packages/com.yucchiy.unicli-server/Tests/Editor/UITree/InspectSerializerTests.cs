using System.Linq;
using System.Threading;
using NUnit.Framework;
using UniCli.Server.Editor.Handlers;

namespace UniCli.Server.Editor.Tests
{
    [TestFixture]
    public class InspectSerializerTests : UITreeWindowTestBase
    {
        [Test]
        public void Serialize_WithResolvedStyleAndBinding_ReturnsExpectedFields()
        {
            var elementResolver = new ElementResolver();
            var element = elementResolver.ResolveSingle(Window.rootVisualElement, "#name-field", CancellationToken.None);
            var serializer = new InspectSerializer();

            var response = serializer.Serialize(element, true, true);

            Assert.That(response.element.type, Does.Contain("TextField"));
            Assert.That(response.content.value, Is.EqualTo("Initial"));
            Assert.That(response.resolvedStyle.Length, Is.GreaterThan(0));
            Assert.That(response.binding.bindingPath, Is.EqualTo("fixture.name"));
            Assert.That(response.binding.hasDataSource, Is.True);
        }

        [Test]
        public void Serialize_WithoutResolvedStyle_ReturnsEmptyStyle()
        {
            var elementResolver = new ElementResolver();
            var element = elementResolver.ResolveSingle(Window.rootVisualElement, "#name-field", CancellationToken.None);
            var serializer = new InspectSerializer();

            var response = serializer.Serialize(element, false, false);

            Assert.That(response.resolvedStyle, Is.Empty);
            Assert.That(response.binding, Is.Null);
            Assert.That(response.element.classes.Contains("editable"), Is.True);
        }
    }
}
