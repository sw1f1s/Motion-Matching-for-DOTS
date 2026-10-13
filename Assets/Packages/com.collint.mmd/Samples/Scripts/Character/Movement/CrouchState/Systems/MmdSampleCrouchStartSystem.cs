using Unity.Burst;
using ColInt.MMD.MotionMatching.Helpers;
using ColInt.MMD.MotionMatching.Data;
using ColInt.MMD.Actions.Components;
using ColInt.MMD.Lifecycle.Components;
using ColInt.MMD.MotionMatching.Components;
using ColInt.MMD.Playback.Components;
using ColInt.MMD.Samples.Character.Movement.CrouchState.Components;
using ColInt.MMD.Samples.Character.Movement.Shared.Components;
using ColInt.MMD.Samples.Character.Movement.Shared.Helpers;
using ColInt.MMD.Samples.Character.Movement.Shared.Systems;
using Unity.Entities;

namespace ColInt.MMD.Samples.Character.Movement.CrouchState.Systems {
	[DisableAutoCreation]
	[UpdateInGroup(typeof(MmdSampleStateStartGroup))]
	public partial struct MmdSampleCrouchStartSystem : ISystem {
		public void OnCreate(ref SystemState state) {
			state.RequireForUpdate<MmdWorldState>();
		}

		public void OnUpdate(ref SystemState state) {
			if (SystemAPI.GetSingleton<MmdWorldState>().IsPaused) {
				return;
			}
			state.Dependency = new StartJob().ScheduleParallel(state.Dependency);
		}

		[BurstCompile]
		[WithAll(typeof(MmdSampleStateStarted<MmdSampleCrouch>))]
		private partial struct StartJob : IJobEntity {
			private void Execute(in MmdSampleCrouch crouch, in MmdSampleCrouchProgress progress, ref MmdInertialization smoothing, ref MmdSampleMovement movement, ref MmdActionRequest request, ref MmdSearchSettings settings, ref MmdPlayback playback, DynamicBuffer<MmdMotionGroup> groups) {
				smoothing = progress.StandingInertialization;
				movement = new MmdSampleMovement { CrouchCollider = true };
				MmdSampleAnimationRequest.Cancel(ref request);
				MmdMotionGroups.Set(groups, MmdMotionGroupUsage.Required, crouch.IdleGroup);
				playback.SearchCountdown = 0f;
				playback.Speed = 1f;
			}
		}
	}
}
