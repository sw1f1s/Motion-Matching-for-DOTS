using Unity.Entities;

namespace ColInt.MMD.Samples.Character.Movement.TraversalState.Components {
	[InternalBufferCapacity(0)]
	public struct MmdSampleTraversalClip : IBufferElementData {
		public int ClipIndex;
		public int ActionId;
		public float StartTime;
		public float RootHeight;
		public float ClearanceTime;
		public float LiftTime;
		public float PeakTime;
		public float TransferTime;
		public float Duration;
		public float EntrySpeed;
		public float ExitSpeed;
		public float CatchHeight;
	}
}
