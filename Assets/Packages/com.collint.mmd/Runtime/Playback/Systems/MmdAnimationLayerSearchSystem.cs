using ColInt.MMD.Database.Components;
using ColInt.MMD.Database.Helpers;
using ColInt.MMD.Lifecycle.Components;
using ColInt.MMD.MotionMatching.Components;
using ColInt.MMD.MotionMatching.Helpers;
using ColInt.MMD.Playback.Components;
using ColInt.MMD.Playback.Data;
using ColInt.MMD.Playback.Helpers;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;

namespace ColInt.MMD.Playback.Systems {
	[BurstCompile]
	[DisableAutoCreation]
	[UpdateInGroup(typeof(MmdAnimationLayerSearchGroup))]
	public partial struct MmdAnimationLayerSearchSystem : ISystem {
		public void OnCreate(ref SystemState state) {
			state.RequireForUpdate<MmdWorldState>();
		}

		[BurstCompile]
		public void OnUpdate(ref SystemState state) {
			if (SystemAPI.GetSingleton<MmdWorldState>().IsPaused) {
				return;
			}
			state.Dependency = new SearchJob { DeltaTime = SystemAPI.Time.DeltaTime }.ScheduleParallel(state.Dependency);
		}

		[BurstCompile]
		private partial struct SearchJob : IJobEntity {
			public float DeltaTime;

			private void Execute(in MmdDatabase data, in MmdPlayback playback, in MmdAnimationLayersDefinitionReference definitions,
				in DynamicBuffer<MmdSearchQueryElement> query, in DynamicBuffer<MmdAnimationLayerControl> controls,
				DynamicBuffer<MmdAnimationLayer> layers, DynamicBuffer<MmdAnimationLayerPose> history) {
				if (DeltaTime <= 0f) {
					return;
				}
				ref var database = ref data.Value.Value;
				for (int index = 0; index < layers.Length; index++) {
					var control = controls[index];
					if (control.Source != MmdAnimationLayerSource.Group) {
						continue;
					}
					var layer = layers[index];
					ref var definition = ref definitions.Value.Value.Layers[index];
					if (layer.Weight <= 0f && (control.TargetWeight <= 0f || !MmdAnimationLayerMath.Active(ref database, playback, ref definition))) {
						layer.Status = MmdAnimationLayerStatus.Inactive;
						layers[index] = layer;
						continue;
					}
					if (!MmdAnimationLayerMath.Valid(control, layer, database.Clips.Length)) {
						layer.Status = MmdAnimationLayerStatus.InvalidSettings;
						layers[index] = layer;
						continue;
					}
					if (query.Length != database.FeatureCount || !Finite(query)) {
						layer.Status = MmdAnimationLayerStatus.InvalidQuery;
						layers[index] = layer;
						continue;
					}
					layer.SearchCountdown -= DeltaTime;
					bool current = MotionDatabaseGroups.Contains(ref database, database.Clips[layer.ClipIndex], control.GroupId);
					bool first = layer.MatchedFrame < 0;
					bool recovering = layer.Status != MmdAnimationLayerStatus.Ready;
					if (!first && current && !recovering && (layer.SearchCountdown > 0f || layer.TransitionElapsed < definition.TransitionDuration) ||
						layer.Status == MmdAnimationLayerStatus.NoCandidate && layer.SearchCountdown > 0f) {
						layers[index] = layer;
						continue;
					}
					int continuation = !first && current ? MmdAnimationMath.FrameAt(ref database, layer.ClipIndex, layer.Time) : -1;
					int frame = MotionSearch.Find(ref database, query, definition.Search, default, continuation, control.Speed, layer.Time, control.GroupId);
					layer.SearchCountdown = definition.Search.Interval;
					if (frame < 0) {
						layer.Status = MmdAnimationLayerStatus.NoCandidate;
					} else {
						if (frame != continuation) {
							var selected = database.Frames[frame];
							MmdAnimationLayerMath.Select(ref database, ref layer, ref definition, history, selected.ClipIndex, selected.Time);
						}
						layer.Status = MmdAnimationLayerStatus.Ready;
						layer.MatchedFrame = frame;
					}
					layers[index] = layer;
				}
			}

			private bool Finite(in DynamicBuffer<MmdSearchQueryElement> query) {
				for (int index = 0; index < query.Length; index++) {
					if (!math.isfinite(query[index].Value)) {
						return false;
					}
				}
				return true;
			}
		}
	}
}
