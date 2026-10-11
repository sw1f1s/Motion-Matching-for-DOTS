using Unity.Entities;

namespace ColInt.MMD.Graphics.Systems {
	[DisableAutoCreation]
	[UpdateInGroup(typeof(MmdGpuRenderBindingGroup))]
	[UpdateAfter(typeof(MmdGpuRenderInstantiationGroup))]
	public partial class MmdGpuRenderAttachmentGroup : ComponentSystemGroup { }
}
