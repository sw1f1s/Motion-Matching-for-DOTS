using Unity.Entities;

namespace ColInt.MMD.Samples.Character.Movement.JumpState.Components {
	public struct MmdSampleJumpIntent : IComponentData {
		public float Remaining;
		public float TraversalRemaining;
		public float CoyoteRemaining;
	}
}
