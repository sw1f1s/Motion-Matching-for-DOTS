using System.Runtime.CompilerServices;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;

namespace ColInt.MMD.Physics.Helpers {
	public struct MmdGroundCastCollector : ICollector<ColliderCastHit> {
		public Entity Owner;
		public float MinimumGroundNormalY;
		public float3 Displacement;
		public bool FindGround;
		public ColliderCastHit ClosestHit;

		public bool EarlyOutOnFirstHit => false;
		public float MaxFraction { get; private set; }
		public int NumHits { get; private set; }

		public MmdGroundCastCollector(Entity owner, float minimumGroundNormalY, float3 displacement, bool findGround) {
			Owner = owner;
			MinimumGroundNormalY = minimumGroundNormalY;
			Displacement = displacement;
			FindGround = findGround;
			ClosestHit = default;
			MaxFraction = 1f;
			NumHits = 0;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool AddHit(ColliderCastHit hit) {
			if (hit.Entity == Owner || hit.Material.CollisionResponse == CollisionResponsePolicy.RaiseTriggerEvents ||
				hit.Material.CollisionResponse == CollisionResponsePolicy.None || hit.Fraction > MaxFraction) {
				return false;
			}
			if (FindGround) {
				if (hit.SurfaceNormal.y < MinimumGroundNormalY) {
					return false;
				}
			} else if (hit.SurfaceNormal.y >= MinimumGroundNormalY || math.dot(Displacement, hit.SurfaceNormal) >= -0.00001f) {
				return false;
			}
			ClosestHit = hit;
			MaxFraction = hit.Fraction;
			NumHits = 1;
			return true;
		}
	}
}
