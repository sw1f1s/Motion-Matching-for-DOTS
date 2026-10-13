using ColInt.MMD.Database.Components;
using ColInt.MMD.Lifecycle.Components;
using ColInt.MMD.Playback.Components;
using ColInt.MMD.Samples.Character.Input.Components;
using ColInt.MMD.Samples.Character.Movement.CrouchState.Components;
using ColInt.MMD.Samples.Character.Movement.CrouchState.Helpers;
using ColInt.MMD.Samples.Character.Movement.Shared.Components;
using ColInt.MMD.Samples.Character.Movement.Shared.Helpers;
using ColInt.MMD.Samples.Character.Movement.Shared.Systems;
using ColInt.MMD.Samples.Character.Movement.SlideState.Components;
using ColInt.MMD.Samples.Physics.Components;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;

namespace ColInt.MMD.Samples.Character.Movement.SlideState.Systems {
	[BurstCompile]
	[DisableAutoCreation]
	[UpdateInGroup(typeof(MmdSampleStateUpdateGroup))]
	public partial struct MmdSampleSlideExitUpdateSystem : ISystem {
		public void OnCreate(ref SystemState state) {
			state.RequireForUpdate<MmdWorldState>();
			state.RequireForUpdate<MmdSampleInput>();
			state.RequireForUpdate<PhysicsWorldSingleton>();
		}

		[BurstCompile]
		public void OnUpdate(ref SystemState state) {
			if (SystemAPI.GetSingleton<MmdWorldState>().IsPaused) {
				return;
			}
			state.Dependency = new UpdateJob {
				DeltaTime = SystemAPI.Time.DeltaTime,
				World = SystemAPI.GetSingleton<PhysicsWorldSingleton>().CollisionWorld
			}.ScheduleParallel(state.Dependency);
		}

		[BurstCompile]
		[WithAll(typeof(MmdSampleState<MmdSampleSlideExit>))]
		private partial struct UpdateJob : IJobEntity {
			public float DeltaTime;
			[Unity.Collections.ReadOnly] public CollisionWorld World;
			private void Execute(in MmdSampleInput input, Entity entity, in LocalTransform transform, in MmdSamplePhysicsColliderOwner owner, in MmdPlayback playback, in MmdDatabase data,
				ref MmdSampleState<MmdSampleSlideExit> current, ref MmdSampleStateTransition transition) {
				current.Elapsed += DeltaTime;
				if ((!input.Crouch || input.Sprint) && MmdSampleCapsuleQuery.CanStand(World, entity, owner.Value, transform)) {
					transition.Request<MmdSampleCrouchExit>(MmdSampleStatePriority.Completion);
				} else if (math.lengthsq(input.Movement) > 0.001f || current.Elapsed >= data.Value.Value.Clips[playback.ClipIndex].Duration) {
					transition.Request<MmdSampleCrouch>(MmdSampleStatePriority.Completion);
				}
			}
		}
	}
}
