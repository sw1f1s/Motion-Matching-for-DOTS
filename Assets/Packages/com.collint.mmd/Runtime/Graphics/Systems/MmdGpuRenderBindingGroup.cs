using ColInt.MMD.Playback.Systems;
using Unity.Entities;
using Unity.Scenes;

namespace ColInt.MMD.Graphics.Systems {
	[DisableAutoCreation]
	[UpdateInGroup(typeof(InitializationSystemGroup))]
	[UpdateAfter(typeof(SceneSystemGroup))]
	[UpdateAfter(typeof(MmdAnimationInitializationGroup))]
	[UpdateAfter(typeof(MmdGpuRenderRequestGroup))]
	public partial class MmdGpuRenderBindingGroup : ComponentSystemGroup { }
}
