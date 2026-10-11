using Unity.Entities;

namespace ColInt.MMD.Samples.Character.Movement.CrouchState.Components {
	public struct MmdSampleCrouch : IComponentData {
		public float Speed;
		public float TransitionDuration;
		public uint IdleTags;
		public uint MoveTags;
		public uint TurnTags;
		public uint StartTags;
		public int EnterAction;
		public int ExitAction;
	}
}
