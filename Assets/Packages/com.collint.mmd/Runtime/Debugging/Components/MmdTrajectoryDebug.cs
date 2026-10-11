using Unity.Entities;

namespace ColInt.MMD.Debugging.Components {
	public struct MmdTrajectoryDebug : IComponentData {
		public float Height;
		public float PointRadius;
		public float DirectionLength;
		public bool DrawClipTrajectory;

		public static MmdTrajectoryDebug Default => new MmdTrajectoryDebug {
			Height = 0.06f, PointRadius = 0.055f, DirectionLength = 0.24f, DrawClipTrajectory = true
		};
	}
}
