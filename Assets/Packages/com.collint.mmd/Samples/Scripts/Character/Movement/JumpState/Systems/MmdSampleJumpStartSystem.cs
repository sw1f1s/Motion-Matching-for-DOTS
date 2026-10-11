using ColInt.MMD.Actions.Components;
using ColInt.MMD.Database.Components;
using ColInt.MMD.Lifecycle.Components;
using ColInt.MMD.MotionMatching.Components;
using ColInt.MMD.Samples.Character.Movement.JumpState.Components;
using ColInt.MMD.Samples.Character.Movement.Shared.Components;
using ColInt.MMD.Samples.Character.Movement.Shared.Helpers;
using ColInt.MMD.Samples.Character.Movement.Shared.Systems;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;

namespace ColInt.MMD.Samples.Character.Movement.JumpState.Systems {
	[DisableAutoCreation]
	[UpdateInGroup(typeof(MmdSampleStateStartGroup))]
	public partial struct MmdSampleJumpStartSystem : ISystem {
		public void OnCreate(ref SystemState state) {
			state.RequireForUpdate<MmdWorldState>();
		}

		public void OnUpdate(ref SystemState state) {
			if (SystemAPI.GetSingleton<MmdWorldState>().IsPaused) {
				return;
			}
			using var commands = new EntityCommandBuffer(Allocator.TempJob);
			state.Dependency = new StartJob { Commands = commands }.Schedule(state.Dependency);
			state.Dependency.Complete();
			commands.Playback(state.EntityManager);
		}

		[BurstCompile]
		[WithAll(typeof(MmdSampleStateStarted<MmdSampleJump>))]
		private partial struct StartJob : IJobEntity {
			public EntityCommandBuffer Commands;

			private void Execute(Entity entity, ref MmdSampleJumpIntent intent, in MmdSampleJump jump,
				in MmdSampleMovementSettings settings, in PhysicsVelocity velocity,
				ref MmdSampleMovement movement, ref MmdSampleGroundState ground, in MmdDatabase data,
				ref MmdActionRequest request, ref MmdSampleJumpProgress progress, DynamicBuffer<MmdSearchQueryElement> query) {
				movement = new MmdSampleMovement { AirVelocity = velocity.Linear.xz, AirControl = true, IgnoreGround = true };
				intent.Remaining = 0f;
				intent.CoyoteRemaining = 0f;
				Commands.AddComponent<MmdSampleJumpImpulse>(entity);
				ground.MaximumDownwardSpeed = 0f;
				int action = MmdSampleAnimationRequest.GaitAction(jump.StandAction, jump.WalkAction, jump.RunAction,
					math.length(velocity.Linear.xz), settings.WalkSpeed);
				MmdSampleAnimationRequest.Play(ref data.Value.Value, query, action, ref request);
				progress.TakeoffTime = MmdSampleAnimationRequest.TakeoffTime(ref data.Value.Value, request.PreferredClip, settings.TakeoffTime);
			}
		}
	}
}
