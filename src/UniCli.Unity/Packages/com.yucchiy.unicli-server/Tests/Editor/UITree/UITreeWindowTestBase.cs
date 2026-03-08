using NUnit.Framework;

namespace UniCli.Server.Editor.Tests
{
    public abstract class UITreeWindowTestBase
    {
        protected UITreeTestEditorWindow Window;

        [SetUp]
        public virtual void SetUp()
        {
            Window = UITreeTestHelpers.OpenFixtureWindow();
        }

        [TearDown]
        public virtual void TearDown()
        {
            UITreeTestHelpers.CloseFixtureWindows();
        }
    }
}
