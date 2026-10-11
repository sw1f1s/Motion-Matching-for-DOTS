using Unity.Entities;
using Unity.Mathematics;

namespace ColInt.MMD.RootMotion.Components {
	// Valid for the current simulation tick only. One movement owner consumes it.
	public struct MmdRootMotion : IComponentData {
		public float3 Translation;
		public quaternion Rotation;
		public float DeltaTime;
		public uint PlaybackRevision;
		public bool Valid;
	}

}
