using Unity.Entities;

namespace ColInt.MMD.Physics.Components {
	// Optional grounded locomotion policy; uses the actor's actual PhysicsCollider.
	public struct MmdGroundMovement : IComponentData {
		public float StepHeight;
		public float GroundProbeDistance;
		public float MinimumGroundNormalY;
		public float Skin;

		public static MmdGroundMovement Default => new MmdGroundMovement {
			StepHeight = 0.25f, GroundProbeDistance = 0.4f, MinimumGroundNormalY = 0.7071068f, Skin = 0.015f
		};
	}
}
