using ColInt.MMD.Database.Data;
using Unity.Entities;
using Unity.Mathematics;

namespace ColInt.MMD.Playback.Components {
	[InternalBufferCapacity(0)]
	public struct MmdInertialPose : IBufferElementData {
		public JointPose Previous;
		public quaternion TargetModelRotation;
		public quaternion PreviousTargetModelRotation;
		public quaternion DisplayedModelRotation;
		public float3 PositionVelocity;
		public float3 AngularVelocity;
		public float3 ScaleVelocity;
		public float3 PositionOffset;
		public float3 RotationOffset;
		public float3 ScaleOffset;
		public float3 PositionVelocityOffset;
		public float3 AngularVelocityOffset;
		public float3 ScaleVelocityOffset;
	}
}
