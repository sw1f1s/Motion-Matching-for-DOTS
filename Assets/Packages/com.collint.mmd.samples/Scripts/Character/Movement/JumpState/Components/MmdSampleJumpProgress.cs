using Unity.Entities;

namespace ColInt.MMD.Samples.Character.Movement.JumpState.Components {
	public struct MmdSampleJumpProgress : IComponentData {
		public float TakeoffTime;
	}
}
