using System;
using ColInt.MMD.Lifecycle.Components;
using ColInt.MMD.Lifecycle.Helpers;
using ColInt.MMD.Playback.Systems;
using NUnit.Framework;
using Unity.Entities;

namespace ColInt.MMD.Tests {
	public class MmdSystemInstallerTests {
		[Test]
		public void InstallsIntoHostGroupWithoutCreatingWorldOrOwningState() {
			using (var host = new World("MMD Host Fixture")) {
				var parent = host.GetOrCreateSystemManaged<FixedStepSimulationSystemGroup>();
				var defaultWorld = World.DefaultGameObjectInjectionWorld;
				int worldCount = World.All.Count;
				var rateManager = parent.RateManager;
				MmdSystemInstaller.Install(host, parent);
				var simulation = host.GetExistingSystemManaged<MmdSimulationGroup>();
				MmdSystemInstaller.Install(host, parent);

				Assert.That(World.All.Count, Is.EqualTo(worldCount));
				Assert.That(World.DefaultGameObjectInjectionWorld, Is.SameAs(defaultWorld));
				Assert.That(parent.RateManager, Is.SameAs(rateManager));
				Assert.That(parent.ManagedSystems.Count, Is.EqualTo(1));
				Assert.That(parent.ManagedSystems[0], Is.SameAs(simulation));
				Assert.That(simulation.ManagedSystems.Count, Is.EqualTo(5));
				Assert.That(simulation.ManagedSystems[2], Is.TypeOf<MmdPlaybackGroup>());
				Assert.That(simulation.ManagedSystems[3], Is.TypeOf<MmdInertializationGroup>());
				Assert.That(host.GetExistingSystemManaged<SimulationSystemGroup>(), Is.Null);
				Assert.That(host.GetExistingSystemManaged<PresentationSystemGroup>(), Is.Null);
				using (var stateQuery = host.EntityManager.CreateEntityQuery(typeof(MmdWorldState))) {
					Assert.That(stateQuery.CalculateEntityCount(), Is.Zero);
				}
			}
		}

		[Test]
		public void RejectsParentFromAnotherWorldBeforeCreatingSystems() {
			using (var host = new World("MMD Host Fixture")) {
				using (var other = new World("MMD Other Fixture")) {
					var parent = other.GetOrCreateSystemManaged<FixedStepSimulationSystemGroup>();
					Assert.Throws<ArgumentException>(() => MmdSystemInstaller.Install(host, parent));
					Assert.That(host.GetExistingSystemManaged<MmdSimulationGroup>(), Is.Null);
					Assert.That(parent.ManagedSystems.Count, Is.Zero);
				}
			}
		}
	}
}
