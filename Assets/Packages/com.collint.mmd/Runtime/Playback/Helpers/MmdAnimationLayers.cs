using ColInt.MMD.Lifecycle.Components;
using System;
using ColInt.MMD.Database.Components;
using ColInt.MMD.MotionMatching.Components;
using ColInt.MMD.Playback.Components;
using ColInt.MMD.Playback.Data;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace ColInt.MMD.Playback.Helpers {
	public static class MmdAnimationLayers {
		// Definitions belong to the external owner and outlive every attached actor.
		public static void Add(EntityManager manager, Entity character, MmdAnimationLayerSet set) {
			if (!manager.HasComponent<MmdDatabase>(character) || !manager.HasComponent<MmdPlayback>(character) ||
				!manager.HasComponent<MmdFault>(character) || !manager.HasBuffer<MmdLocalPose>(character) ||
				!manager.HasBuffer<MmdSearchQueryElement>(character) || set == null || !set.Definition.IsCreated || set.Database != manager.GetComponentData<MmdDatabase>(character).Value) {
				throw new ArgumentException("The layer definition must belong to the character's database.");
			}
			if (manager.HasComponent<MmdAnimationLayersDefinitionReference>(character) || manager.HasBuffer<MmdAnimationLayer>(character) ||
				manager.HasBuffer<MmdAnimationLayerControl>(character) || manager.HasBuffer<MmdAnimationLayerRequest>(character) ||
				manager.HasBuffer<MmdAnimationLayerPose>(character) || manager.HasBuffer<MmdComposedPose>(character)) {
				throw new ArgumentException("Animation layers are already configured on this character.");
			}
			var definition = set.Definition;
			manager.AddComponentData(character, new MmdAnimationLayersDefinitionReference { Value = definition });
			manager.AddBuffer<MmdAnimationLayer>(character);
			manager.AddBuffer<MmdAnimationLayerControl>(character);
			manager.AddBuffer<MmdAnimationLayerRequest>(character);
			manager.AddBuffer<MmdAnimationLayerPose>(character);
			manager.AddBuffer<MmdComposedPose>(character);
			var layers = manager.GetBuffer<MmdAnimationLayer>(character);
			var controls = manager.GetBuffer<MmdAnimationLayerControl>(character);
			manager.GetBuffer<MmdAnimationLayerPose>(character).Resize(definition.Value.PoseCount, NativeArrayOptions.ClearMemory);
			for (int index = 0; index < definition.Value.Layers.Length; index++) {
				ref var source = ref definition.Value.Layers[index];
				layers.Add(new MmdAnimationLayer {
					ClipIndex = source.ClipIndex, Time = source.Time, MatchedFrame = -1,
					Weight = source.FadeDuration <= 0f ? source.Weight : 0f, TransitionElapsed = source.TransitionDuration
				});
				controls.Add(new MmdAnimationLayerControl {
					Source = source.Source, GroupId = source.GroupId, Speed = source.Speed,
					TargetWeight = source.Weight, FadeDuration = source.FadeDuration
				});
			}
		}

		public static void SetWeight(DynamicBuffer<MmdAnimationLayerRequest> requests, int index, float weight, float fadeDuration) {
			if (index < 0 || !math.isfinite(weight) || weight < 0f || weight > 1f || !math.isfinite(fadeDuration) || fadeDuration < 0f) {
				throw new ArgumentException("A valid layer index, weight in 0–1 and nonnegative fade duration are required.");
			}
			requests.Add(new MmdAnimationLayerRequest { LayerIndex = index, Weight = weight, FadeDuration = fadeDuration });
		}

		public static void Play(DynamicBuffer<MmdAnimationLayerRequest> requests, int index, int clip, float time, float speed) {
			if (index < 0 || clip < 0 || !math.isfinite(time) || time < 0f || !math.isfinite(speed) || speed < 0f) {
				throw new ArgumentException("A valid layer/clip index and nonnegative finite playback timing are required.");
			}
			requests.Add(new MmdAnimationLayerRequest { LayerIndex = index, Operation = MmdAnimationLayerOperation.Play, ClipIndex = clip, Time = time, Speed = speed });
		}

		public static void SetGroup(DynamicBuffer<MmdAnimationLayerRequest> requests, int index, int group, float speed) {
			if (index < 0 || group <= 0 || !math.isfinite(speed) || speed <= 0f) {
				throw new ArgumentException("A valid layer index, positive group ID and positive finite speed are required.");
			}
			requests.Add(new MmdAnimationLayerRequest { LayerIndex = index, Operation = MmdAnimationLayerOperation.SetGroup, GroupId = group, Speed = speed });
		}
	}
}
