using Unity.Entities;

namespace ColInt.MMD.Physics.Components {
	public struct MmdTrajectoryEnvironment : IComponentData {
		// Collision-limited speed over the first prediction interval; infinity means unobstructed.
		public float MaximumSpeed;
		// Resolved/requested planar travel over that interval. Unobstructed movement is 1.
		public float MovementFraction;
	}
}
