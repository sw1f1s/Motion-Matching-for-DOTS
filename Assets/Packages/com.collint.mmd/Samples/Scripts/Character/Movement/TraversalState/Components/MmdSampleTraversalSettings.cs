using Unity.Entities;

namespace ColInt.MMD.Samples.Character.Movement.TraversalState.Components {
	public struct MmdSampleTraversalSettings : IComponentData {
		public float Reach;
		public float MinimumHeight;
		public float MaximumHeight;
		public float MantleHeight;
		public float MaximumCrossingDepth;
		public float MaximumLandingDrop;
		public float CatchReach;
		public float CatchDistance;
		public float Radius;
		public float Skin;
		public float AlignmentDuration;
	}
}
