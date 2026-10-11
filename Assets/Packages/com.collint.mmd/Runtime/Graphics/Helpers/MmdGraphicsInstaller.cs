using System;
using ColInt.MMD.Debugging.Systems;
using ColInt.MMD.Graphics.Systems;
using ColInt.MMD.Presentation.Systems;
using Unity.Entities;
using Unity.Scenes;

namespace ColInt.MMD.Graphics.Helpers {
	public static class MmdGraphicsInstaller {
		// Install into the host's single Entities Graphics world, after its renderer/transform systems.
		public static void Install(World world, PresentationSystemGroup parent) {
			if (world == null || !world.IsCreated || parent == null || parent.World != world) {
				throw new ArgumentException("An existing render world and its presentation group are required.");
			}
			var initialization = world.GetExistingSystemManaged<InitializationSystemGroup>();
			if (initialization == null || world.GetExistingSystemManaged<SceneSystemGroup>() == null) {
				throw new InvalidOperationException("The host must install its initialization and scene-loading systems before GPU rendering.");
			}
			var models = world.GetOrCreateSystemManaged<MmdPresentationGroup>();
			var skinning = world.GetOrCreateSystemManaged<MmdSkinningGroup>();
			models.AddSystemToUpdateList(world.GetOrCreateSystem<MmdModelMatrixSystem>());
			skinning.AddSystemToUpdateList(world.GetOrCreateSystem<MmdSkinMatrixSystem>());
			parent.AddSystemToUpdateList(models);
			parent.AddSystemToUpdateList(skinning);
#if UNITY_EDITOR
			var debug = world.GetOrCreateSystemManaged<MmdTrajectoryDebugGroup>();
			debug.AddSystemToUpdateList(world.GetOrCreateSystem<MmdTrajectoryDebugSystem>());
			debug.AddSystemToUpdateList(world.GetOrCreateSystem<MmdClipTrajectoryDebugSystem>());
			parent.AddSystemToUpdateList(debug);
#endif
			parent.SortSystems();
			var cleanup = world.GetOrCreateSystemManaged<MmdGpuRenderCleanupGroup>();
			var requests = world.GetOrCreateSystemManaged<MmdGpuRenderRequestGroup>();
			var binding = world.GetOrCreateSystemManaged<MmdGpuRenderBindingGroup>();
			cleanup.AddSystemToUpdateList(world.GetOrCreateSystem<MmdGpuRenderCleanupSystem>());
			cleanup.AddSystemToUpdateList(world.GetOrCreateSystem<MmdGpuRenderRetirementSystem>());
			requests.AddSystemToUpdateList(world.GetOrCreateSystem<MmdGpuRenderRequestSystem>());
			var instantiation = world.GetOrCreateSystemManaged<MmdGpuRenderInstantiationGroup>();
			var attachment = world.GetOrCreateSystemManaged<MmdGpuRenderAttachmentGroup>();
			instantiation.AddSystemToUpdateList(world.GetOrCreateSystem<MmdGpuRenderBindingSystem>());
			attachment.AddSystemToUpdateList(world.GetOrCreateSystem<MmdGpuRenderAttachmentSystem>());
			binding.AddSystemToUpdateList(instantiation);
			binding.AddSystemToUpdateList(attachment);
			binding.SortSystems();
			initialization.AddSystemToUpdateList(cleanup);
			initialization.AddSystemToUpdateList(requests);
			initialization.AddSystemToUpdateList(binding);
			initialization.SortSystems();
		}
	}
}
