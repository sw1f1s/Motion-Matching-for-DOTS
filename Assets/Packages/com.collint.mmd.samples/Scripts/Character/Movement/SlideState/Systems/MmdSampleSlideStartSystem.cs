using ColInt.MMD.Actions.Components;
using ColInt.MMD.Database.Components;
using ColInt.MMD.Lifecycle.Components;
using ColInt.MMD.MotionMatching.Components;
using ColInt.MMD.Playback.Components;
using ColInt.MMD.Samples.Character.Components;
using ColInt.MMD.Samples.Character.Movement.CrouchState.Components;
using ColInt.MMD.Samples.Character.Movement.Shared.Components;
using ColInt.MMD.Samples.Character.Movement.Shared.Helpers;
using ColInt.MMD.Samples.Character.Movement.Shared.Systems;
using ColInt.MMD.Samples.Character.Movement.SlideState.Components;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;

namespace ColInt.MMD.Samples.Character.Movement.SlideState.Systems {
	[BurstCompile]
	[DisableAutoCreation]
	[UpdateInGroup(typeof(MmdSampleStateStartGroup))]
	public partial struct MmdSampleSlideStartSystem : ISystem {
		public void OnCreate(ref SystemState state) {
			state.RequireForUpdate<MmdWorldState>();
		}

		[BurstCompile]
		public void OnUpdate(ref SystemState state) {
			if (SystemAPI.GetSingleton<MmdWorldState>().IsPaused) {
				return;
			}
			state.Dependency = new UpdateJob().ScheduleParallel(state.Dependency);
		}

		[BurstCompile]
		[WithAll(typeof(MmdSampleStateStarted<MmdSampleSlide>))]
		private partial struct UpdateJob : IJobEntity {

			private void Execute(
				in MmdSampleSlide slide,
				in MmdDatabase data,
				in MmdSampleCharacterControl control,
				in PhysicsVelocity velocity,
				ref MmdInertialization smoothing,
				ref MmdSampleCrouchProgress crouchProgress,
				ref MmdSampleSlideProgress progress,
				ref MmdSampleMovement movement,
				in MmdSearchSettings settings,
				ref MmdPlayback playback,
				ref MmdActionRequest request,
				DynamicBuffer<MmdSearchQueryElement> query) {
				crouchProgress.StandingControl = control;
				crouchProgress.StandingInertialization = smoothing;
				smoothing.PoseDuration = math.min(smoothing.PoseDuration, 0.1f);
				crouchProgress.StandingDirectionWeight = settings.TrajectoryDirectionWeight;
				progress.Velocity = new float3(velocity.Linear.x, 0f, velocity.Linear.z);
				movement = new MmdSampleMovement { CrouchCollider = true };
				playback.Speed = 1f;
				MmdSampleAnimationRequest.Play(ref data.Value.Value, query, slide.EnterAction, ref request);
			}
		}
	}
}
