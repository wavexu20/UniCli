using System.Linq;
using System.Threading;
using NUnit.Framework;
using UniCli.Server.Editor.Handlers;

namespace UniCli.Server.Editor.Tests
{
    [TestFixture]
    public class SideEffectTrackerTests : UITreeWindowTestBase
    {
        [Test]
        public void Diff_WhenFieldChanges_ReturnsChangedEntry()
        {
            var resolver = new ElementResolver();
            var field = resolver.ResolveSingle(Window.rootVisualElement, "#name-field", CancellationToken.None);
            var tracker = new SideEffectTracker();

            var before = tracker.Capture(Window.rootVisualElement, 1024, CancellationToken.None);
            ElementIntrospection.TrySetValueFromString(field, "Changed", out _, out _, out _);
            var after = tracker.Capture(Window.rootVisualElement, 1024, CancellationToken.None);

            var diff = tracker.Diff(before, after, 64);
            Assert.That(diff.Length, Is.GreaterThan(0));
            Assert.That(diff.Any(entry => entry.summary.Contains("#name-field")), Is.True);
        }
    }
}
