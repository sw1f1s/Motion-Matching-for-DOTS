using Unity.Entities;

namespace ColInt.MMD.Lifecycle.Components {
	// Host copies its own pause state here before the MMD group runs.
	public struct MmdWorldState : IComponentData {
		public bool IsPaused;
	}

}
