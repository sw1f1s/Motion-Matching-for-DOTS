using System.Runtime.CompilerServices;
using ColInt.MMD.Database.Data;
using ColInt.MMD.Playback.Components;
using ColInt.MMD.Playback.Helpers;
using ColInt.MMD.Samples.Character.Movement.Shared.Components;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace ColInt.MMD.Samples.Character.Movement.Shared.Helpers {
	public static class MmdSampleFootPose {
		public static bool Support(ref MotionDatabase database, DynamicBuffer<MmdLocalPose> poses, in MmdSampleFootContact contact,
			in LocalTransform transform, float surface) {
			float lowest = math.min(math.min(Height(ref database, poses, database.LeftFootBone, transform), Height(ref database, poses, contact.LeftToe, transform)),
				math.min(Height(ref database, poses, database.RightFootBone, transform), Height(ref database, poses, contact.RightToe, transform)));
			float lift = math.max(0f, surface - lowest);
			if (lift <= 0.000001f) {
				return false;
			}
			float3 offset = math.rotate(math.inverse(transform.Rotation), math.up() * lift) / transform.Scale;
			for (int bone = 0; bone < poses.Length; bone++) {
				if (database.Parents[bone] < 0) {
					var pose = poses[bone].Value;
					pose.Position += offset;
					poses[bone] = new MmdLocalPose { Value = pose };
				}
			}
			return true;
		}

		public static void Record(ref MotionDatabase database, DynamicBuffer<MmdLocalPose> poses,
			DynamicBuffer<MmdInertialPose> history, ref MmdInertializationState smoothing, float deltaTime) {
			// Subsequent blends start from the corrected displayed pose.
			for (int bone = 0; bone < poses.Length; bone++) {
				var entry = history[bone];
				int parent = database.Parents[bone];
				bool modelSpace = parent < 0 || database.Parents[parent] < 0;
				quaternion previous = modelSpace ? entry.DisplayedModelRotation : entry.Previous.Rotation;
				previous = math.mul(math.inverse(MmdAnimationMath.Rotation(entry.AngularVelocity * deltaTime)), previous);
				var pose = poses[bone].Value;
				entry.DisplayedModelRotation = parent < 0 ? pose.Rotation : math.mul(history[parent].DisplayedModelRotation, pose.Rotation);
				quaternion displayed = modelSpace ? entry.DisplayedModelRotation : pose.Rotation;
				entry.AngularVelocity = MmdAnimationMath.RotationVector(math.mul(displayed, math.inverse(previous))) / deltaTime;
				entry.PositionVelocity += (pose.Position - entry.Previous.Position) / deltaTime;
				entry.Previous = pose;
				history[bone] = entry;
			}
			smoothing.PreviousFootCenter = (Model(ref database, poses, database.LeftFootBone).c3.xyz + Model(ref database, poses, database.RightFootBone).c3.xyz) * 0.5f;
		}

		public static float4x4 Model(ref MotionDatabase database, in DynamicBuffer<MmdLocalPose> poses, int bone) {
			float4x4 result = float4x4.identity;
			for (int current = bone; current >= 0; current = database.Parents[current]) {
				var pose = poses[current].Value;
				result = math.mul(float4x4.TRS(pose.Position, pose.Rotation, pose.Scale), result);
			}
			return result;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static float Height(ref MotionDatabase database, in DynamicBuffer<MmdLocalPose> poses, int bone, in LocalTransform transform) {
			return transform.Position.y + math.rotate(transform.Rotation, Model(ref database, poses, bone).c3.xyz * transform.Scale).y;
		}
	}
}
