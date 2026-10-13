using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;

namespace ColInt.MMD.Samples.Character.Movement.TraversalState.Helpers {
	public struct MmdSampleTraversalCastCollector : ICollector<ColliderCastHit> {
		public Entity Owner;
		public float3 Displacement;
		public bool Blocked;
		public bool EarlyOutOnFirstHit => true;
		public float MaxFraction => 1f;
		public int NumHits => Blocked ? 1 : 0;

		public bool AddHit(ColliderCastHit hit) {
			if (hit.Entity == Owner || hit.Fraction < 0.02f && math.dot(Displacement, hit.SurfaceNormal) >= -0.00001f) {
				return false;
			}
			Blocked = true;
			return true;
		}
	}
}
