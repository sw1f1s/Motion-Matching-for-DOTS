using Unity.Entities;

namespace ColInt.MMD.Samples.Character.Input.Components {
	// Optional per-character input for autonomous locomotion. Player input remains world-scoped.
	public struct MmdSampleCharacterInputOverride : IComponentData {
		public MmdSampleInput Value;
	}
}
