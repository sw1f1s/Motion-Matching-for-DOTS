using ColInt.MMD.Lifecycle.Components;
using ColInt.MMD.Samples.Character.Input.Components;
using ColInt.MMD.MotionMatching.Components;
using ColInt.MMD.MotionMatching.Helpers;
using ColInt.MMD.Physics.Components;
using ColInt.MMD.Samples.Character.Movement.Shared.Components;
using ColInt.MMD.Samples.Character.Movement.Shared.Systems;
using ColInt.MMD.Samples.Character.Movement.SlideState.Components;
using ColInt.MMD.Samples.Character.Movement.SlideState.Helpers;
using ColInt.MMD.Samples.Character.Movement.Trajectory.Components;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;

namespace ColInt.MMD.Samples.Character.Movement.SlideState.Systems {
	[BurstCompile]
	[DisableAutoCreation]
	[UpdateInGroup(typeof(MmdSampleSearchQueryGroup))]
	public partial struct MmdSampleSlideTrajectorySystem : ISystem {
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
			state.Dependency = new PredictionJob {
				Input = SystemAPI.GetSingleton<MmdSampleInput>(), CharacterInputs = SystemAPI.GetComponentLookup<MmdSampleCharacterInputOverride>(true),
				World = SystemAPI.GetSingleton<PhysicsWorldSingleton>().CollisionWorld, Gravity = math.abs(SystemAPI.GetSingleton<PhysicsStep>().Gravity.y),
				Exits = SystemAPI.GetComponentLookup<MmdSampleState<MmdSampleSlideExit>>(true)
			}.ScheduleParallel(state.Dependency);
		}

		[BurstCompile]
		[WithAny(typeof(MmdSampleState<MmdSampleSlide>), typeof(MmdSampleState<MmdSampleSlideExit>))]
		private partial struct PredictionJob : IJobEntity {
			public MmdSampleInput Input;
			[ReadOnly] public ComponentLookup<MmdSampleCharacterInputOverride> CharacterInputs;
			[ReadOnly] public CollisionWorld World;
			[ReadOnly] public ComponentLookup<MmdSampleState<MmdSampleSlideExit>> Exits;
			public float Gravity;

			private void Execute(Entity entity, in MmdSampleSlide slide, in MmdSampleSlideProgress progress, in MmdSampleGroundState ground,
				in MmdSampleMovementSettings settings, in PhysicsCollider collider, in LocalTransform transform, ref MmdSampleTrajectoryState trajectory,
				ref MmdSearchQuerySpace space, DynamicBuffer<MmdSearchQueryElement> query, DynamicBuffer<MmdTrajectoryPoint> points) {
				space = new MmdSearchQuerySpace { Position = transform.Position, Rotation = transform.Rotation };
				bool exiting = Exits.HasComponent(entity);
				var input = CharacterInputs.HasComponent(entity) ? CharacterInputs[entity].Value : Input;
				float3 velocity = exiting ? float3.zero : progress.Velocity;
				float3 normal = MmdSampleSlideMath.GroundNormal(World, entity, collider, transform, settings);
				float3 localVelocity = math.rotate(math.inverse(transform.Rotation), velocity);
				query[12] = new MmdSearchQueryElement { Value = localVelocity.x };
				query[13] = new MmdSearchQueryElement { Value = localVelocity.y };
				query[14] = new MmdSearchQueryElement { Value = localVelocity.z };
				trajectory.Velocity = velocity;
				trajectory.DesiredMovement = math.normalizesafe(velocity.xz);
				trajectory.TurnError = 0f;
				points.ResizeUninitialized(3);
				float3 position = transform.Position;
				float previousTime = 0f;
				for (int point = 0; point < 3; point++) {
					float time = LocomotionSchema.PredictionTimes[point];
					float step = (time - previousTime) / 8f;
					for (int sample = 0; sample < 8; sample++) {
						velocity = MmdSampleSlideMath.Steer(velocity, normal, exiting ? float2.zero : input.Movement, slide.SteeringSpeed, step);
						velocity = MmdSampleSlideMath.Advance(velocity, normal, slide, ground.IsGrounded && !exiting ? Gravity : 0f, step);
						position += velocity * step;
					}
					float3 local = math.rotate(math.inverse(transform.Rotation), position - transform.Position);
					float3 forward = math.normalizesafe(new float3(velocity.x, 0f, velocity.z), math.rotate(transform.Rotation, math.forward()));
					float3 localForward = math.rotate(math.inverse(transform.Rotation), forward);
					query[point * 4] = new MmdSearchQueryElement { Value = local.x };
					query[point * 4 + 1] = new MmdSearchQueryElement { Value = local.z };
					query[point * 4 + 2] = new MmdSearchQueryElement { Value = localForward.x };
					query[point * 4 + 3] = new MmdSearchQueryElement { Value = localForward.z };
					points[point] = new MmdTrajectoryPoint { Time = time, Position = position, Forward = forward };
					previousTime = time;
				}
			}
		}
	}
}
