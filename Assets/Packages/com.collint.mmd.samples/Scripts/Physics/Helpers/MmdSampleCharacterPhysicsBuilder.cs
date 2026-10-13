using System;
using ColInt.MMD.Actions.Components;
using ColInt.MMD.Physics.Components;
using ColInt.MMD.RootMotion.Components;
using ColInt.MMD.Samples.Physics.Components;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;

namespace ColInt.MMD.Samples.Physics.Helpers {
	public static class MmdSampleCharacterPhysicsBuilder {
		public static void Add(EntityManager manager, Entity character) {
			Add(manager, character, 0f);
		}

		public static void Add(EntityManager manager, Entity character, float crouchHeight) {
			if (manager.HasComponent<PhysicsCollider>(character) || manager.HasComponent<MmdKinematicRoot>(character)) {
				throw new InvalidOperationException("The sample capsule requires a character with no existing physics body or kinematic root owner.");
			}
			var collider = MmdSampleWorldPhysicsColliderFactory.CreateCapsule(new float3(0f, 0.9f, 0f), 0.3f, 1.8f, 0f);
			BlobAssetReference<Collider> crouched = default;
			BlobAssetReference<Collider> traversal = default;
			bool owned = false;
			try {
				if (crouchHeight > 0f) {
					if (crouchHeight < 0.6f || crouchHeight >= 1.8f) {
						throw new ArgumentOutOfRangeException(nameof(crouchHeight), "Crouch capsule height must be at least 0.6 and below 1.8 metres.");
					}
					crouched = MmdSampleWorldPhysicsColliderFactory.CreateCapsule(new float3(0f, crouchHeight * 0.5f, 0f), 0.3f, crouchHeight, 0f);
				}
				traversal = MmdSampleWorldPhysicsColliderFactory.CreateCapsule(new float3(0f, 1.2f, 0f), 0.3f, 1.2f, 0f);
				manager.AddComponentData(character, new MmdSamplePhysicsColliderOwner(collider, crouched, traversal));
				owned = true;
				manager.AddComponentData(character, new PhysicsCollider { Value = collider });
				var mass = PhysicsMass.CreateDynamic(collider.Value.MassProperties, 70f);
				// Prevent contact torques from tipping the capsule; root motion still supplies yaw velocity.
				mass.InverseInertia = float3.zero;
				manager.AddComponentData(character, mass);
				manager.AddComponent<PhysicsVelocity>(character);
				manager.AddSharedComponent(character, new PhysicsWorldIndex());
				manager.AddComponentData(character, new LocalToWorld { Value = manager.GetComponentData<LocalTransform>(character).ToMatrix() });
				manager.AddComponent<MmdPhysicsRoot>(character);
				manager.AddComponentData(character, MmdGroundMovement.Default);
				if (!manager.HasComponent<MmdActionState>(character)) {
					manager.AddComponent<MmdActionState>(character);
				}
				if (!manager.HasComponent<MmdRootMotion>(character)) {
					manager.AddComponent<MmdRootMotion>(character);
				}
			} catch {
				if (!owned) {
					if (traversal.IsCreated) {
						traversal.Dispose();
					}
					collider.Dispose();
					if (crouched.IsCreated) {
						crouched.Dispose();
					}
				}
				throw;
			}
		}
	}
}
