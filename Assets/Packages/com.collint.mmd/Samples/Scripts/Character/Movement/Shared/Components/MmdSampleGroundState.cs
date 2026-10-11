using Unity.Entities;

namespace ColInt.MMD.Samples.Character.Movement.Shared.Components {
	public struct MmdSampleGroundState : IComponentData {
		public bool IsGrounded;
		public float MaximumDownwardSpeed;
		public float LandingStrength;
		public float RecoveryDuration;
	}
}
