using ColInt.MMD.Lifecycle.Components;
using ColInt.MMD.Samples.Character.Input.Components;
using ColInt.MMD.Samples.Character.Movement.Shared.Systems;
using Unity.Burst;
using Unity.Entities;

namespace ColInt.MMD.Samples.Character.Input.Systems {
	[BurstCompile]
	[DisableAutoCreation]
	[UpdateInGroup(typeof(MmdSampleStateCleanupGroup))]
	public partial struct MmdSampleInputCleanupSystem : ISystem {
		public void OnCreate(ref SystemState state) {
			state.RequireForUpdate<MmdWorldState>();
			state.RequireForUpdate<MmdSampleInput>();
		}

		[BurstCompile]
		public void OnUpdate(ref SystemState state) {
			if (SystemAPI.GetSingleton<MmdWorldState>().IsPaused) {
				return;
			}
			state.Dependency = new ConsumeJob().ScheduleParallel(state.Dependency);
		}

		[BurstCompile]
		private partial struct ConsumeJob : IJobEntity {
			private void Execute(ref MmdSampleInput input) {
				input.JumpPressed = false;
				input.CrouchPressed = false;
			}
		}
	}
}
