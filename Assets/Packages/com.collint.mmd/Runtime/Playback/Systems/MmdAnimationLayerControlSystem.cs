using ColInt.MMD.Database.Components;
using ColInt.MMD.Lifecycle.Components;
using ColInt.MMD.Lifecycle.Data;
using ColInt.MMD.Playback.Components;
using ColInt.MMD.Playback.Data;
using ColInt.MMD.Playback.Helpers;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;

namespace ColInt.MMD.Playback.Systems {
	[BurstCompile]
	[DisableAutoCreation]
	[UpdateInGroup(typeof(MmdAnimationLayerControlGroup))]
	public partial struct MmdAnimationLayerControlSystem : ISystem {
		public void OnCreate(ref SystemState state) {
			state.RequireForUpdate<MmdWorldState>();
		}

		[BurstCompile]
		public void OnUpdate(ref SystemState state) {
			if (SystemAPI.GetSingleton<MmdWorldState>().IsPaused) {
				return;
			}
			state.Dependency = new ControlJob().ScheduleParallel(state.Dependency);
		}

		[BurstCompile]
		private partial struct ControlJob : IJobEntity {
			private void Execute(in MmdDatabase data, in MmdAnimationLayersDefinitionReference definitions, ref MmdFault fault,
				DynamicBuffer<MmdAnimationLayer> layers, DynamicBuffer<MmdAnimationLayerControl> controls,
				DynamicBuffer<MmdAnimationLayerRequest> requests, DynamicBuffer<MmdAnimationLayerPose> history) {
				ref var database = ref data.Value.Value;
				for (int index = 0; index < requests.Length; index++) {
					var request = requests[index];
					if (request.LayerIndex < 0 || request.LayerIndex >= layers.Length || controls.Length != layers.Length) {
						fault.Code = MmdFaultCode.InvalidLayerRequest;
						continue;
					}
					var layer = layers[request.LayerIndex];
					var control = controls[request.LayerIndex];
					ref var definition = ref definitions.Value.Value.Layers[request.LayerIndex];
					bool valid = true;
					if (request.Operation == MmdAnimationLayerOperation.SetWeight) {
						valid = math.isfinite(request.Weight) && request.Weight >= 0f && request.Weight <= 1f && math.isfinite(request.FadeDuration) && request.FadeDuration >= 0f;
						if (valid) {
							control.TargetWeight = request.Weight;
							control.FadeDuration = request.FadeDuration;
						}
					} else if (request.Operation == MmdAnimationLayerOperation.Play) {
						valid = request.ClipIndex >= 0 && request.ClipIndex < database.Clips.Length && math.isfinite(request.Time) && request.Time >= 0f && math.isfinite(request.Speed) && request.Speed >= 0f;
						if (valid) {
							MmdAnimationLayerMath.Select(ref database, ref layer, ref definition, history, request.ClipIndex,
								MmdAnimationMath.LocalTime(database.Clips[request.ClipIndex], request.Time));
							control.Source = MmdAnimationLayerSource.Clip;
							control.GroupId = 0;
							control.Speed = request.Speed;
							layer.MatchedFrame = MmdAnimationMath.FrameAt(ref database, layer.ClipIndex, layer.Time);
						}
					} else if (request.Operation == MmdAnimationLayerOperation.SetGroup) {
						valid = request.GroupId > 0 && math.isfinite(request.Speed) && request.Speed > 0f && HasGroup(data, request.GroupId);
						if (valid) {
							control.Source = MmdAnimationLayerSource.Group;
							control.GroupId = request.GroupId;
							control.Speed = request.Speed;
							layer.SearchCountdown = 0f;
							layer.Status = MmdAnimationLayerStatus.Inactive;
						}
					} else {
						valid = false;
					}
					if (!valid) {
						fault.Code = MmdFaultCode.InvalidLayerRequest;
					} else if (layer.Status == MmdAnimationLayerStatus.InvalidSettings) {
						layer.Status = MmdAnimationLayerStatus.Ready;
					}
					controls[request.LayerIndex] = control;
					layers[request.LayerIndex] = layer;
				}
				requests.Clear();
			}

			private bool HasGroup(in MmdDatabase data, int id) {
				for (int index = 0; index < data.Value.Value.Groups.Length; index++) {
					if (data.Value.Value.Groups[index].Id == id) {
						return true;
					}
				}
				return false;
			}
		}
	}
}
