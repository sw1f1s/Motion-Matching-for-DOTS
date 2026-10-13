using ColInt.MMD.Samples.Character.Input.Systems;
using ColInt.MMD.Samples.Character.Movement.Shared.Systems;
using Unity.Entities;

namespace ColInt.MMD.Samples.Character.Input.Helpers {
	public static class MmdSampleInputInstaller {
		public static void Install(World world, MmdSampleStateCleanupGroup cleanup) {
			var players = world.GetOrCreateSystemManaged<MmdSamplePlayerInputGroup>();
			players.AddSystemToUpdateList(world.GetOrCreateSystem<MmdSamplePlayerInputInitializationSystem>());
			players.AddSystemToUpdateList(world.GetOrCreateSystem<MmdSamplePlayerInputCleanupSystem>());
			players.AddSystemToUpdateList(world.GetOrCreateSystemManaged<MmdSamplePlayerInputSystem>());
			players.SortSystems();
			var simulation = world.GetExistingSystemManaged<SimulationSystemGroup>();
			simulation.AddSystemToUpdateList(players);
			simulation.SortSystems();
			cleanup.AddSystemToUpdateList(world.GetOrCreateSystem<MmdSampleInputCleanupSystem>());
		}
	}
}
