using ColInt.MMD.Presentation.Systems;
using Unity.Entities;
using Unity.Rendering;

namespace ColInt.MMD.Graphics.Systems {
	[DisableAutoCreation]
	[UpdateInGroup(typeof(PresentationSystemGroup))]
	[UpdateAfter(typeof(MmdPresentationGroup))]
	[UpdateBefore(typeof(DeformationsInPresentation))]
	public partial class MmdSkinningGroup : ComponentSystemGroup { }

}
