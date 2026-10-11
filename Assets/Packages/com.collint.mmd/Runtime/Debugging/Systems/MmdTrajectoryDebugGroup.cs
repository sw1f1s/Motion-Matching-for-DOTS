using Unity.Entities;

namespace ColInt.MMD.Debugging.Systems {
	[DisableAutoCreation]
	[UpdateInGroup(typeof(PresentationSystemGroup))]
	public partial class MmdTrajectoryDebugGroup : ComponentSystemGroup { }
}
