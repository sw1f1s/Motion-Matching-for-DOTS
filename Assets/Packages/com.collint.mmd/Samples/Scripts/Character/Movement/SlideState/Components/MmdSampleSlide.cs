using Unity.Entities;

namespace ColInt.MMD.Samples.Character.Movement.SlideState.Components {
	public struct MmdSampleSlide : IComponentData {
		public float MinimumEntrySpeed;
		public float Deceleration;
		public float StopSpeed;
		public float MaximumSpeed;
		public float SteeringSpeed;
		public float MinimumDownhillNormalY;
		public int EnterAction;
		public int MotionGroup;
	}
}
