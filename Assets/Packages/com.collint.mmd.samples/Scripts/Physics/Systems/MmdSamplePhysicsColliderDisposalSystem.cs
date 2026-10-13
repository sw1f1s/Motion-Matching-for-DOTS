using ColInt.MMD.Samples.Physics.Components;
using Unity.Collections;
using Unity.Entities;

namespace ColInt.MMD.Samples.Physics.Systems {
	[DisableAutoCreation]
	[UpdateInGroup(typeof(MmdSamplePhysicsCleanupGroup))]
	public partial struct MmdSamplePhysicsColliderDisposalSystem : ISystem {
		private EntityQuery _owners;

		public void OnCreate(ref SystemState state) {
			_owners = new EntityQueryBuilder(Allocator.Temp).WithAll<MmdSamplePhysicsColliderOwner>()
				.WithOptions(EntityQueryOptions.IncludeDisabledEntities | EntityQueryOptions.IncludePrefab).Build(ref state);
		}

		public void OnDestroy(ref SystemState state) {
			state.Dependency.Complete();
			using (var entities = _owners.ToEntityArray(Allocator.Temp)) {
				foreach (Entity entity in entities) {
					var owner = state.EntityManager.GetComponentData<MmdSamplePhysicsColliderOwner>(entity);
					var collider = owner.Value;
					collider.Dispose();
					var traversal = owner.Traversal;
					if (traversal.IsCreated) {
						traversal.Dispose();
					}
					var crouched = owner.Crouched;
					if (crouched.IsCreated) {
						crouched.Dispose();
					}
					state.EntityManager.RemoveComponent<MmdSamplePhysicsColliderOwner>(entity);
				}
			}
		}
	}
}
