using System;
using ColInt.MMD.Physics.Systems;
using ColInt.MMD.Playback.Systems;
using Unity.Entities;

namespace ColInt.MMD.Physics.Helpers {
	public static class MmdPhysicsInstaller {
		// Call after MmdSystemInstaller.Install. The host still owns physics and the fixed tick.
		public static void Install(World world, ComponentSystemGroup fixedSimulationParent) {
			if (world == null || !world.IsCreated || fixedSimulationParent == null || fixedSimulationParent.World != world) {
				throw new ArgumentException("An existing world and its fixed simulation group are required.");
			}
			var simulation = world.GetExistingSystemManaged<MmdSimulationGroup>();
			if (simulation == null) {
				throw new InvalidOperationException("Install MMD simulation before its physics integration.");
			}
			var constraints = world.GetOrCreateSystemManaged<MmdTrajectoryConstraintGroup>();
			constraints.AddSystemToUpdateList(world.GetOrCreateSystem<MmdPhysicsTrajectorySystem>());
			simulation.AddSystemToUpdateList(constraints);
			simulation.SortSystems();
			var preparation = world.GetOrCreateSystemManaged<MmdPhysicsPreparationGroup>();
			preparation.AddSystemToUpdateList(world.GetOrCreateSystem<MmdPhysicsRootSystem>());
			preparation.AddSystemToUpdateList(world.GetOrCreateSystem<MmdGroundedPhysicsRootSystem>());
			fixedSimulationParent.AddSystemToUpdateList(preparation);
			fixedSimulationParent.SortSystems();
		}
	}
}
