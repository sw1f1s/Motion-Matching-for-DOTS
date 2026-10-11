using ColInt.MMD.Physics.Components;
using Unity.Entities;

namespace ColInt.MMD.Samples.Character.Movement.Shared.Components {
	public struct MmdSampleMovementSettings : IComponentData {
		public float JumpHeight;
		public float TakeoffTime;
		public float AirAcceleration;
		public float MaximumFallSpeed;
		public float GroundContactDistance;
		public float FallEntrySpeed;
		public float HeavyLandingHeight;
		public float LightRecoveryTime;
		public float HeavyRecoveryTime;
		public float WalkSpeed;
		public float RunSpeed;
		public MmdGroundMovement Ground;
	}
}
