using Unity.Entities;
using Unity.Scenes;

namespace ColInt.MMD.Playback.Systems {
	[DisableAutoCreation]
	[UpdateInGroup(typeof(InitializationSystemGroup))]
	[UpdateAfter(typeof(SceneSystemGroup))]
	public partial class MmdAnimationInitializationGroup : ComponentSystemGroup { }
}
