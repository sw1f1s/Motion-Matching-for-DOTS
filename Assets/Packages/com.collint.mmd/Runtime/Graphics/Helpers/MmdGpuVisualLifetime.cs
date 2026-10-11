using ColInt.MMD.Graphics.Components;
using ColInt.MMD.Graphics.Data;
using Unity.Entities;
using Unity.Scenes;

namespace ColInt.MMD.Graphics.Helpers {
	public static class MmdGpuVisualLifetime {
		public static void Release(EntityCommandBuffer commands, Entity owner, MmdGpuCharacterRender cleanup, EntityStorageInfoLookup entities, BufferLookup<LinkedEntityGroup> linked, bool removeOwner) {
			if (entities.Exists(cleanup.VisualRoot)) {
				if (linked.HasBuffer(owner)) {
					var remaining = commands.SetBuffer<LinkedEntityGroup>(owner);
					var visual = linked[cleanup.VisualRoot];
					foreach (var child in linked[owner]) {
						bool owned = false;
						foreach (var visualChild in visual) {
							if (child.Value == visualChild.Value) {
								owned = true;
								break;
							}
						}
						if (!owned) {
							remaining.Add(child);
						}
					}
				}
				commands.DestroyEntity(cleanup.VisualRoot);
			}
			commands.RemoveComponent<RequestEntityPrefabLoaded>(owner);
			commands.RemoveComponent<PrefabLoadResult>(owner);
			commands.RemoveComponent<MmdGpuCharacterRenderInitialized>(owner);
			// Remove ownership only after releasing the visual and its prefab request.
			if (removeOwner) {
				commands.RemoveComponent<MmdGpuCharacterRender>(owner);
			} else {
				cleanup.VisualRoot = Entity.Null;
				cleanup.Status = MmdGpuRenderStatus.Unloaded;
				commands.SetComponent(owner, cleanup);
			}
		}
	}
}
