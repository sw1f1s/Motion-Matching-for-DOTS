using Unity.Entities;

namespace ColInt.MMD.Graphics.Systems {
	[DisableAutoCreation]
	[UpdateInGroup(typeof(MmdGpuRenderBindingGroup))]
	public partial class MmdGpuRenderInstantiationGroup : ComponentSystemGroup { }
}
