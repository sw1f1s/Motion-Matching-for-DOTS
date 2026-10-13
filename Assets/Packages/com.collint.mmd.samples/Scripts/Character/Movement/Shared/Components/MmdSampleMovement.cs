using Unity.Entities;
using Unity.Mathematics;

namespace ColInt.MMD.Samples.Character.Movement.Shared.Components {
	// Movement policy set by state lifecycle; physics never dispatches on state types.
	public struct MmdSampleMovement : IComponentData {
		public bool IgnoreGround;
		public bool AirControl;
		public bool CrouchCollider;
		public bool TraversalCollider;
		public float2 AirVelocity;
	}
}
