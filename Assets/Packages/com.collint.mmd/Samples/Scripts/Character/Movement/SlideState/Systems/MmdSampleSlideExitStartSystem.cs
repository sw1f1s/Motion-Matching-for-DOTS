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
				ref MmdPlayback playback, in DynamicBuffer<MmdSearchQueryElement> query) {
				progress.Velocity = float3.zero;
				movement = new MmdSampleMovement { CrouchCollider = true };
				control.WalkSpeed = crouch.Speed;
				control.RunSpeed = crouch.Speed;
				control.SprintSpeed = crouch.Speed;
				control.IdleTags = crouch.IdleTags;
				control.WalkTags = crouch.MoveTags;
				control.RunTags = crouch.MoveTags;
				control.SprintTags = crouch.MoveTags;
				control.TurnTags = crouch.TurnTags;
				control.StartTags = crouch.StartTags;
				MmdSampleAnimationRequest.Cancel(ref request);
				settings.RequiredTags = slide.MotionTags;
				settings.ExcludedTags = 0;
				settings.PreferLoop = false;
				int exit = SelectExit(ref data.Value.Value, slide.MotionTags, query);
				playback.ClipIndex = exit;
				playback.Time = 0f;
				playback.Speed = 1f;
				playback.MatchedFrame = data.Value.Value.Clips[exit].FirstFrame;
				playback.Revision++;
			}

			private static int SelectExit(ref MotionDatabase database, uint tags, in DynamicBuffer<MmdSearchQueryElement> query) {
				float best = float.PositiveInfinity;
				int selected = -1;
				for (int clip = 0; clip < database.Clips.Length; clip++) {
					ref var candidate = ref database.Clips[clip];
					if (candidate.Loop || candidate.ActionId != 0 || (candidate.Tags & tags) == 0) {
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
