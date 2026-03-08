using NUnit.Framework;
using UniCli.Server.Editor.Handlers;

namespace UniCli.Server.Editor.Tests
{
    [TestFixture]
    public class UITreeSelectHandlerTests : UITreeWindowTestBase
    {
        [Test]
        public void Execute_Dropdown_SelectsChoice()
        {
            var handler = new UITreeSelectHandler();
            var response = UITreeTestHelpers.Execute<UITreeSelectRequest, UITreeSelectResponse>(handler, new UITreeSelectRequest
            {
                panel = UITreeTestEditorWindow.Title,
                selector = "#mode-dropdown",
                choice = "Beta"
            });

            Assert.That(response.oldValue, Is.EqualTo("Alpha"));
            Assert.That(response.newValue, Is.EqualTo("Beta"));
            Assert.That(Window.lastChoice, Is.EqualTo("Beta"));
            Assert.That(response.choices, Has.Member("Gamma"));
            Assert.That(response.sideEffects.Length, Is.GreaterThan(0));
        }

        [Test]
        public void Execute_WithInvalidChoice_ThrowsCommandFailedException()
        {
            var handler = new UITreeSelectHandler();

            var ex = Assert.Throws<CommandFailedException>(() =>
                UITreeTestHelpers.Execute<UITreeSelectRequest, UITreeSelectResponse>(handler, new UITreeSelectRequest
                {
                    panel = UITreeTestEditorWindow.Title,
                    selector = "#mode-dropdown",
                    choice = "UnknownChoice"
                }));

            StringAssert.Contains("does not exist", ex.Message);
        }
    }
}
