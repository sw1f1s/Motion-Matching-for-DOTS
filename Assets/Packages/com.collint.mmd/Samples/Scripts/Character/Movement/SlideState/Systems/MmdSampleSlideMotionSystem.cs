using ColInt.MMD.Lifecycle.Components;
using ColInt.MMD.Samples.Character.Input.Components;
using ColInt.MMD.Samples.Character.Components;
using ColInt.MMD.Samples.Character.Movement.Shared.Components;
using ColInt.MMD.Samples.Character.Movement.Shared.Systems;
using ColInt.MMD.Samples.Character.Movement.SlideState.Components;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;
using ColInt.MMD.RootMotion.Components;
using ColInt.MMD.Samples.Character.Movement.Facing.Helpers;
using ColInt.MMD.Samples.Character.Movement.SlideState.Helpers;

namespace ColInt.MMD.Samples.Character.Movement.SlideState.Systems {
	[BurstCompile]
	[DisableAutoCreation]
	[UpdateInGroup(typeof(MmdSampleSlideMotionGroup))]
	public partial struct MmdSampleSlideMotionSystem : ISystem {
		public void OnCreate(ref SystemState state) {
			state.RequireForUpdate<MmdWorldState>();
			state.RequireForUpdate<MmdSampleInput>();
			state.RequireForUpdate<PhysicsWorldSingleton>();
			state.RequireForUpdate<PhysicsStep>();
		}

		[BurstCompile]
		public void OnUpdate(ref SystemState state) {
			if (SystemAPI.GetSingleton<MmdWorldState>().IsPaused) {
				return;
			}
			state.Dependency = new UpdateJob {
				Input = SystemAPI.GetSingleton<MmdSampleInput>(), CharacterInputs = SystemAPI.GetComponentLookup<MmdSampleCharacterInputOverride>(true),
				World = SystemAPI.GetSingleton<PhysicsWorldSingleton>().CollisionWorld,
				Exits = SystemAPI.GetComponentLookup<MmdSampleState<MmdSampleSlideExit>>(true),
				Gravity = math.abs(SystemAPI.GetSingleton<PhysicsStep>().Gravity.y),
				DeltaTime = SystemAPI.Time.DeltaTime
			}.ScheduleParallel(state.Dependency);
		}

		[BurstCompile]
		[WithAny(typeof(MmdSampleState<MmdSampleSlide>), typeof(MmdSampleState<MmdSampleSlideExit>))]
		private partial struct UpdateJob : IJobEntity {
			public MmdSampleInput Input;
			[ReadOnly] public ComponentLookup<MmdSampleCharacterInputOverride> CharacterInputs;
			[ReadOnly] public CollisionWorld World;
			[ReadOnly] public ComponentLookup<MmdSampleState<MmdSampleSlideExit>> Exits;
			public float Gravity;
			public float DeltaTime;
			private void Execute(
				Entity entity,
				in MmdSampleSlide slide,
				in MmdSampleGroundState ground,
				in PhysicsCollider collider,
				in LocalTransform transform,
				in PhysicsVelocity velocity,
				in MmdSampleMovementSettings settings,
				in MmdSampleCharacterControl control,
				ref MmdSampleSlideProgress progress,
				ref MmdRootMotion motion) {
				float3 next = float3.zero;
				if (!Exits.HasComponent(entity)) {
					float3 normal = MmdSampleSlideMath.GroundNormal(World, entity, collider, transform, settings);
					float3 feedback = MmdSampleSlideMath.FromPlanarVelocity(velocity.Linear.xz, normal);
					var input = CharacterInputs.HasComponent(entity) ? CharacterInputs[entity].Value : Input;
					feedback = MmdSampleSlideMath.Steer(feedback, normal, input.Movement, slide.SteeringSpeed, DeltaTime);
					next = MmdSampleSlideMath.Advance(feedback, normal, slide, ground.IsGrounded ? Gravity : 0f, DeltaTime);
				}
				progress.Velocity = next;
				motion.Translation = math.rotate(math.inverse(transform.Rotation), next * DeltaTime) / transform.Scale;
				float error = MmdSampleFacing.YawError(transform.Rotation, next.xz);
				motion.Rotation = quaternion.RotateY(math.clamp(error, -control.TurnSpeed * DeltaTime, control.TurnSpeed * DeltaTime));
				motion.DeltaTime = DeltaTime;
				motion.Valid = true;
			}
		}
	}
}
