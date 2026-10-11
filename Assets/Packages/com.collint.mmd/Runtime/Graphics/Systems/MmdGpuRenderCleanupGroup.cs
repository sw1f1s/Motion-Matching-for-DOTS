using Unity.Entities;
using Unity.Scenes;

namespace ColInt.MMD.Graphics.Systems {
	[DisableAutoCreation]
	[UpdateInGroup(typeof(InitializationSystemGroup))]
	[UpdateBefore(typeof(SceneSystemGroup))]
	public partial class MmdGpuRenderCleanupGroup : ComponentSystemGroup { }
}
