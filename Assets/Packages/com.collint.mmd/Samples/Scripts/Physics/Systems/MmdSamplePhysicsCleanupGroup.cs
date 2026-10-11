using Unity.Entities;

namespace ColInt.MMD.Samples.Physics.Systems {
	[DisableAutoCreation]
	[UpdateInGroup(typeof(InitializationSystemGroup))]
	public partial class MmdSamplePhysicsCleanupGroup : ComponentSystemGroup { }
}
