using Unity.Entities;
using Unity.Physics;

namespace ColInt.MMD.Samples.Character.Movement.TraversalState.Helpers {
	public struct MmdSampleTraversalRayCollector : ICollector<RaycastHit> {
		public Entity Owner;
		public RaycastHit Hit;
		public float MaxFraction { get; private set; }
		public int NumHits { get; private set; }
		public bool EarlyOutOnFirstHit => false;

		public MmdSampleTraversalRayCollector(Entity owner) {
			Owner = owner;
			Hit = default;
			MaxFraction = 1f;
			NumHits = 0;
		}

		public bool AddHit(RaycastHit hit) {
			if (hit.Entity == Owner || hit.Fraction > MaxFraction) {
				return false;
			}
			Hit = hit;
			MaxFraction = hit.Fraction;
			NumHits = 1;
			return true;
		}
	}
}
