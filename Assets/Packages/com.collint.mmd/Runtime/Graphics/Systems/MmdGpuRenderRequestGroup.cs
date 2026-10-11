using Unity.Entities;
using Unity.Scenes;

namespace ColInt.MMD.Graphics.Systems {
	[DisableAutoCreation]
	[UpdateInGroup(typeof(InitializationSystemGroup))]
	[UpdateAfter(typeof(MmdGpuRenderCleanupGroup))]
	// Let Unity retire an old prefab reference before adding a new request on that entity.
	[UpdateAfter(typeof(SceneSystemGroup))]
	public partial class MmdGpuRenderRequestGroup : ComponentSystemGroup { }
}
