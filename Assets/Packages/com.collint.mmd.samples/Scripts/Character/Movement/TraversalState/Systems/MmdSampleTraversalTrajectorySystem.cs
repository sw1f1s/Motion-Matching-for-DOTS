using ColInt.MMD.Actions.Components;
using ColInt.MMD.Database.Components;
using ColInt.MMD.Lifecycle.Components;
using ColInt.MMD.MotionMatching.Components;
using ColInt.MMD.MotionMatching.Helpers;
using ColInt.MMD.Playback.Components;
using ColInt.MMD.Samples.Character.Movement.TraversalState.Components;
using ColInt.MMD.Samples.Character.Movement.TraversalState.Helpers;
using Unity.Entities;
using Unity.Mathematics;

namespace ColInt.MMD.Samples.Character.Movement.TraversalState.Systems {
	[DisableAutoCreation]
	[UpdateInGroup(typeof(MmdSampleTraversalTrajectoryGroup))]
	public partial struct MmdSampleTraversalTrajectorySystem : ISystem {
		public void OnCreate(ref SystemState state) {
			state.RequireForUpdate<MmdWorldState>();
			state.RequireForUpdate<MmdSampleTraversalActive>();
		}

		public void OnUpdate(ref SystemState state) {
			if (SystemAPI.GetSingleton<MmdWorldState>().IsPaused) {
				return;
			}
			var databases = SystemAPI.GetComponentLookup<MmdDatabase>(true);
			foreach (var (target, progress, settings, playback, action, request, points, entity) in SystemAPI.Query<RefRO<MmdSampleTraversalTarget>,
				RefRO<MmdSampleTraversalProgress>, RefRO<MmdSampleTraversalSettings>, RefRO<MmdPlayback>, RefRO<MmdActionState>,
				RefRO<MmdActionRequest>, DynamicBuffer<MmdTrajectoryPoint>>().WithAll<MmdSampleTraversalActive>().WithEntityAccess()) {
				float time = action.ValueRO.ActiveRevision == request.ValueRO.Revision ? playback.ValueRO.Time : progress.ValueRO.Clip.StartTime;
				var data = databases[entity];
				var trajectoryPoints = points;
				trajectoryPoints.ResizeUninitialized(3);
				for (int point = 0; point < 3; point++) {
					float prediction = LocomotionSchema.PredictionTimes[point];
					float future = time + prediction;
					trajectoryPoints[point] = new MmdTrajectoryPoint {
						Time = prediction, Position = MmdSampleTraversalPath.Position(ref data.Value.Value, target.ValueRO, progress.ValueRO.Clip, future),
						Forward = math.forward(MmdSampleTraversalPath.Rotation(target.ValueRO, progress.ValueRO.Clip, future, settings.ValueRO.AlignmentDuration))
					};
				}
			}
		}
	}
}
