using ColInt.MMD.Database.Components;
using ColInt.MMD.Debugging.Components;
using ColInt.MMD.Debugging.Helpers;
using ColInt.MMD.MotionMatching.Helpers;
using ColInt.MMD.Playback.Components;
using ColInt.MMD.Playback.Helpers;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace ColInt.MMD.Debugging.Systems {
	[DisableAutoCreation]
	[UpdateInGroup(typeof(MmdTrajectoryDebugGroup))]
	public partial struct MmdClipTrajectoryDebugSystem : ISystem {
		private EntityQuery _characters;

		public void OnCreate(ref SystemState state) {
			_characters = new EntityQueryBuilder(Allocator.Temp).WithAll<MmdTrajectoryDebug, LocalTransform, MmdDatabase, MmdPlayback>().Build(ref state);
			state.RequireForUpdate(_characters);
		}

		public void OnUpdate(ref SystemState state) {
#if UNITY_EDITOR
			foreach (var (settings, transform, data, playback) in SystemAPI.Query<RefRO<MmdTrajectoryDebug>, RefRO<LocalTransform>, RefRO<MmdDatabase>, RefRO<MmdPlayback>>()) {
				var database = data.ValueRO.Value;
				int clip = playback.ValueRO.ClipIndex;
				if (!settings.ValueRO.DrawClipTrajectory || !database.IsCreated || clip < 0 || clip >= database.Value.Clips.Length) {
					continue;
				}
				var root = MmdTrajectoryDebugGeometry.ToWorld(transform.ValueRO, float3.zero, new float3(0f, 0f, 1f), settings.ValueRO.Height + 0.02f);
				float3 previous = root.Position;
				RigidTransform start = MmdAnimationMath.RootAt(ref database.Value, clip, playback.ValueRO.Time);
				for (int index = 0; index < 3; index++) {
					RigidTransform future = MmdAnimationMath.RootAt(ref database.Value, clip, playback.ValueRO.Time + LocomotionSchema.PredictionTimes[index]);
					RigidTransform delta = math.mul(math.inverse(start), future);
					var point = MmdTrajectoryDebugGeometry.ToWorld(transform.ValueRO, delta.pos, math.rotate(delta.rot, new float3(0f, 0f, 1f)), settings.ValueRO.Height + 0.02f);
					MmdTrajectoryDebugDrawing.DrawPoint(point, previous, settings.ValueRO, MmdTrajectoryDebugDrawing.ClipColor);
					previous = point.Position;
				}
			}
#endif
		}
	}
}
