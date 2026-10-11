using ColInt.MMD.Lifecycle.Components;
using ColInt.MMD.MotionMatching.Components;
using ColInt.MMD.MotionMatching.Helpers;
using ColInt.MMD.Physics.Components;
using ColInt.MMD.Samples.Character.Components;
using ColInt.MMD.Samples.Character.Input.Components;
using ColInt.MMD.Samples.Character.Movement.Facing.Helpers;
using ColInt.MMD.Samples.Character.Movement.Shared.Systems;
using ColInt.MMD.Samples.Character.Movement.Trajectory.Components;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace ColInt.MMD.Samples.Character.Movement.Trajectory.Systems {
	[BurstCompile]
	[DisableAutoCreation]
	[UpdateInGroup(typeof(MmdSampleSearchQueryGroup))]
	public partial struct MmdSampleTrajectorySystem : ISystem {
		public void OnCreate(ref SystemState state) {
			state.RequireForUpdate<MmdSampleInput>();
			state.RequireForUpdate<MmdWorldState>();
		}

		[BurstCompile]
		public void OnUpdate(ref SystemState state) {
			if (SystemAPI.GetSingleton<MmdWorldState>().IsPaused) {
				return;
			}
			state.Dependency = new TrajectoryPredictionJob {
				Input = SystemAPI.GetSingleton<MmdSampleInput>(), CharacterInputs = SystemAPI.GetComponentLookup<MmdSampleCharacterInputOverride>(true), DeltaTime = SystemAPI.Time.DeltaTime,
				PhysicsRoots = SystemAPI.GetComponentLookup<MmdPhysicsRoot>(true)
			}.ScheduleParallel(state.Dependency);
		}

		[BurstCompile]
		private partial struct TrajectoryPredictionJob : IJobEntity {
			public MmdSampleInput Input;
			[ReadOnly] public ComponentLookup<MmdSampleCharacterInputOverride> CharacterInputs;
			public float DeltaTime;
			[ReadOnly] public ComponentLookup<MmdPhysicsRoot> PhysicsRoots;

			private void Execute(Entity entity, in MmdSampleCharacterControl control, in LocalTransform transform, ref MmdSampleTrajectoryState trajectory,
				ref MmdSearchQuerySpace space, DynamicBuffer<MmdSearchQueryElement> query, DynamicBuffer<MmdTrajectoryPoint> points) {
				var input = CharacterInputs.HasComponent(entity) ? CharacterInputs[entity].Value : Input;
				space = new MmdSearchQuerySpace { Position = transform.Position, Rotation = transform.Rotation };
				points.ResizeUninitialized(3);
				float desiredSpeed = math.lengthsq(input.Movement) > 0f ? input.Run ? control.RunSpeed : control.WalkSpeed : 0f;
				float2 facing = MmdSampleFacing.Resolve(input.Movement);
				float error = MmdSampleFacing.YawError(transform.Rotation, facing, trajectory.TurnError);
				trajectory.TurnError = error;
				float3 velocity = math.rotate(math.inverse(transform.Rotation), trajectory.Velocity);
				query[12] = new MmdSearchQueryElement { Value = velocity.x };
				query[13] = new MmdSearchQueryElement { Value = velocity.y };
				query[14] = new MmdSearchQueryElement { Value = velocity.z };
				float speed = math.length(trajectory.Velocity.xz);
				bool backwardStart = speed < desiredSpeed * 0.15f && math.abs(error) > 2.0943951f;
				float3 position = float3.zero;
				float previousTime = 0f;
				for (int point = 0; point < 3; point++) {
					float time = LocomotionSchema.PredictionTimes[point];
					float step = (time - previousTime) / 8f;
					for (int sample = 1; sample <= 8; sample++) {
						float futureTime = previousTime + sample * step;
						float futureYaw = math.clamp(error, -control.TurnSpeed * futureTime, control.TurnSpeed * futureTime);
						// Brake for a reversal; accelerate as the predicted facing reaches the requested heading.
						float targetSpeed = backwardStart ? desiredSpeed : desiredSpeed * math.saturate((math.cos(error - futureYaw) + 1f) * 0.5f);
						speed += math.clamp(targetSpeed - speed, -10f * step, 10f * step);
						// A start from rest can step backward while its facing turns independently.
						float movementYaw = backwardStart ? error : futureYaw;
						position += new float3(math.sin(movementYaw), 0f, math.cos(movementYaw)) * speed * step;
					}
					float yaw = math.clamp(error, -control.TurnSpeed * time, control.TurnSpeed * time);
					query[point * 4] = new MmdSearchQueryElement { Value = position.x };
					query[point * 4 + 1] = new MmdSearchQueryElement { Value = position.z };
					query[point * 4 + 2] = new MmdSearchQueryElement { Value = math.sin(yaw) };
					query[point * 4 + 3] = new MmdSearchQueryElement { Value = math.cos(yaw) };
					points[point] = new MmdTrajectoryPoint {
						Time = time, Position = transform.Position + math.rotate(transform.Rotation, position),
						Forward = math.rotate(transform.Rotation, new float3(math.sin(yaw), 0f, math.cos(yaw)))
					};
					previousTime = time;
				}
				if (!PhysicsRoots.HasComponent(entity)) {
					// Preview actors have no physics feedback; retain their simulated acceleration.
					float yaw = math.clamp(error, -control.TurnSpeed * DeltaTime, control.TurnSpeed * DeltaTime);
					float target = desiredSpeed * math.saturate((math.cos(error - yaw) + 1f) * 0.5f);
					float nextSpeed = math.length(trajectory.Velocity.xz);
					nextSpeed += math.clamp(target - nextSpeed, -10f * DeltaTime, 10f * DeltaTime);
					trajectory.Velocity = math.rotate(transform.Rotation, new float3(math.sin(yaw), 0f, math.cos(yaw))) * nextSpeed;
				}
			}
		}
	}
}
