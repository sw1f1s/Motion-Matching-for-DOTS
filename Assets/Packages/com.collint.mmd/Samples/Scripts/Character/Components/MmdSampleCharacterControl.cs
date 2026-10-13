using Unity.Entities;

namespace ColInt.MMD.Samples.Character.Components {
	public struct MmdSampleCharacterControl : IComponentData {
		public float WalkSpeed;
		public float RunSpeed;
		public float SprintSpeed;
		public float TurnSpeed;
		public int IdleGroup;
		public int WalkGroup;
		public int RunGroup;
		public int SprintGroup;
		public int TurnGroup;
		public int StartGroup;
	}
}
