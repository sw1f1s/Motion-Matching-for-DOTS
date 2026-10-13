using ColInt.MMD.Lifecycle.Components;
using ColInt.MMD.Samples.Benchmark.Components;
using ColInt.MMD.Samples.Benchmark.Helpers;
using ColInt.MMD.Samples.Character.Input.Components;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace ColInt.MMD.Samples.Benchmark.Systems {
	[BurstCompile]
	[DisableAutoCreation]
	[UpdateInGroup(typeof(MmdSampleBenchmarkIntentGroup))]
	public partial struct MmdSampleBenchmarkTargetSystem : ISystem {
		public void OnCreate(ref SystemState state) {
			state.RequireForUpdate<MmdWorldState>();
			state.RequireForUpdate<MmdSampleBenchmarkArea>();
		}

		[BurstCompile]
		public void OnUpdate(ref SystemState state) {
			if (SystemAPI.GetSingleton<MmdWorldState>().IsPaused) {
				return;
			}
			state.Dependency = new TargetJob {
				Area = SystemAPI.GetSingleton<MmdSampleBenchmarkArea>()
			}.ScheduleParallel(state.Dependency);
		}

		[BurstCompile]
		private partial struct TargetJob : IJobEntity {
			public MmdSampleBenchmarkArea Area;

			private void Execute(in LocalTransform transform, ref MmdSampleBenchmarkAgent agent, ref MmdSampleInput input) {
				if (math.distancesq(transform.Position.xz, agent.Target.xz) <= Area.ArrivalRadius * Area.ArrivalRadius) {
					// Sample from the previous goal so contact timing cannot change the random sequence.
					agent.Target = MmdSampleBenchmarkRandom.Target(ref agent.Random, Area, agent.Target);
					agent.CompletedTargets++;
				}
				float2 direction = math.normalizesafe(agent.Target.xz - transform.Position.xz);
				input = new MmdSampleInput { Movement = direction, Facing = direction, Run = true };
			}
		}
	}
}
