using Unity.Entities;
using Unity.Mathematics;

namespace ColInt.MMD.Samples.Character.Movement.TraversalState.Components {
	public struct MmdSampleTraversalTarget : IComponentData {
		public Entity Obstacle;
		public RigidTransform ObstacleTransform;
		public float3 Start;
		public quaternion StartRotation;
		public quaternion Rotation;
		public float3 Front;
		public float3 Ledge;
		public float3 AboveFront;
		public float3 AboveEnd;
		public float3 End;
		public float3 ExitVelocity;
		public float Height;
		public bool Across;
		public bool Supported;
	}
}
