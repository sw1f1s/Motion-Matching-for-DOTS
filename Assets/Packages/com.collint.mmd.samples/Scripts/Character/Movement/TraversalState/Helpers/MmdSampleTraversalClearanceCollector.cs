using Unity.Entities;
using Unity.Physics;

namespace ColInt.MMD.Samples.Character.Movement.TraversalState.Helpers {
	public struct MmdSampleTraversalClearanceCollector : ICollector<DistanceHit> {
		public Entity Owner;
		public bool Blocked;
		public bool EarlyOutOnFirstHit => true;
		public float MaxFraction => 0f;
		public int NumHits => Blocked ? 1 : 0;

		public bool AddHit(DistanceHit hit) {
			if (hit.Entity == Owner || hit.Distance >= -0.02f) {
				return false;
			}
			Blocked = true;
			return true;
		}
	}
}
