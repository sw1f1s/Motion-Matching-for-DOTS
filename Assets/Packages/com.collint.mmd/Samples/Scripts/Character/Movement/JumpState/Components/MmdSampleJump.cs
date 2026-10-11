using Unity.Entities;

namespace ColInt.MMD.Samples.Character.Movement.JumpState.Components {
	public struct MmdSampleJump : IComponentData {
		public int StandAction;
		public int WalkAction;
		public int RunAction;
	}
}
