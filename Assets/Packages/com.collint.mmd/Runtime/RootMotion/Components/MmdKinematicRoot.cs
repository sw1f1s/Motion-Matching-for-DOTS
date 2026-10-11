using Unity.Entities;

namespace ColInt.MMD.RootMotion.Components {
	// Opt-in movement owner for the sample; never combine with a physics owner.
	public struct MmdKinematicRoot : IComponentData { }

}
