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
			foreach (var (fall, velocity, movement, request, settings, playback, ground) in
				SystemAPI.Query<RefRO<MmdSampleFall>, RefRO<PhysicsVelocity>, RefRW<MmdSampleMovement>, RefRW<MmdActionRequest>,
					RefRW<MmdSearchSettings>, RefRW<MmdPlayback>, RefRW<MmdSampleGroundState>>().WithAll<MmdSampleStateStarted<MmdSampleFall>>()) {
				movement.ValueRW = new MmdSampleMovement { IgnoreGround = true, AirControl = true, AirVelocity = velocity.ValueRO.Linear.xz };
				ground.ValueRW.MaximumDownwardSpeed = math.max(0f, -velocity.ValueRO.Linear.y);
				MmdSampleAnimationRequest.Cancel(ref request.ValueRW);
				settings.ValueRW.RequiredTags = fall.ValueRO.Tags;
				settings.ValueRW.ExcludedTags = 0;
				playback.ValueRW.Speed = 1f;
				playback.ValueRW.SearchCountdown = 0f;
				playback.ValueRW.TransitionElapsed = settings.ValueRO.MinimumTransitionTime;
			}
		}
	}
}
