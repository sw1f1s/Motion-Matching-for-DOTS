using ColInt.MMD.Database.Components;
using ColInt.MMD.Database.Data;
using ColInt.MMD.Lifecycle.Components;
using ColInt.MMD.Playback.Components;
using ColInt.MMD.Playback.Data;
using ColInt.MMD.Playback.Helpers;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;

namespace ColInt.MMD.Playback.Systems {
	[BurstCompile]
	[DisableAutoCreation]
	[UpdateInGroup(typeof(MmdAnimationLayerGroup))]
	[UpdateAfter(typeof(MmdAnimationLayerSearchGroup))]
	public partial struct MmdAnimationLayerSystem : ISystem {
		public void OnCreate(ref SystemState state) {
			state.RequireForUpdate<MmdWorldState>();
		}

		[BurstCompile]
		public void OnUpdate(ref SystemState state) {
			if (SystemAPI.GetSingleton<MmdWorldState>().IsPaused) {
				return;
			}
			state.Dependency = new LayerJob { DeltaTime = SystemAPI.Time.DeltaTime }.ScheduleParallel(state.Dependency);
		}

		[BurstCompile]
		private partial struct LayerJob : IJobEntity {
			public float DeltaTime;

			private void Execute(in MmdDatabase data, in MmdPlayback playback, in MmdAnimationLayersDefinitionReference definitions,
				in DynamicBuffer<MmdLocalPose> poses, in DynamicBuffer<MmdAnimationLayerControl> controls, DynamicBuffer<MmdAnimationLayer> layers,
				DynamicBuffer<MmdAnimationLayerPose> history, DynamicBuffer<MmdComposedPose> composed) {
				if (DeltaTime <= 0f) {
					return;
				}
				composed.ResizeUninitialized(poses.Length);
				for (int bone = 0; bone < poses.Length; bone++) {
					composed[bone] = new MmdComposedPose { Value = poses[bone].Value };
				}
				ref var database = ref data.Value.Value;
				for (int index = 0; index < layers.Length; index++) {
					var layer = layers[index];
					var control = controls[index];
					ref var definition = ref definitions.Value.Value.Layers[index];
					if (!MmdAnimationLayerMath.Valid(control, layer, database.Clips.Length)) {
						layer.Status = MmdAnimationLayerStatus.InvalidSettings;
						layers[index] = layer;
						continue;
					}
					float targetWeight = MmdAnimationLayerMath.Active(ref database, playback, ref definition) ? control.TargetWeight : 0f;
					if (layer.Weight <= 0f && targetWeight <= 0f) {
						layer.Status = MmdAnimationLayerStatus.Inactive;
						layers[index] = layer;
						continue;
					}
					layer.Weight = control.FadeDuration <= 0f ? targetWeight : layer.Weight + math.clamp(targetWeight - layer.Weight, -DeltaTime / control.FadeDuration, DeltaTime / control.FadeDuration);
					if (control.Source == MmdAnimationLayerSource.Group && layer.Status != MmdAnimationLayerStatus.Ready) {
						layers[index] = layer;
						continue;
					}
					layer.Status = MmdAnimationLayerStatus.Ready;
					ref var clip = ref database.Clips[layer.ClipIndex];
					double next = layer.Time + (double)DeltaTime * control.Speed;
					layer.Time = clip.Loop ? (float)math.fmod(next, clip.Duration) : (float)math.min(next, clip.Duration);
					layer.TransitionElapsed = math.min(layer.TransitionElapsed + DeltaTime, definition.TransitionDuration);
					for (int slot = 0; slot < definition.Bones.Length; slot++) {
						int bone = definition.Bones[slot].BoneIndex;
						int entryIndex = definition.FirstPose + slot;
						var entry = history[entryIndex];
						var target = MmdAnimationMath.PoseAt(ref database, layer.ClipIndex, layer.Time, bone);
						target.Position += MmdAnimationMath.Decay(entry.PositionOffset, float3.zero, layer.TransitionElapsed, definition.TransitionDuration);
						target.Rotation = math.normalize(math.mul(MmdAnimationMath.Rotation(MmdAnimationMath.DecayRotation(entry.RotationOffset,
							float3.zero, layer.TransitionElapsed, definition.TransitionDuration)), target.Rotation));
						target.Scale += MmdAnimationMath.Decay(entry.ScaleOffset, float3.zero, layer.TransitionElapsed, definition.TransitionDuration);
						entry.Previous = target;
						history[entryIndex] = entry;
						float weight = layer.Weight * definition.Bones[slot].Weight;
						var pose = composed[bone].Value;
						if ((definition.Channels & MotionPoseChannels.Position) != 0) {
							pose.Position = math.lerp(pose.Position, target.Position, weight);
						}
						if ((definition.Channels & MotionPoseChannels.Rotation) != 0) {
							pose.Rotation = math.slerp(pose.Rotation, target.Rotation, weight);
						}
						if ((definition.Channels & MotionPoseChannels.Scale) != 0) {
							pose.Scale = math.lerp(pose.Scale, target.Scale, weight);
						}
						composed[bone] = new MmdComposedPose { Value = pose };
					}
					if (control.Source == MmdAnimationLayerSource.Clip) {
						layer.MatchedFrame = MmdAnimationMath.FrameAt(ref database, layer.ClipIndex, layer.Time);
					}
					layers[index] = layer;
				}
			}
		}
	}
}
