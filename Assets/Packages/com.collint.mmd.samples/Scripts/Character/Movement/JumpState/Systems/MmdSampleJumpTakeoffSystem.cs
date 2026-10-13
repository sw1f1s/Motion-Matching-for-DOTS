using ColInt.MMD.Lifecycle.Components;
using ColInt.MMD.Playback.Components;
using ColInt.MMD.Samples.Character.Movement.JumpState.Components;
using ColInt.MMD.Samples.Character.Movement.Shared.Components;
using ColInt.MMD.Samples.Character.Movement.Shared.Systems;
using Unity.Entities;

namespace ColInt.MMD.Samples.Character.Movement.JumpState.Systems {
	[DisableAutoCreation]
	[UpdateInGroup(typeof(MmdSampleActionEntryGroup))]
	public partial struct MmdSampleJumpTakeoffSystem : ISystem {
		public void OnCreate(ref SystemState state) {
			state.RequireForUpdate<MmdWorldState>();
			state.RequireForUpdate<MmdSampleJumpImpulse>();
		}

		public void OnUpdate(ref SystemState state) {
			if (SystemAPI.GetSingleton<MmdWorldState>().IsPaused) {
				return;
			}
			foreach (var (progress, playback) in SystemAPI.Query<RefRO<MmdSampleJumpProgress>, RefRW<MmdPlayback>>()
				.WithAll<MmdSampleState<MmdSampleJump>, MmdSampleJumpImpulse>()) {
				playback.ValueRW.Time = progress.ValueRO.TakeoffTime;
			}
		}
	}
}
