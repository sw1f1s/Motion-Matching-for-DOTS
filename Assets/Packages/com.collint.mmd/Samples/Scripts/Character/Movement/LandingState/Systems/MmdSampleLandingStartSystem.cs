using ColInt.MMD.Actions.Components;
using ColInt.MMD.Database.Components;
using ColInt.MMD.Lifecycle.Components;
using ColInt.MMD.MotionMatching.Components;
using ColInt.MMD.Samples.Character.Movement.LandingState.Components;
using ColInt.MMD.Samples.Character.Movement.Shared.Components;
using ColInt.MMD.Samples.Character.Movement.Shared.Helpers;
using ColInt.MMD.Samples.Character.Movement.Shared.Systems;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;

namespace ColInt.MMD.Samples.Character.Movement.LandingState.Systems {
	[DisableAutoCreation]
	[UpdateInGroup(typeof(MmdSampleStateStartGroup))]
	public partial struct MmdSampleLandingStartSystem : ISystem {
		public void OnCreate(ref SystemState state) {
			state.RequireForUpdate<MmdWorldState>();
			state.RequireForUpdate<PhysicsStep>();
		}

		public void OnUpdate(ref SystemState state) {
			if (SystemAPI.GetSingleton<MmdWorldState>().IsPaused) {
				return;
			}
			state.Dependency = new StartJob { Gravity = math.abs(SystemAPI.GetSingleton<PhysicsStep>().Gravity.y) }.Schedule(state.Dependency);
		}

		[BurstCompile]
		[WithAll(typeof(MmdSampleStateStarted<MmdSampleLanding>))]
		private partial struct StartJob : IJobEntity {
			public float Gravity;

			private void Execute(in MmdSampleLanding landing, in MmdSampleMovementSettings settings, in PhysicsVelocity velocity,
				ref MmdSampleMovement movement, ref MmdSampleGroundState ground, in MmdDatabase data,
				ref MmdActionRequest request, DynamicBuffer<MmdSearchQueryElement> query) {
				movement = default;
				ground.LandingStrength = ground.MaximumDownwardSpeed * ground.MaximumDownwardSpeed / math.max(0.001f, 2f * Gravity);
				bool heavy = ground.LandingStrength >= settings.HeavyLandingHeight;
				ground.RecoveryDuration = heavy ? settings.HeavyRecoveryTime : settings.LightRecoveryTime;
				int action = heavy
					? MmdSampleAnimationRequest.GaitAction(landing.StandHeavyAction, landing.WalkHeavyAction, landing.RunHeavyAction,
						math.length(velocity.Linear.xz), settings.WalkSpeed)
					: MmdSampleAnimationRequest.GaitAction(landing.StandLightAction, landing.WalkLightAction, landing.RunLightAction,
						math.length(velocity.Linear.xz), settings.WalkSpeed);
				MmdSampleAnimationRequest.Play(ref data.Value.Value, query, action, ref request);
			}
		}
	}
}
