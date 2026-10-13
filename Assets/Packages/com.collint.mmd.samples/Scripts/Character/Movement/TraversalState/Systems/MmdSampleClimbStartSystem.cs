using ColInt.MMD.Actions.Components;
using ColInt.MMD.Lifecycle.Components;
using ColInt.MMD.Playback.Components;
using ColInt.MMD.Samples.Character.Movement.Shared.Components;
using ColInt.MMD.Samples.Character.Movement.Shared.Systems;
using ColInt.MMD.Samples.Character.Movement.TraversalState.Components;
using ColInt.MMD.Samples.Character.Movement.TraversalState.Helpers;
using Unity.Collections;
using Unity.Entities;
using Unity.Physics;
using Unity.Mathematics;

namespace ColInt.MMD.Samples.Character.Movement.TraversalState.Systems {
	[DisableAutoCreation]
	[UpdateInGroup(typeof(MmdSampleStateStartGroup))]
	public partial struct MmdSampleClimbStartSystem : ISystem {
		public void OnCreate(ref SystemState state) {
			state.RequireForUpdate<MmdWorldState>();
			state.RequireForUpdate<MmdSampleStateStarted<MmdSampleClimb>>();
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

		[WithAll(typeof(MmdSampleStateStarted<MmdSampleClimb>))]
		private partial struct StartJob : IJobEntity {
			public EntityCommandBuffer Commands;

			private void Execute(Entity entity, in MmdSampleTraversalTarget target, ref MmdSampleTraversalProgress progress, ref MmdSampleMovement movement,
				ref MmdActionRequest request, ref MmdPlayback playback, ref PhysicsMassOverride massOverride,
				ref PhysicsGravityFactor gravity, ref MmdInertialization smoothing) {
				MmdSampleTraversalAnimation.Start(ref progress, ref movement, ref request, ref playback, ref massOverride, ref gravity, ref smoothing);
				movement.TraversalCollider = target.Across;
				if (math.distancesq(target.Front, target.Start) < 0.01f) {
					smoothing.PoseDuration = math.min(smoothing.PoseDuration, 0.1f);
				}
				Commands.AddComponent<MmdSampleTraversalActive>(entity);
				Commands.AddComponent<MmdSampleTraversalEntry>(entity);
			}
		}
	}
}
