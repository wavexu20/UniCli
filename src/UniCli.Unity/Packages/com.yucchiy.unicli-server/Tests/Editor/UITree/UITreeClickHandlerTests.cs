using NUnit.Framework;
using UniCli.Server.Editor.Handlers;

namespace UniCli.Server.Editor.Tests
{
    [TestFixture]
    public class UITreeClickHandlerTests : UITreeWindowTestBase
    {
        [Test]
        public void Execute_ClickButton_UpdatesState()
        {
            var handler = new UITreeClickHandler();
            var response = UITreeTestHelpers.Execute<UITreeClickRequest, UITreeClickResponse>(handler, new UITreeClickRequest
            {
                panel = UITreeTestEditorWindow.Title,
                selector = "#run-button"
            });

            Assert.That(response.result, Is.EqualTo("success"));
            Assert.That(Window.clickCount, Is.EqualTo(1));
            Assert.That(response.sideEffects.Length, Is.GreaterThan(0));
            Assert.That(Window.StatusText, Does.Contain("clicked:1"));
        }
    }
}
