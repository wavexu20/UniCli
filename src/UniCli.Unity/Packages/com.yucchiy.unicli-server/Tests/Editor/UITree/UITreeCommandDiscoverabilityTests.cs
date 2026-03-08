using System;
using System.Linq;
using NUnit.Framework;
using UnityEditor;

namespace UniCli.Server.Editor.Tests
{
    [TestFixture]
    public class UITreeCommandDiscoverabilityTests
    {
        [Test]
        public void CommandDispatcher_ContainsAllExpectedCommands()
        {
            var dispatcher = new CommandDispatcher(CreateServiceRegistry());
            var names = dispatcher.GetAllCommandInfo()
                .Select(info => info.name)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            Assert.That(names.Contains("UITree.Dump"), Is.True);
            Assert.That(names.Contains("UITree.Inspect"), Is.True);
            Assert.That(names.Contains("UITree.Click"), Is.True);
            Assert.That(names.Contains("UITree.Fill"), Is.True);
            Assert.That(names.Contains("UITree.Select"), Is.True);
            Assert.That(names.Contains("Screenshot.CaptureEditor"), Is.True);
            Assert.That(names.Contains("Screenshot.CaptureCamera"), Is.True);
        }

        private static ServiceRegistry CreateServiceRegistry()
        {
            var services = new ServiceRegistry();
            var installerTypes = TypeCache.GetTypesDerivedFrom<IServiceInstaller>();
            foreach (var type in installerTypes)
            {
                if (type.IsAbstract || type.IsInterface)
                {
                    continue;
                }

                var installer = (IServiceInstaller)Activator.CreateInstance(type);
                installer.Install(services);
            }

            return services;
        }
    }
}
