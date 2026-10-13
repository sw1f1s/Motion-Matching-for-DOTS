using Unity.Entities;

namespace ColInt.MMD.MotionMatching.Components {
	public struct MmdSearchSettings : IComponentData {
		public bool PreferLoop;
		public float Interval;
		public float SwitchPenalty;
		public float MinimumRemainingTime;
		public float MinimumSwitchTime;
		public float MinimumTransitionTime;
		public float TrajectoryDirectionWeight;
		public float TrajectoryPositionWeight;
	}

}
