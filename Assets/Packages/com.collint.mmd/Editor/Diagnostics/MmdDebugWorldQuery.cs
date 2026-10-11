using ColInt.MMD.Database.Components;
using ColInt.MMD.Lifecycle.Components;
using ColInt.MMD.Playback.Components;
using Unity.Entities;

namespace ColInt.MMD.Editor.Diagnostics {
	public sealed class MmdDebugWorldQuery : System.IDisposable {
		public World World { get; }
		public EntityQuery Characters { get; }
		public EntityQuery State { get; }

		public MmdDebugWorldQuery(World world) {
			World = world;
			Characters = world.EntityManager.CreateEntityQuery(new EntityQueryDesc {
				All = new[] { ComponentType.ReadOnly<MmdDatabase>(), ComponentType.ReadOnly<MmdPlayback>() },
				Options = EntityQueryOptions.IncludeDisabledEntities | EntityQueryOptions.IncludePrefab
			});
			State = world.EntityManager.CreateEntityQuery(ComponentType.ReadOnly<MmdWorldState>());
		}

		public void Dispose() {
			if (World.IsCreated) {
				Characters.Dispose();
				State.Dispose();
			}
		}
	}
}
