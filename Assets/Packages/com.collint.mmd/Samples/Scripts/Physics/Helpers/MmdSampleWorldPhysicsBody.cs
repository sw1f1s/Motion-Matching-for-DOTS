using Unity.Entities;
using Unity.Mathematics;

namespace ColInt.MMD.Samples.Physics.Helpers {
	public readonly struct MmdSampleWorldPhysicsBody {
		public readonly string Name;
		public readonly RigidTransform Transform;
		public readonly BlobAssetReference<Unity.Physics.Collider> Collider;

		public MmdSampleWorldPhysicsBody(string name, RigidTransform transform, BlobAssetReference<Unity.Physics.Collider> collider) {
			Name = name;
			Transform = transform;
			Collider = collider;
		}
	}
}
