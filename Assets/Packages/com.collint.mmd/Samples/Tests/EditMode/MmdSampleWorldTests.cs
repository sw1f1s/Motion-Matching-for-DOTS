using System;
using ColInt.MMD.Actions.Components;
using ColInt.MMD.Database.Helpers;
using ColInt.MMD.MotionMatching.Components;
using ColInt.MMD.MotionMatching.Helpers;
using ColInt.MMD.Playback.Components;
using ColInt.MMD.RootMotion.Components;
using ColInt.MMD.Samples.Lifecycle;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace ColInt.MMD.Tests {
	public class MmdSampleWorldTests {
		[Test]
		public void SearchSelectsWalkingAndRootMotionWorksWithoutView() {
			using (var world = CreateWorld()) {
				Entity entity = world.Characters.Create(MotionDatabaseBuilder.Build(MotionDatabaseFixture.Create()), true, LocalTransform.Identity);
				LocomotionSchema.WriteTrajectory(world.World.EntityManager.GetBuffer<MmdSearchQueryElement>(entity), new float3(0f, 0f, 1f), 0f);
				world.Tick();
				var manager = world.World.EntityManager;
				Assert.That(manager.GetComponentData<MmdPlayback>(entity).ClipIndex, Is.EqualTo(1));
				Assert.That(manager.GetComponentData<LocalTransform>(entity).Position.z, Is.EqualTo(0.1f).Within(0.00001f));
				Assert.That(manager.GetBuffer<MmdLocalPose>(entity).Length, Is.EqualTo(2));
				Assert.That(manager.GetComponentData<MmdRootMotion>(entity).Valid, Is.False);
			}
		}

		[Test]
		public void PhysicsOwnerReceivesDeltaWithoutPluginMovingTransform() {
			using (var world = CreateWorld()) {
				Entity entity = world.Characters.Create(MotionDatabaseBuilder.Build(MotionDatabaseFixture.Create()), false, LocalTransform.Identity);
				LocomotionSchema.WriteTrajectory(world.World.EntityManager.GetBuffer<MmdSearchQueryElement>(entity), new float3(0f, 0f, 1f), 0f);
				world.Tick();
				var manager = world.World.EntityManager;
				Assert.That(manager.GetComponentData<LocalTransform>(entity).Position.z, Is.Zero);
				Assert.That(manager.GetComponentData<MmdRootMotion>(entity).Translation.z, Is.EqualTo(0.1f).Within(0.00001f));
				Assert.That(manager.GetComponentData<MmdRootMotion>(entity).Valid, Is.True);
			}
		}

		[Test]
		public void WorldsHaveIndependentPausePlaybackAndLifetime() {
			using (var first = CreateWorld()) {
				using (var second = CreateWorld()) {
					Entity a = first.Characters.Create(MotionDatabaseBuilder.Build(MotionDatabaseFixture.Create()), true, LocalTransform.Identity);
					Entity b = second.Characters.Create(MotionDatabaseBuilder.Build(MotionDatabaseFixture.Create()), true, LocalTransform.Identity);
					first.SetPaused(true);
					first.Tick();
					second.Tick();
					Assert.That(first.World.EntityManager.GetComponentData<MmdPlayback>(a).Time, Is.Zero);
					Assert.That(second.World.EntityManager.GetComponentData<MmdPlayback>(b).Time, Is.EqualTo(0.1f).Within(0.00001f));
					first.Dispose();
					second.Tick();
					Assert.That(second.World.EntityManager.GetComponentData<MmdPlayback>(b).Time, Is.EqualTo(0.2f).Within(0.00001f));
				}
			}
		}

		[Test]
		public void ActionRevisionDoesNotRestartAndCompletionIsSimulationState() {
			using (var world = CreateWorld()) {
				Entity entity = world.Characters.Create(MotionDatabaseBuilder.Build(MotionDatabaseFixture.Create()), true, LocalTransform.Identity);
				var manager = world.World.EntityManager;
				manager.SetComponentData(entity, new MmdActionRequest { ActionId = 7, Revision = 1, PreferredClip = -1 });
				for (int i = 0; i < 15; i++) {
					world.Tick();
				}
				Assert.That(manager.GetComponentData<MmdActionState>(entity).Completed, Is.True);
				Assert.That(manager.GetComponentData<MmdPlayback>(entity).Time, Is.EqualTo(1f));
				Assert.That(manager.GetComponentData<LocalTransform>(entity).Position.z, Is.EqualTo(2f).Within(0.0001f));
				manager.SetComponentData(entity, new MmdActionRequest { ActionId = 0, Revision = 2, PreferredClip = -1 });
				world.Tick();
				Assert.That(manager.GetComponentData<MmdActionState>(entity).ActionId, Is.Zero);
				Assert.That(manager.GetComponentData<MmdPlayback>(entity).ClipIndex, Is.Zero);
			}
		}

		[Test]
		public void DisabledCharactersDoNotAdvanceAndDisposeIsIdempotent() {
			var world = CreateWorld();
			Entity entity = world.Characters.Create(MotionDatabaseBuilder.Build(MotionDatabaseFixture.Create()), true, LocalTransform.Identity);
			world.World.EntityManager.AddComponent<Disabled>(entity);
			world.Tick();
			Assert.That(world.World.EntityManager.GetComponentData<MmdPlayback>(entity).Time, Is.Zero);
			world.Dispose();
			world.Dispose();
			Assert.Throws<ObjectDisposedException>(() => world.Tick());
		}

		private static MmdSampleWorld CreateWorld() {
			return new MmdSampleWorld(0.1f);
		}
	}

}
