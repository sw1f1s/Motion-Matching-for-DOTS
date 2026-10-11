using ColInt.MMD.Debugging.Components;
using ColInt.MMD.Debugging.Data;
using ColInt.MMD.Debugging.Helpers;
using ColInt.MMD.MotionMatching.Components;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace ColInt.MMD.Debugging.Systems {
	[DisableAutoCreation]
	[UpdateInGroup(typeof(MmdTrajectoryDebugGroup))]
	public partial struct MmdTrajectoryDebugSystem : ISystem {
		private EntityQuery _characters;

		public void OnCreate(ref SystemState state) {
			_characters = new EntityQueryBuilder(Allocator.Temp).WithAll<MmdTrajectoryDebug, LocalTransform, MmdSearchQueryElement>().Build(ref state);
			state.RequireForUpdate(_characters);
		}

		public void OnUpdate(ref SystemState state) {
#if UNITY_EDITOR
			// Run on the main thread: Debug.DrawLine is a Unity API. An in buffer
			// parameter preserves read-only access, unlike SystemAPI.Query buffers.
			new DrawTrajectoryJob {
				Spaces = SystemAPI.GetComponentLookup<MmdSearchQuerySpace>(true), Points = SystemAPI.GetBufferLookup<MmdTrajectoryPoint>(true)
			}.Run(_characters);
#endif
		}

#if UNITY_EDITOR
		private partial struct DrawTrajectoryJob : IJobEntity {
			[ReadOnly] public ComponentLookup<MmdSearchQuerySpace> Spaces;
			[ReadOnly] public BufferLookup<MmdTrajectoryPoint> Points;

			private void Execute(Entity entity, in MmdTrajectoryDebug settings, in LocalTransform transform, in DynamicBuffer<MmdSearchQueryElement> query) {
				// This component is optional for host-authored queries; their existing transform remains the default basis.
				var basis = transform;
				if (Spaces.HasComponent(entity)) {
					var space = Spaces[entity];
					basis = LocalTransform.FromPositionRotation(space.Position, space.Rotation);
				}
				var root = MmdTrajectoryDebugGeometry.ToWorld(basis, float3.zero, new float3(0f, 0f, 1f), settings.Height);
				MmdTrajectoryDebugDrawing.DrawPoint(root, root.Position, settings, MmdTrajectoryDebugDrawing.QueryColor);
				float3 previous = root.Position;
				for (int index = 0; index < 3; index++) {
					MmdTrajectoryDebugPoint point;
					if (Points.HasBuffer(entity) && Points[entity].Length == 3) {
						var sample = Points[entity][index];
						point = new MmdTrajectoryDebugPoint(sample.Position + new float3(0f, settings.Height, 0f), sample.Forward);
					} else if (!MmdTrajectoryDebugGeometry.TryReadQueryPoint(query.AsNativeArray().AsReadOnly(), basis, index, settings.Height, out point)) {
						break;
					}
					MmdTrajectoryDebugDrawing.DrawPoint(point, previous, settings, MmdTrajectoryDebugDrawing.QueryColor);
					previous = point.Position;
				}
			}
		}
#endif
	}
}
