using System;
using ColInt.MMD.Actions.Systems;
using ColInt.MMD.MotionMatching.Systems;
using ColInt.MMD.Playback.Systems;
using ColInt.MMD.RootMotion.Systems;
using Unity.Entities;

namespace ColInt.MMD.Lifecycle.Helpers {
	public static class MmdSystemInstaller {
		public static void InstallInitialization(World world, InitializationSystemGroup parent) {
			if (world == null || !world.IsCreated || parent == null || parent.World != world) {
				throw new ArgumentException("An existing world and its initialization group are required.");
			}
			var initialization = world.GetOrCreateSystemManaged<MmdAnimationInitializationGroup>();
			initialization.AddSystemToUpdateList(world.GetOrCreateSystem<MmdBindPoseInitializationSystem>());
			parent.AddSystemToUpdateList(initialization);
			parent.SortSystems();
		}

		// The host owns the parent group, its fixed timestep and world lifecycle.
		public static void Install(World world, ComponentSystemGroup simulationParent) {
			if (world == null || !world.IsCreated) {
				throw new ArgumentException("An existing world is required.", nameof(world));
			}
			if (simulationParent == null || simulationParent.World != world) {
				throw new ArgumentException("The simulation parent must belong to the supplied world.", nameof(simulationParent));
			}

			var simulation = world.GetOrCreateSystemManaged<MmdSimulationGroup>();
			var actions = world.GetOrCreateSystemManaged<MmdActionGroup>();
			var search = world.GetOrCreateSystemManaged<MmdSearchGroup>();
			var playback = world.GetOrCreateSystemManaged<MmdPlaybackGroup>();
			var inertialization = world.GetOrCreateSystemManaged<MmdInertializationGroup>();
			var rootApply = world.GetOrCreateSystemManaged<MmdRootApplyGroup>();
			actions.AddSystemToUpdateList(world.GetOrCreateSystem<MmdActionSystem>());
			search.AddSystemToUpdateList(world.GetOrCreateSystem<MmdSearchSystem>());
			playback.AddSystemToUpdateList(world.GetOrCreateSystem<MmdPlaybackSystem>());
			inertialization.AddSystemToUpdateList(world.GetOrCreateSystem<MmdInertializationSystem>());
			rootApply.AddSystemToUpdateList(world.GetOrCreateSystem<MmdKinematicRootSystem>());
			simulation.AddSystemToUpdateList(actions);
			simulation.AddSystemToUpdateList(search);
			simulation.AddSystemToUpdateList(playback);
			simulation.AddSystemToUpdateList(inertialization);
			simulation.AddSystemToUpdateList(rootApply);
			simulationParent.AddSystemToUpdateList(simulation);
			simulationParent.SortSystems();
		}
	}
}
