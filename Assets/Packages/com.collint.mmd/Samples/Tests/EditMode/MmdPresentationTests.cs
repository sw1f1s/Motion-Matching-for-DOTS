using ColInt.MMD.Database.Components;
using ColInt.MMD.Database.Helpers;
using ColInt.MMD.Graphics.Components;
using ColInt.MMD.Graphics.Systems;
using ColInt.MMD.Playback.Components;
using ColInt.MMD.Presentation.Components;
using ColInt.MMD.Presentation.Systems;
using ColInt.MMD.Samples.Lifecycle;
using NUnit.Framework;
using Unity.Deformations;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace ColInt.MMD.Tests {
	public class MmdPresentationTests {
		[Test]
		public void ModelAndSkinMatricesReadSameEntityWithoutModifyingAnimation() {
			using (var sample = new MmdSampleWorld(0.1f)) {
				Entity character = sample.Characters.Create(MotionDatabaseBuilder.Build(MotionDatabaseFixture.Create()), true, LocalTransform.Identity);
				sample.Tick();
				var manager = sample.World.EntityManager;
				MmdPlayback before = manager.GetComponentData<MmdPlayback>(character);
				manager.AddBuffer<MmdModelMatrix>(character);
				var skeleton = manager.AddBuffer<MmdSkeletonBone>(character);
				var data = manager.GetComponentData<MmdDatabase>(character).Value;
				ref var database = ref data.Value;
				for (int i = 0; i < database.Parents.Length; i++) {
					skeleton.Add(new MmdSkeletonBone { ParentIndex = database.Parents[i], PathHash = database.BoneNameHashes[i] });
				}
				var modelSystem = sample.World.GetExistingSystem<MmdModelMatrixSystem>();
				modelSystem.Update(sample.World.Unmanaged);
				manager.CompleteAllTrackedJobs();
				Entity mesh = manager.CreateEntity(typeof(MmdSkinSource));
				manager.SetComponentData(mesh, new MmdSkinSource { PoseEntity = character, ModelToMesh = float4x4.identity });
				var bindings = manager.AddBuffer<MmdSkinBinding>(mesh);
				bindings.Add(new MmdSkinBinding { BoneIndex = 1, BindPose = float4x4.Translate(new float3(0f, -1f, 0f)) });
				manager.AddBuffer<SkinMatrix>(mesh);
				var skinSystem = sample.World.GetExistingSystem<MmdSkinMatrixSystem>();
				skinSystem.Update(sample.World.Unmanaged);
				manager.CompleteAllTrackedJobs();
				Assert.That(manager.GetBuffer<SkinMatrix>(mesh)[0].Value.c3.y, Is.EqualTo(0.1f).Within(0.00001f));
				Assert.That(manager.GetComponentData<MmdPlayback>(character).Time, Is.EqualTo(before.Time));
				Assert.That(manager.GetBuffer<MmdLocalPose>(character)[1].Value.Position.y, Is.EqualTo(1.1f).Within(0.00001f));
			}
		}
	}
}
