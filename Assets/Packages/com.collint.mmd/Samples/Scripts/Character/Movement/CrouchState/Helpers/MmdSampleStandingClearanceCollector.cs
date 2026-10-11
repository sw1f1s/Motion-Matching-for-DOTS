using Unity.Entities;
using Unity.Physics;

namespace ColInt.MMD.Samples.Character.Movement.CrouchState.Helpers {
	public struct MmdSampleStandingClearanceCollector : ICollector<DistanceHit> {
		public Entity Owner;
		public bool Blocked;
		public bool EarlyOutOnFirstHit => true;
		public float MaxFraction => 0f;
		public int NumHits => Blocked ? 1 : 0;

		public bool AddHit(DistanceHit hit) {
			if (hit.Entity == Owner || hit.Distance >= -0.005f || hit.SurfaceNormal.y > 0.7f) {
				return false;
			}
			Blocked = true;
			return true;
		}
	}
}
