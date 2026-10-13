using ColInt.MMD.Database.Components;
using ColInt.MMD.Lifecycle.Components;
using ColInt.MMD.Playback.Components;
using ColInt.MMD.Playback.Helpers;
using ColInt.MMD.RootMotion.Components;
using ColInt.MMD.Samples.Character.Input.Components;
using ColInt.MMD.Samples.Character.Movement.TraversalState.Components;
using ColInt.MMD.Samples.Character.Movement.TraversalState.Helpers;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;

namespace ColInt.MMD.Samples.Character.Movement.TraversalState.Systems {
	[DisableAutoCreation]
	[UpdateInGroup(typeof(MmdSampleTraversalMotionGroup))]
	public partial struct MmdSampleTraversalMotionSystem : ISystem {
		public void OnCreate(ref SystemState state) {
			state.RequireForUpdate<MmdWorldState>();
			state.RequireForUpdate<MmdSampleTraversalActive>();
			state.RequireForUpdate<PhysicsWorldSingleton>();
			state.RequireForUpdate<MmdSampleInput>();
		}

		public void OnUpdate(ref SystemState state) {
			if (SystemAPI.GetSingleton<MmdWorldState>().IsPaused) {
				return;
			}
			// The immediate traversal job must consume the velocity produced by earlier scheduled jobs.
			state.CompleteDependency();
			var world = SystemAPI.GetSingleton<PhysicsWorldSingleton>().CollisionWorld;
			using var commands = new EntityCommandBuffer(Allocator.TempJob);
			new MotionJob { World = world, Input = SystemAPI.GetSingleton<MmdSampleInput>(), DeltaTime = SystemAPI.Time.DeltaTime, Commands = commands }.Run();
			commands.Playback(state.EntityManager);
		}

		[BurstCompile]
		[WithAll(typeof(MmdSampleTraversalActive))]
		[WithNone(typeof(MmdSampleTraversalBlocked))]
		private partial struct MotionJob : IJobEntity {
			[ReadOnly] public CollisionWorld World;
			public MmdSampleInput Input;
			public float DeltaTime;
			public EntityCommandBuffer Commands;

			private void Execute(Entity entity, in MmdSampleTraversalTarget target, in MmdSampleTraversalProgress progress,
				in MmdSampleTraversalSettings settings, in MmdPlayback playback, in LocalTransform transform,
				in PhysicsCollider collider, in MmdDatabase data, in PhysicsMass mass,
				ref PhysicsVelocity velocity, ref MmdRootMotion motion, ref MmdInertializationState smoothing) {
				bool continueMoving = target.Supported && math.dot(Input.Movement, math.forward(target.Rotation).xz) > 0.5f;
				float3 position = MmdSampleTraversalPath.Position(ref data.Value.Value, target, progress.Clip, playback.Time, continueMoving);
				quaternion rotation = MmdSampleTraversalPath.Rotation(target, progress.Clip, playback.Time, settings.AlignmentDuration);
				if (!MmdSampleTraversalQuery.TargetExists(World, target) ||
					!MmdSampleTraversalQuery.ClearSegment(World, entity, collider,
						transform.Position, position, rotation, transform.Scale)) {
					velocity = default;
					motion.Translation = float3.zero;
					motion.Rotation = quaternion.identity;
					motion.Valid = false;
					smoothing.RootVelocity = float3.zero;
					smoothing.RootAngularVelocity = float3.zero;
					Commands.AddComponent<MmdSampleTraversalBlocked>(entity);
					return;
				}
				float deltaTime = DeltaTime;
				velocity.Linear = (position - transform.Position) / deltaTime;
				quaternion delta = math.mul(math.inverse(transform.Rotation), rotation);
				float3 worldAngular = math.rotate(transform.Rotation, MmdAnimationMath.RotationVector(delta)) / deltaTime;
				quaternion inertiaRotation = math.mul(transform.Rotation, mass.Transform.rot);
				velocity.Angular = math.rotate(math.inverse(inertiaRotation), worldAngular);
				motion.Translation = math.rotate(math.inverse(transform.Rotation), position - transform.Position) / transform.Scale;
				motion.Rotation = delta;
				motion.DeltaTime = deltaTime;
				motion.Valid = false;
				// Inertialization follows the applied path, rather than the unwarped FBX root.
				smoothing.RootVelocity = velocity.Linear;
				smoothing.RootAngularVelocity = worldAngular;
			}
		}
	}
}
