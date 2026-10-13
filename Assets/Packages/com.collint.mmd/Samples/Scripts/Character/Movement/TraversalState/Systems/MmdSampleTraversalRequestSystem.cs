using ColInt.MMD.Samples.Character.Movement.LocomotionState.Helpers;
using ColInt.MMD.Database.Components;
using ColInt.MMD.Lifecycle.Components;
using ColInt.MMD.MotionMatching.Components;
using ColInt.MMD.Samples.Character.Components;
using ColInt.MMD.Samples.Character.Input.Components;
using ColInt.MMD.Samples.Character.Movement.FallState.Components;
using ColInt.MMD.Samples.Character.Movement.JumpState.Components;
using ColInt.MMD.Samples.Character.Movement.LandingState.Components;
using ColInt.MMD.Samples.Character.Movement.LocomotionState.Components;
using ColInt.MMD.Samples.Character.Movement.Shared.Components;
using ColInt.MMD.Samples.Character.Movement.Shared.Helpers;
using ColInt.MMD.Samples.Character.Movement.Shared.Systems;
using ColInt.MMD.Samples.Character.Movement.TraversalState.Components;
using ColInt.MMD.Samples.Character.Movement.TraversalState.Helpers;
using ColInt.MMD.Samples.Physics.Components;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;

namespace ColInt.MMD.Samples.Character.Movement.TraversalState.Systems {
	[DisableAutoCreation]
	[UpdateInGroup(typeof(MmdSampleStateRequestGroup))]
	public partial struct MmdSampleTraversalRequestSystem : ISystem {
		public void OnCreate(ref SystemState state) {
			state.RequireForUpdate<MmdWorldState>();
			state.RequireForUpdate<MmdSampleTraversalSettings>();
			state.RequireForUpdate<MmdSampleInput>();
			state.RequireForUpdate<PhysicsWorldSingleton>();
			state.RequireForUpdate<PhysicsStep>();
		}

		public void OnUpdate(ref SystemState state) {
			var input = SystemAPI.GetSingleton<MmdSampleInput>();
			if (SystemAPI.GetSingleton<MmdWorldState>().IsPaused) {
				return;
			}
			state.Dependency = new RequestJob { Input = input, World = SystemAPI.GetSingleton<PhysicsWorldSingleton>().CollisionWorld,
				Gravity = math.abs(SystemAPI.GetSingleton<PhysicsStep>().Gravity.y) }.Schedule(state.Dependency);
		}

		[WithAny(typeof(MmdSampleState<MmdSampleLocomotion>), typeof(MmdSampleState<MmdSampleJump>), typeof(MmdSampleState<MmdSampleFall>), typeof(MmdSampleState<MmdSampleLanding>))]
		private partial struct RequestJob : IJobEntity {
			public MmdSampleInput Input;
			public float Gravity;
			[ReadOnly] public CollisionWorld World;

			private void Execute(Entity entity, ref MmdSampleJumpIntent intent, in MmdSamplePhysicsColliderOwner owner, in LocalTransform transform, in MmdSampleCharacterControl control,
				in MmdSampleGroundState ground, in PhysicsVelocity velocity, in PhysicsCollider collider, in MmdDatabase data,
				in MmdSampleTraversalSettings settings, in MmdSampleMantle mantle, in MmdSampleClimb climb,
				in MmdSampleVault vault, in MmdSampleHurdle hurdle, in MmdSampleCatchMantle catchMantle,
				in MmdSampleCatchCliff catchCliff, in MmdSampleCatchVault catchVault, in MmdSampleCatchHurdle catchHurdle,
				ref MmdSampleStateTransition transition, ref MmdSampleTraversalTarget destination,
				ref MmdSampleTraversalProgress progress, DynamicBuffer<MmdSampleTraversalClip> clips, DynamicBuffer<MmdSearchQueryElement> query) {
				if (intent.TraversalRemaining <= 0f) {
					return;
				}
				float2 direction = math.lengthsq(Input.Movement) > 0.001f ? Input.Movement : Input.Facing;
				float3 facing = math.lengthsq(direction) > 0.001f ? new float3(direction.x, 0f, direction.y) : math.forward(transform.Rotation);
				bool airborne = !ground.IsGrounded;
				var probeSettings = settings;
				if (!airborne) {
					probeSettings.Reach += math.length(velocity.Linear.xz) * 0.3f;
				}
				if (!MmdSampleTraversalQuery.Probe(World, entity, transform, facing, probeSettings, airborne, out var target)) {
					return;
				}
				float speed = math.max(math.length(velocity.Linear.xz), math.length(Input.Movement) * (MmdSampleGait.Speed(control, Input)));
				if (!airborne && math.dot(target.Front - target.Start, math.forward(target.Rotation)) < 0.08f) {
					speed = math.length(velocity.Linear.xz);
				}
				int action;
				if (airborne) {
					action = target.Across ? ((Input.Run || Input.Sprint) ? catchHurdle.ActionId : catchVault.ActionId) :
						target.Height > 1.1f ? catchCliff.ActionId : catchMantle.ActionId;
				} else {
					action = target.Across ? (speed > control.WalkSpeed * 0.5f ? hurdle.ActionId : vault.ActionId) :
						target.Height > settings.MantleHeight ? climb.ActionId : mantle.ActionId;
				}
				var selected = MmdSampleTraversalAnimation.Select(ref data.Value.Value, clips, query, action, speed, target.Height, airborne);
				selected = MmdSampleTraversalAnimation.Fit(ref data.Value.Value, selected, target, airborne);
				MmdSampleTraversalQuery.ExtendLanding(World, entity, ref target, speed);
				if (!target.Supported) {
					float3 forward = math.forward(target.Rotation);
					target.ExitVelocity = forward * math.min(MmdSampleGait.Speed(control, Input), selected.ExitSpeed);
					target.ExitVelocity.y = -math.sqrt(2f * Gravity * math.max(0f, target.AboveEnd.y - target.End.y));
					target.End += forward * (math.length(target.ExitVelocity.xz) * (selected.Duration - selected.TransferTime) * 0.5f);
				}
				var traversalCollider = target.Across ? new PhysicsCollider { Value = owner.Traversal } : collider;
				if (!MmdSampleTraversalQuery.ClearPath(ref data.Value.Value, World, entity, traversalCollider, target, selected, transform.Scale)) {
					return;
				}
				intent.Remaining = 0f;
				intent.TraversalRemaining = 0f;
				intent.CoyoteRemaining = 0f;
				destination = target;
				progress.Clip = selected;
				if (airborne) {
					if (target.Across) {
						if (Input.Run || Input.Sprint) {
							transition.Request<MmdSampleCatchHurdle>(MmdSampleStatePriority.Traversal);
						} else {
							transition.Request<MmdSampleCatchVault>(MmdSampleStatePriority.Traversal);
						}
					} else if (target.Height > 1.1f) {
						transition.Request<MmdSampleCatchCliff>(MmdSampleStatePriority.Traversal);
					} else {
						transition.Request<MmdSampleCatchMantle>(MmdSampleStatePriority.Traversal);
					}
				} else if (target.Across) {
					if (speed > control.WalkSpeed * 0.5f) {
						transition.Request<MmdSampleHurdle>(MmdSampleStatePriority.Traversal);
					} else {
						transition.Request<MmdSampleVault>(MmdSampleStatePriority.Traversal);
					}
				} else if (target.Height > settings.MantleHeight) {
					transition.Request<MmdSampleClimb>(MmdSampleStatePriority.Traversal);
				} else {
					transition.Request<MmdSampleMantle>(MmdSampleStatePriority.Traversal);
				}
			}
		}
	}
}
