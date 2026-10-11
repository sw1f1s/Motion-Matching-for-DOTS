using Unity.Entities;

namespace ColInt.MMD.Physics.Components {
	// Only for bodies whose movement is owned by physics; excludes kinematic sample owner.
	public struct MmdPhysicsRoot : IComponentData { }

}
