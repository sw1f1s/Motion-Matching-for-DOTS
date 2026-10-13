using ColInt.MMD.MotionMatching.Helpers;
using ColInt.MMD.MotionMatching.Data;
using ColInt.MMD.Actions.Components;
using ColInt.MMD.Database.Components;
using ColInt.MMD.Database.Data;
using ColInt.MMD.Lifecycle.Components;
using ColInt.MMD.MotionMatching.Components;
using ColInt.MMD.Playback.Components;
using ColInt.MMD.Samples.Character.Components;
using ColInt.MMD.Samples.Character.Movement.CrouchState.Components;
using ColInt.MMD.Samples.Character.Movement.Shared.Components;
using ColInt.MMD.Samples.Character.Movement.Shared.Helpers;
using ColInt.MMD.Samples.Character.Movement.Shared.Systems;
using ColInt.MMD.Samples.Character.Movement.SlideState.Components;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;

namespace ColInt.MMD.Samples.Character.Movement.SlideState.Systems {
	[BurstCompile]
	[DisableAutoCreation]
	[UpdateInGroup(typeof(MmdSampleStateStartGroup))]
	public partial struct MmdSampleSlideExitStartSystem : ISystem {
		public void OnCreate(ref SystemState state) {
			state.RequireForUpdate<MmdWorldState>();
		}

		[BurstCompile]
		public void OnUpdate(ref SystemState state) {
			if (SystemAPI.GetSingleton<MmdWorldState>().IsPaused) {
				return;
			}
			state.Dependency = new UpdateJob().ScheduleParallel(state.Dependency);
		}

		[BurstCompile]
		[WithAll(typeof(MmdSampleStateStarted<MmdSampleSlideExit>))]
		private partial struct UpdateJob : IJobEntity {

			private void Execute(
				in MmdSampleSlide slide,
				in MmdSampleCrouch crouch,
				in MmdDatabase data,
				ref MmdSampleCharacterControl control,
				ref MmdSampleSlideProgress progress,
				ref MmdSampleMovement movement,
				ref MmdActionRequest request,
				ref MmdSearchSettings settings,
				ref MmdPlayback playback, in DynamicBuffer<MmdSearchQueryElement> query, DynamicBuffer<MmdMotionGroup> groups) {
				progress.Velocity = float3.zero;
				movement = new MmdSampleMovement { CrouchCollider = true };
				control.WalkSpeed = crouch.Speed;
				control.RunSpeed = crouch.Speed;
				control.SprintSpeed = crouch.Speed;
				control.IdleGroup = crouch.IdleGroup;
				control.WalkGroup = crouch.MoveGroup;
				control.RunGroup = crouch.MoveGroup;
				control.SprintGroup = crouch.MoveGroup;
				control.TurnGroup = crouch.TurnGroup;
				control.StartGroup = crouch.StartGroup;
				MmdSampleAnimationRequest.Cancel(ref request);
				MmdMotionGroups.Set(groups, MmdMotionGroupUsage.Required, slide.MotionGroup);
				MmdMotionGroups.Set(groups, MmdMotionGroupUsage.Excluded, 0);
				settings.PreferLoop = false;
				int exit = SelectExit(ref data.Value.Value, slide.MotionGroup, query);
				playback.ClipIndex = exit;
				playback.Time = 0f;
				playback.Speed = 1f;
				playback.MatchedFrame = data.Value.Value.Clips[exit].FirstFrame;
				playback.Revision++;
			}

			private static int SelectExit(ref MotionDatabase database, int group, in DynamicBuffer<MmdSearchQueryElement> query) {
				float best = float.PositiveInfinity;
				int selected = -1;
				for (int clip = 0; clip < database.Clips.Length; clip++) {
					ref var candidate = ref database.Clips[clip];
					if (candidate.Loop || candidate.ActionId != 0 || !ColInt.MMD.Database.Helpers.MotionDatabaseGroups.Contains(ref database, candidate, group)) {
						continue;
					}
					float cost = 0f;
					int offset = candidate.FirstFrame * database.FeatureCount;
					for (int feature = 15; feature < database.FeatureCount; feature++) {
						float difference = (query[feature].Value - database.Features[offset + feature]) * database.FeatureScales[feature];
						cost += difference * difference;
					}
					if (cost < best) {
						best = cost;
						selected = clip;
					}
				}
				return selected;
			}
		}
	}
}
