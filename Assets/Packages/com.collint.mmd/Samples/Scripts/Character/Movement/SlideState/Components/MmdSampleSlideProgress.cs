using Unity.Entities;
using Unity.Mathematics;

namespace ColInt.MMD.Samples.Character.Movement.SlideState.Components {
	public struct MmdSampleSlideProgress : IComponentData {
		public float3 Velocity;
	}
}
