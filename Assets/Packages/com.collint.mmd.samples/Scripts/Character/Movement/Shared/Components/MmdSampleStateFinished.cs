using Unity.Entities;

namespace ColInt.MMD.Samples.Character.Movement.Shared.Components {
	public readonly struct MmdSampleStateFinished<T> : IComponentData where T : unmanaged, IComponentData { }
}
