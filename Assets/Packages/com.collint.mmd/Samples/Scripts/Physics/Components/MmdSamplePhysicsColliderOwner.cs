using Unity.Entities;

namespace ColInt.MMD.Samples.Physics.Components {
	public readonly struct MmdSamplePhysicsColliderOwner : ICleanupComponentData {
		public readonly BlobAssetReference<Unity.Physics.Collider> Value;
		public readonly BlobAssetReference<Unity.Physics.Collider> Crouched;
		public readonly BlobAssetReference<Unity.Physics.Collider> Traversal;

		public MmdSamplePhysicsColliderOwner(BlobAssetReference<Unity.Physics.Collider> value) : this(value, default) { }

		public MmdSamplePhysicsColliderOwner(BlobAssetReference<Unity.Physics.Collider> value, BlobAssetReference<Unity.Physics.Collider> crouched) : this(value, crouched, default) { }

		public MmdSamplePhysicsColliderOwner(BlobAssetReference<Unity.Physics.Collider> value, BlobAssetReference<Unity.Physics.Collider> crouched,
			BlobAssetReference<Unity.Physics.Collider> traversal) {
			Value = value;
			Crouched = crouched;
			Traversal = traversal;
		}
	}
}
