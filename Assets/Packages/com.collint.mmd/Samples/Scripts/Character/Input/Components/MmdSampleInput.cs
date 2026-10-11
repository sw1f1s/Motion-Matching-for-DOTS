using Unity.Entities;
using Unity.Mathematics;

namespace ColInt.MMD.Samples.Character.Input.Components {
	public struct MmdSampleInput : IComponentData {
		// Desired movement in world X/Z, independent of camera pitch and character rotation.
		public float2 Movement;
		// Camera heading reference in world X/Z; idle locomotion does not follow it.
		public float2 Facing;
		public bool Run;
		public bool Crouch;
		public bool JumpPressed;
	}
}
