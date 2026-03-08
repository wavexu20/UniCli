using NUnit.Framework;
using UniCli.Server.Editor.Handlers;

namespace UniCli.Server.Editor.Tests
{
    [TestFixture]
    public class UITreeInspectHandlerTests : UITreeWindowTestBase
    {
        [Test]
        public void Execute_WithSingleMatch_ReturnsElementDetails()
        {
            var handler = new UITreeInspectHandler();
            var response = UITreeTestHelpers.Execute<UITreeInspectRequest, UITreeInspectResponse>(handler, new UITreeInspectRequest
            {
                panel = UITreeTestEditorWindow.Title,
                selector = "#name-field",
                includeResolvedStyle = true,
                includeBindingInfo = true
            });

            Assert.That(response.element.type, Does.Contain("TextField"));
            Assert.That(float.IsNaN(response.layout.worldBound.width), Is.False);
            Assert.That(response.layout.worldBound.width, Is.GreaterThanOrEqualTo(0));
            Assert.That(response.resolvedStyle, Is.Not.Empty);
            Assert.That(response.binding.bindingPath, Is.EqualTo("fixture.name"));
        }

        [Test]
        public void Execute_WithMultipleMatches_ThrowsCommandFailedException()
        {
            var handler = new UITreeInspectHandler();

            var ex = Assert.Throws<CommandFailedException>(() =>
                UITreeTestHelpers.Execute<UITreeInspectRequest, UITreeInspectResponse>(handler, new UITreeInspectRequest
                {
                    panel = UITreeTestEditorWindow.Title,
                    selector = ".editable"
                }));

            StringAssert.Contains("multiple", ex.Message);
        }
    }
}
