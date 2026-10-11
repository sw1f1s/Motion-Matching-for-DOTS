using ColInt.MMD.Actions.Components;
using ColInt.MMD.Lifecycle.Components;
using ColInt.MMD.Physics.Components;
using ColInt.MMD.Playback.Helpers;
using ColInt.MMD.RootMotion.Components;
using ColInt.MMD.Samples.Character.Components;
using ColInt.MMD.Samples.Character.Input.Components;
using ColInt.MMD.Samples.Character.Movement.Facing.Helpers;
using ColInt.MMD.Samples.Character.Movement.Trajectory.Components;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace ColInt.MMD.Samples.Character.Movement.Facing.Systems {
	[BurstCompile]
	[DisableAutoCreation]
	[UpdateInGroup(typeof(MmdSampleFacingGroup))]
	public partial struct MmdSampleFacingSystem : ISystem {
		public void OnCreate(ref SystemState state) {
			state.RequireForUpdate<MmdWorldState>();
			state.RequireForUpdate<MmdSampleInput>();
		}

		[BurstCompile]
		public void OnUpdate(ref SystemState state) {
			if (SystemAPI.GetSingleton<MmdWorldState>().IsPaused) {
				return;
			}
			state.Dependency = new FacingJob {
				Input = SystemAPI.GetSingleton<MmdSampleInput>(), PhysicsRoots = SystemAPI.GetComponentLookup<MmdPhysicsRoot>(true)
			}.ScheduleParallel(state.Dependency);
		}

		[BurstCompile]
		private partial struct FacingJob : IJobEntity {
			public MmdSampleInput Input;
			[ReadOnly] public ComponentLookup<MmdPhysicsRoot> PhysicsRoots;

			private void Execute(Entity entity, in MmdSampleCharacterControl control, in LocalTransform transform, in MmdActionState action,
				ref MmdSampleTrajectoryState trajectory, ref MmdRootMotion motion) {
				if (!motion.Valid || motion.DeltaTime <= 0f) {
					return;
				}
				if (!PhysicsRoots.HasComponent(entity)) {
					trajectory.Velocity = math.rotate(transform.Rotation, motion.Translation) / motion.DeltaTime;
				}
				float2 facing = MmdSampleFacing.Resolve(Input.Movement);
				if (action.ActionId != 0) {
					return;
				}
				if (math.lengthsq(facing) < 0.000001f) {
					// Stop steering immediately, including residual angular motion from the outgoing clip.
					motion.Rotation = quaternion.identity;
					return;
				}
				float error = trajectory.TurnError;
				float limit = math.max(0f, control.TurnSpeed) * motion.DeltaTime;
				float authoredYaw = MmdAnimationMath.RotationVector(motion.Rotation).y;
				float correction = math.clamp(error - authoredYaw, -limit, limit);
				float yaw = math.clamp(authoredYaw + correction, -limit, limit);
				yaw = error >= 0f ? math.clamp(yaw, 0f, error) : math.clamp(yaw, error, 0f);
				motion.Rotation = quaternion.RotateY(yaw);
			}
		}
	}
}
