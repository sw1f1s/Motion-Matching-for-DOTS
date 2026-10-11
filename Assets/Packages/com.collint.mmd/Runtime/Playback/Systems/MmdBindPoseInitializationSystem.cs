using ColInt.MMD.Playback.Components;
using ColInt.MMD.Presentation.Components;
using Unity.Collections;
using Unity.Entities;
using Unity.Scenes;

namespace ColInt.MMD.Playback.Systems {
	[DisableAutoCreation]
	[UpdateInGroup(typeof(MmdAnimationInitializationGroup))]
	public partial struct MmdBindPoseInitializationSystem : ISystem {
		private EntityQuery _pending;
		private BufferLookup<MmdSkeletonBone> _skeletons;
		private BufferLookup<MmdLocalPose> _poses;

		public void OnCreate(ref SystemState state) {
			_pending = new EntityQueryBuilder(Allocator.Temp).WithAll<MmdBindPoseInitialization, PrefabLoadResult>()
				.WithNone<MmdSkeletonBone, MmdLocalPose, MmdPlayback>().WithNone<MmdModelMatrix>().Build(ref state);
			state.RequireForUpdate(_pending);
			_skeletons = state.GetBufferLookup<MmdSkeletonBone>(true);
			_poses = state.GetBufferLookup<MmdLocalPose>(true);
		}

		public void OnUpdate(ref SystemState state) {
			_skeletons.Update(ref state);
			_poses.Update(ref state);
			state.Dependency.Complete();
			using (var commands = new EntityCommandBuffer(Allocator.Temp)) {
				foreach (var (result, entity) in SystemAPI.Query<RefRO<PrefabLoadResult>>().WithAll<MmdBindPoseInitialization>()
					.WithNone<MmdSkeletonBone, MmdLocalPose, MmdPlayback>().WithNone<MmdModelMatrix>().WithEntityAccess()) {
					Entity rig = result.ValueRO.PrefabRoot;
					if (!_skeletons.HasBuffer(rig) || !_poses.HasBuffer(rig)) {
						continue;
					}
					commands.AddBuffer<MmdSkeletonBone>(entity).CopyFrom(_skeletons[rig]);
					commands.AddBuffer<MmdLocalPose>(entity).CopyFrom(_poses[rig]);
					commands.AddBuffer<MmdModelMatrix>(entity);
					commands.AddComponent<MmdPlayback>(entity);
					commands.RemoveComponent<MmdBindPoseInitialization>(entity);
				}
				commands.Playback(state.EntityManager);
			}
		}
	}
}
