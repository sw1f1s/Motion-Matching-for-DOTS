using System.Runtime.CompilerServices;
using Unity.Entities;
using Unity.Physics;

namespace ColInt.MMD.Physics.Helpers {
	public struct MmdGroundRayCollector : ICollector<RaycastHit> {
		public Entity Owner;
		public float MinimumNormalY;
		public RaycastHit ClosestHit;

		public bool EarlyOutOnFirstHit => false;
		public float MaxFraction { get; private set; }
		public int NumHits { get; private set; }

		public MmdGroundRayCollector(Entity owner, float minimumNormalY) {
			Owner = owner;
			MinimumNormalY = minimumNormalY;
			ClosestHit = default;
			MaxFraction = 1f;
			NumHits = 0;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool AddHit(RaycastHit hit) {
			if (hit.Entity == Owner || hit.Material.CollisionResponse == CollisionResponsePolicy.RaiseTriggerEvents ||
				hit.Material.CollisionResponse == CollisionResponsePolicy.None || hit.Fraction > MaxFraction || hit.SurfaceNormal.y < MinimumNormalY) {
				return false;
			}
			ClosestHit = hit;
			MaxFraction = hit.Fraction;
			NumHits = 1;
			return true;
		}
	}
}
