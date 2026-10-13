using Unity.Entities;

namespace ColInt.MMD.Samples.Character.Movement.CrouchState.Components {
	public struct MmdSampleCrouch : IComponentData {
		public float Speed;
		public float AnimationSpeed;
		public float TransitionDuration;
		public int IdleGroup;
		public int MoveGroup;
		public int TurnGroup;
		public int StartGroup;
		public int EnterAction;
		public int ExitAction;
	}
}
