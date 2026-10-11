using System.Runtime.CompilerServices;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;

namespace ColInt.MMD.Samples.Character.Movement.CrouchState.Helpers {
	public static class MmdSampleCapsuleQuery {
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool CanStand(CollisionWorld world, Entity owner, BlobAssetReference<Collider> collider, in LocalTransform transform) {
			var input = new ColliderDistanceInput(collider, 0f, new RigidTransform(transform.Rotation, transform.Position), transform.Scale);
			var collector = new MmdSampleStandingClearanceCollector { Owner = owner };
			world.CalculateDistance(input, ref collector);
			return !collector.Blocked;
		}
	}
}
