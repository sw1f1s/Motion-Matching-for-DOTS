using System.Runtime.CompilerServices;
using ColInt.MMD.RootMotion.Components;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;

namespace ColInt.MMD.Physics.Components {
	public static class MmdPhysicsRootVelocity {
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void Apply(in LocalTransform transform, in PhysicsMass mass, ref PhysicsVelocity velocity, ref MmdRootMotion motion) {
			if (!motion.Valid || motion.DeltaTime <= 0f) {
				velocity.Linear.x = 0f;
				velocity.Linear.z = 0f;
				velocity.Angular = float3.zero;
				motion.Valid = false;
				return;
			}
			float3 desired = math.rotate(transform.Rotation, motion.Translation) * transform.Scale / motion.DeltaTime;
			velocity.Linear.x = desired.x;
			velocity.Linear.z = desired.z;
			quaternion rotation = motion.Rotation;
			if (rotation.value.w < 0f) {
				rotation.value = -rotation.value;
			}
			float angle = 2f * math.acos(math.clamp(rotation.value.w, -1f, 1f));
			float3 localAxis = math.normalizesafe(rotation.value.xyz);
			float3 worldAngular = math.rotate(transform.Rotation, localAxis) * angle / motion.DeltaTime;
			quaternion inertiaRotation = math.mul(transform.Rotation, mass.Transform.rot);
			velocity.Angular = math.rotate(math.inverse(inertiaRotation), worldAngular);
			motion.Valid = false;
		}
	}
}
