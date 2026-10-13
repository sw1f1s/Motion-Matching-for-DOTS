using Unity.Entities;
using Unity.Mathematics;

namespace ColInt.MMD.Samples.Character.Movement.Trajectory.Components {
	public struct MmdSampleTrajectoryState : IComponentData {
		public float3 Velocity;
		public float2 DesiredMovement;
		public float TurnError;
	}
}
