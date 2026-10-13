using System;
using ColInt.MMD.Graphics.Helpers;
using ColInt.MMD.Graphics.Systems;
using ColInt.MMD.Presentation.Systems;
using NUnit.Framework;
using Unity.Entities;

namespace ColInt.MMD.Tests {
	public class MmdGraphicsInstallerTests {
		[Test]
		public void RejectsHostWithoutPrefabLoadingBeforeInstallingSystems() {
			using (var world = new World("MMD Invalid Graphics Host")) {
				var parent = world.GetOrCreateSystemManaged<PresentationSystemGroup>();
				Assert.Throws<InvalidOperationException>(() => MmdGraphicsInstaller.Install(world, parent));
				Assert.That(parent.ManagedSystems.Count, Is.Zero);
				Assert.That(world.GetExistingSystemManaged<MmdPresentationGroup>(), Is.Null);
				Assert.That(world.GetExistingSystemManaged<MmdSkinningGroup>(), Is.Null);
			}
		}
	}
}
