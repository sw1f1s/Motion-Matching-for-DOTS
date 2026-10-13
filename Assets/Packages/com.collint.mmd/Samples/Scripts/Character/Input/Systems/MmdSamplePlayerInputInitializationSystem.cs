using ColInt.MMD.Samples.Character.Input.Components;
using Unity.Collections;
using Unity.Entities;

namespace ColInt.MMD.Samples.Character.Input.Systems {
	[DisableAutoCreation]
	[UpdateInGroup(typeof(MmdSamplePlayerInputGroup), OrderFirst = true)]
	public partial struct MmdSamplePlayerInputInitializationSystem : ISystem {
		private EntityQuery _pending;

		public void OnCreate(ref SystemState state) {
			_pending = new EntityQueryBuilder(Allocator.Temp).WithAll<MmdSamplePlayerInput>().WithNone<MmdSampleInput>().Build(ref state);
			state.RequireForUpdate(_pending);
		}

		public void OnUpdate(ref SystemState state) {
			using var commands = new EntityCommandBuffer(Allocator.Temp);
			foreach (var (_, entity) in SystemAPI.Query<RefRO<MmdSamplePlayerInput>>().WithNone<MmdSampleInput>().WithEntityAccess()) {
				commands.AddComponent(entity, new MmdSampleInput { Run = true });
			}
			commands.Playback(state.EntityManager);
		}
	}
}
