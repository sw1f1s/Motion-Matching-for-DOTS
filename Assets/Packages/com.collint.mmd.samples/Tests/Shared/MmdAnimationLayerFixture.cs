using System.Collections.Generic;
using ColInt.MMD.Database.Components;
using ColInt.MMD.Playback.Helpers;
using Unity.Entities;
using System;
using System.Linq;
using ColInt.MMD.Database.Data;
using ColInt.MMD.Database.Helpers;
using ColInt.MMD.Playback.Authoring;
using ColInt.MMD.Playback.Data;
using Unity.Mathematics;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ColInt.MMD.Tests {
	public sealed class MmdAnimationLayerFixture : IDisposable {
		private readonly GameObject _rig;
		private readonly List<MmdAnimationLayerSet> _definitions = new List<MmdAnimationLayerSet>();

		public MmdAnimationMask Mask { get; }
		public MotionDatabaseSource Source { get; }

		public MmdAnimationLayerFixture() {
			_rig = new GameObject("Layer fixture rig");
			var root = new GameObject("root");
			root.transform.SetParent(_rig.transform);
			var arm = new GameObject("arm");
			arm.transform.SetParent(root.transform);
			Mask = ScriptableObject.CreateInstance<MmdAnimationMask>();
			Mask.Configure(_rig, new[] {
				new MmdAnimationBoneWeight { Path = "root", Weight = 0f },
				new MmdAnimationBoneWeight { Path = "root/arm", Weight = 1f }
			});
			Source = MotionDatabaseFixture.Create();
			Source.BoneNameHashes = new[] { BonePathHash.Calculate("root"), BonePathHash.Calculate("root/arm") };
			for (int frame = 0; frame < Source.Frames.Length; frame++) {
				int clip = Source.Frames[frame].ClipIndex;
				var pose = Source.Poses[frame * 2 + 1];
				pose.Position.x = clip * 3f;
				pose.Rotation = quaternion.RotateZ(clip * 0.4f);
				pose.Scale = new float3(1f + clip * 0.2f);
				Source.Poses[frame * 2 + 1] = pose;
			}
		}

		public MmdAnimationLayerSettings Settings(string group, float weight, float fade, float speed, float time) {
			return new MmdAnimationLayerSettings {
				Name = group, Mask = Mask, Group = group, Weight = weight, FadeDuration = fade,
				ActiveGroups = Source.Groups.Where(value => value.Mode != MotionPlaybackMode.LayerOnly).Select(value => value.Name.ToString()).ToArray(),
				Speed = speed, Time = time, Channels = MotionPoseChannels.All
			};
		}

		public void Add(EntityManager manager, Entity actor, MmdAnimationLayerSettings[] settings) {
			var definition = MmdAnimationLayerBuilder.Build(manager.GetComponentData<MmdDatabase>(actor).Value, settings);
			_definitions.Add(definition);
			MmdAnimationLayers.Add(manager, actor, definition);
		}

		public void Dispose() {
			foreach (var definition in _definitions) {
				var owned = definition;
				owned.Dispose();
			}
			Object.DestroyImmediate(Mask);
			Object.DestroyImmediate(_rig);
		}
	}
}
