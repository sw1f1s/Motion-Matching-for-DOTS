using ColInt.MMD.MotionMatching.Helpers;
using ColInt.MMD.MotionMatching.Data;
using ColInt.MMD.Database.Helpers;
using ColInt.MMD.Actions.Components;
using ColInt.MMD.Lifecycle.Components;
using ColInt.MMD.MotionMatching.Components;
using ColInt.MMD.Playback.Components;
using ColInt.MMD.Samples.Benchmark.Components;
using ColInt.MMD.Samples.Benchmark.Helpers;
using ColInt.MMD.Samples.Benchmark.Systems;
using ColInt.MMD.Samples.Character.Components;
using ColInt.MMD.Samples.Character.Input.Components;
using ColInt.MMD.Samples.Character.Movement.Shared.Components;
using ColInt.MMD.Samples.Lifecycle;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace ColInt.MMD.Tests {
	public class MmdSampleBenchmarkTests {
		[TestCase(0)]
		[TestCase(12345)]
		[TestCase(-17)]
		public void SeedReproducesIndependentStreamsAndTargetsStayInsideArea(int seed) {
			var area = new MmdSampleBenchmarkArea { Center = new float3(10f, 2f, -8f), HalfSize = new float2(45f, 30f), ArrivalRadius = 2f };
			for (int character = 0; character < 1000; character++) {
				var first = MmdSampleBenchmarkRandom.CreateStream(seed, character);
				var replay = MmdSampleBenchmarkRandom.CreateStream(seed, character);
				var other = MmdSampleBenchmarkRandom.CreateStream(seed, character + 1);
				Assert.That(first.state, Is.Not.EqualTo(other.state));
				float3 position = MmdSampleBenchmarkRandom.Position(ref first, area);
				Assert.That(position, Is.EqualTo(MmdSampleBenchmarkRandom.Position(ref replay, area)));
				for (int destination = 0; destination < 5; destination++) {
					float3 target = MmdSampleBenchmarkRandom.Target(ref first, area, position);
					Assert.That(target, Is.EqualTo(MmdSampleBenchmarkRandom.Target(ref replay, area, position)));
					Assert.That(math.all(math.abs(target.xz - area.Center.xz) <= area.HalfSize), Is.True);
					Assert.That(target.y, Is.EqualTo(area.Center.y));
					Assert.That(math.distance(target.xz, position.xz), Is.GreaterThan(area.ArrivalRadius));
					position = target;
				}
			}
		}

		[Test]
		public void ArrivalSelectsNextTargetWithoutRestartingDistantAgentsAndPauseFreezesIt() {
			using (var world = new World("Benchmark target test")) {
				var manager = world.EntityManager;
				Entity state = manager.CreateEntity(typeof(MmdWorldState), typeof(MmdSampleBenchmarkArea));
				manager.SetComponentData(state, new MmdSampleBenchmarkArea { HalfSize = new float2(45f), ArrivalRadius = 2f });
				Entity arrived = CreateAgent(manager, float3.zero, float3.zero, 0);
				Entity replay = CreateAgent(manager, new float3(1f, 0f, 0f), float3.zero, 0);
				Entity distant = CreateAgent(manager, float3.zero, new float3(12f, 0f, -5f), 1);
				Entity disabled = CreateAgent(manager, float3.zero, float3.zero, 2);
				manager.AddComponent<Disabled>(disabled);
				var system = world.GetOrCreateSystem<MmdSampleBenchmarkTargetSystem>();
				system.Update(world.Unmanaged);
				manager.CompleteAllTrackedJobs();
				var agent = manager.GetComponentData<MmdSampleBenchmarkAgent>(arrived);
				Assert.That(agent.CompletedTargets, Is.EqualTo(1u));
				Assert.That(manager.GetComponentData<MmdSampleBenchmarkAgent>(replay).Target, Is.EqualTo(agent.Target), "An arrival offset must not change the seeded goal sequence.");
				Assert.That(math.distance(agent.Target.xz, float2.zero), Is.GreaterThan(2f));
				Assert.That(manager.GetComponentData<MmdSampleBenchmarkAgent>(distant).Target, Is.EqualTo(new float3(12f, 0f, -5f)));
				Assert.That(manager.GetComponentData<MmdSampleBenchmarkAgent>(distant).CompletedTargets, Is.Zero);
				Assert.That(manager.GetComponentData<MmdSampleBenchmarkAgent>(disabled).CompletedTargets, Is.Zero);
				var input = manager.GetComponentData<MmdSampleInput>(arrived);
				Assert.That(input.Run, Is.True);
				Assert.That(math.length(input.Movement), Is.EqualTo(1f).Within(0.00001f));
				Assert.That(input.Facing, Is.EqualTo(input.Movement));
				manager.SetComponentData(arrived, LocalTransform.FromPosition(agent.Target));
				manager.SetComponentData(state, new MmdWorldState { IsPaused = true });
				system.Update(world.Unmanaged);
				manager.CompleteAllTrackedJobs();
				Assert.That(manager.GetComponentData<MmdSampleBenchmarkAgent>(arrived).Random.state, Is.EqualTo(agent.Random.state));
				Assert.That(manager.GetComponentData<MmdSampleBenchmarkAgent>(arrived).CompletedTargets, Is.EqualTo(1u));
				manager.SetComponentData(state, default(MmdWorldState));
				system.Update(world.Unmanaged);
				manager.CompleteAllTrackedJobs();
				Assert.That(manager.GetComponentData<MmdSampleBenchmarkAgent>(arrived).CompletedTargets, Is.EqualTo(2u));
			}
		}

		[Test]
		public void IndividualInputsDriveSeparatePlayerAndBotTrajectories() {
			using (var sample = new MmdSampleWorld(1f / 60f)) {
				var manager = sample.World.EntityManager;
				Entity right = CreateControlledCharacter(sample);
				Entity left = CreateControlledCharacter(sample);
				Entity player = CreateControlledCharacter(sample);
				manager.SetComponentData(right, new MmdSampleInput { Movement = new float2(1f, 0f), Run = true });
				manager.SetComponentData(left, new MmdSampleInput { Movement = new float2(-1f, 0f), Run = true });
				manager.SetComponentData(player, new MmdSampleInput { Movement = new float2(0f, 1f) });
				sample.Tick();
				Assert.That(manager.GetBuffer<MmdSearchQueryElement>(right)[8].Value, Is.GreaterThan(0f));
				Assert.That(manager.GetBuffer<MmdSearchQueryElement>(left)[8].Value, Is.LessThan(0f));
				Assert.That(manager.GetBuffer<MmdSearchQueryElement>(player)[8].Value, Is.Zero.Within(0.00001f));
				Assert.That(manager.GetBuffer<MmdSearchQueryElement>(player)[9].Value, Is.GreaterThan(0f));
				Assert.That(MmdMotionGroups.First(manager.GetBuffer<MmdMotionGroup>(right), MmdMotionGroupUsage.Required), Is.EqualTo(4));
				Assert.That(MmdMotionGroups.First(manager.GetBuffer<MmdMotionGroup>(left), MmdMotionGroupUsage.Required), Is.EqualTo(4));
				Assert.That(MmdMotionGroups.First(manager.GetBuffer<MmdMotionGroup>(player), MmdMotionGroupUsage.Required), Is.EqualTo(2));
			}
		}

		private static Entity CreateAgent(EntityManager manager, float3 position, float3 target, int index) {
			Entity entity = manager.CreateEntity(typeof(LocalTransform), typeof(MmdSampleBenchmarkAgent), typeof(MmdSampleInput));
			manager.SetComponentData(entity, LocalTransform.FromPosition(position));
			manager.SetComponentData(entity, new MmdSampleBenchmarkAgent { Random = MmdSampleBenchmarkRandom.CreateStream(12345, index), Target = target });
			return entity;
		}

		private static Entity CreateControlledCharacter(MmdSampleWorld sample) {
			Entity entity = sample.Characters.Create(default, true, LocalTransform.Identity);
			var manager = sample.World.EntityManager;
			manager.AddComponent<MmdSampleMovement>(entity);
			manager.AddComponentData(entity, new MmdActionRequest { PreferredClip = -1 });
			manager.AddComponentData(entity, new MmdSampleCharacterControl { WalkSpeed = 2f, RunSpeed = 5f, TurnSpeed = 4.5f, IdleGroup = 1, WalkGroup = 2, RunGroup = 4 });
			manager.AddComponent<MmdSearchSettings>(entity);
			manager.AddComponentData(entity, new MmdPlayback { Speed = 1f });
			return entity;
		}
	}
}
