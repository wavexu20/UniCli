using NUnit.Framework;
using UniCli.Server.Editor.Handlers;

namespace UniCli.Server.Editor.Tests
{
    [TestFixture]
    public class UITreeFillHandlerTests : UITreeWindowTestBase
    {
        [Test]
        public void Execute_TextField_UpdatesValueAndSideEffects()
        {
            var handler = new UITreeFillHandler();
            var response = UITreeTestHelpers.Execute<UITreeFillRequest, UITreeFillResponse>(handler, new UITreeFillRequest
            {
                panel = UITreeTestEditorWindow.Title,
                selector = "#name-field",
                value = "Updated"
            });

            Assert.That(response.oldValue, Is.EqualTo("Initial"));
            Assert.That(response.newValue, Is.EqualTo("Updated"));
            Assert.That(Window.lastTextValue, Is.EqualTo("Updated"));
            Assert.That(Window.fillChangeCount, Is.GreaterThan(0));
            Assert.That(response.sideEffects.Length, Is.GreaterThan(0));
        }

        [Test]
        public void Execute_IntegerField_WithInvalidValue_ThrowsCommandFailedException()
        {
            var handler = new UITreeFillHandler();

            var ex = Assert.Throws<CommandFailedException>(() =>
                UITreeTestHelpers.Execute<UITreeFillRequest, UITreeFillResponse>(handler, new UITreeFillRequest
                {
                    panel = UITreeTestEditorWindow.Title,
                    selector = "#count-field",
                    value = "not-a-number"
                }));

            StringAssert.Contains("Cannot convert", ex.Message);
        }
    }
}
