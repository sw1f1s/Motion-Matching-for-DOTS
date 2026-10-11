using ColInt.MMD.Samples.Physics.Components;
using Unity.Collections;
using Unity.Entities;
using Unity.Physics;

namespace ColInt.MMD.Samples.Physics.Systems {
	[DisableAutoCreation]
	[UpdateInGroup(typeof(MmdSamplePhysicsCleanupGroup))]
	public partial struct MmdSamplePhysicsColliderCleanupSystem : ISystem {
		private EntityQuery _removed;

		public void OnCreate(ref SystemState state) {
			_removed = new EntityQueryBuilder(Allocator.Temp).WithAll<MmdSamplePhysicsColliderOwner>().WithNone<PhysicsCollider>()
				.WithOptions(EntityQueryOptions.IncludeDisabledEntities | EntityQueryOptions.IncludePrefab).Build(ref state);
			state.RequireForUpdate(_removed);
		}

		public void OnUpdate(ref SystemState state) {
			using (var commands = new EntityCommandBuffer(Allocator.Temp)) {
				foreach (var (owner, entity) in SystemAPI.Query<RefRO<MmdSamplePhysicsColliderOwner>>().WithNone<PhysicsCollider>()
					.WithOptions(EntityQueryOptions.IncludeDisabledEntities | EntityQueryOptions.IncludePrefab).WithEntityAccess()) {
					var collider = owner.ValueRO.Value;
					collider.Dispose();
					var traversal = owner.ValueRO.Traversal;
					if (traversal.IsCreated) {
						traversal.Dispose();
					}
					var crouched = owner.ValueRO.Crouched;
					if (crouched.IsCreated) {
						crouched.Dispose();
					}
					commands.RemoveComponent<MmdSamplePhysicsColliderOwner>(entity);
				}
				commands.Playback(state.EntityManager);
			}
		}
	}
}
