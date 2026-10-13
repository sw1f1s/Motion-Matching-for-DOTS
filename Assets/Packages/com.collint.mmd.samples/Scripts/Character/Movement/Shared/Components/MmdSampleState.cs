using Unity.Entities;

namespace ColInt.MMD.Samples.Character.Movement.Shared.Components {
	public struct MmdSampleState<T> : IComponentData where T : unmanaged, IComponentData {
		public float Elapsed;
	}
}
