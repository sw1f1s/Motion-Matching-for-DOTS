using Unity.Entities;

namespace ColInt.MMD.Samples.Character.Components {
	public struct MmdSampleCharacterControl : IComponentData {
		public float WalkSpeed;
		public float RunSpeed;
		public float TurnSpeed;
		public uint IdleTags;
		public uint WalkTags;
		public uint RunTags;
		public uint TurnTags;
		public uint StartTags;
	}
}
