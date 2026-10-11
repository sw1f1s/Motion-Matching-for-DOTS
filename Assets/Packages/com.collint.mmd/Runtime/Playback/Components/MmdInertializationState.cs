using Unity.Entities;
using Unity.Mathematics;

namespace ColInt.MMD.Playback.Components {
	public struct MmdInertializationState : IComponentData {
		public uint Revision;
		public float Elapsed;
		public float PoseDuration;
		public int PreviousClipIndex;
		public float3 PreviousFootCenter;
		public float3 StationaryPoseOffset;
		public float3 RootVelocity;
		public float3 RootAngularVelocity;
		public float3 RootVelocityOffset;
		public float3 RootAngularOffset;
	}
}
