using System;
using System.Threading;
using NUnit.Framework;
using UniCli.Server.Editor.Handlers;

namespace UniCli.Server.Editor.Tests
{
    [TestFixture]
    public class UITreeCancellationTests : UITreeWindowTestBase
    {
        [Test]
        public void Dump_WithCancelledToken_ThrowsOperationCanceledException()
        {
            var serializer = new TreeSerializer(new ElementResolver());
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            Assert.Throws<OperationCanceledException>(() =>
                serializer.Serialize(Window.rootVisualElement, -1, null, false, cts.Token));
        }
    }
}
