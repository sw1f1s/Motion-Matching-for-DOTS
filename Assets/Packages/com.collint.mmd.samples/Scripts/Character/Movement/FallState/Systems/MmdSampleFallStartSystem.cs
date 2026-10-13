using Unity.Burst;
using ColInt.MMD.MotionMatching.Helpers;
using ColInt.MMD.MotionMatching.Data;
using ColInt.MMD.Actions.Components;
using ColInt.MMD.Lifecycle.Components;
using ColInt.MMD.MotionMatching.Components;
using ColInt.MMD.Playback.Components;
using ColInt.MMD.Samples.Character.Movement.FallState.Components;
using ColInt.MMD.Samples.Character.Movement.Shared.Components;
using ColInt.MMD.Samples.Character.Movement.Shared.Helpers;
using ColInt.MMD.Samples.Character.Movement.Shared.Systems;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;

namespace ColInt.MMD.Samples.Character.Movement.FallState.Systems {
	[DisableAutoCreation]
	[UpdateInGroup(typeof(MmdSampleStateStartGroup))]
	public partial struct MmdSampleFallStartSystem : ISystem {
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
		[WithAll(typeof(MmdSampleStateStarted<MmdSampleFall>))]
		private partial struct StartJob : IJobEntity {
			private void Execute(in MmdSampleFall fall, in PhysicsVelocity velocity, ref MmdSampleMovement movement, ref MmdActionRequest request, ref MmdSearchSettings settings, ref MmdPlayback playback, ref MmdSampleGroundState ground, DynamicBuffer<MmdMotionGroup> groups) {
				movement = new MmdSampleMovement { IgnoreGround = true, AirControl = true, AirVelocity = velocity.Linear.xz };
				ground.MaximumDownwardSpeed = math.max(0f, -velocity.Linear.y);
				MmdSampleAnimationRequest.Cancel(ref request);
				MmdMotionGroups.Set(groups, MmdMotionGroupUsage.Required, fall.Group);
				MmdMotionGroups.Set(groups, MmdMotionGroupUsage.Excluded, 0);
				playback.Speed = 1f;
				playback.SearchCountdown = 0f;
				playback.TransitionElapsed = settings.MinimumTransitionTime;
			}
		}
	}
}
