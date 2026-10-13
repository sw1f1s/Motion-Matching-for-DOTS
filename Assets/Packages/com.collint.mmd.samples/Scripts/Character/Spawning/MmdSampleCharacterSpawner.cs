using System;
using ColInt.MMD.Samples.Character.Configurations;
using ColInt.MMD.Samples.Lifecycle;
using Unity.Entities;
using Unity.Transforms;
using UnityEngine;

namespace ColInt.MMD.Samples.Character.Spawning {
	public sealed class MmdSampleCharacterSpawner : MonoBehaviour {
		[SerializeField] private MmdSampleCharacterConfiguration _configuration;
		[SerializeField] private bool _playerControlled = true;
		[SerializeField] private bool _drawTrajectory = true;
		private MmdSampleWorld _sample;

		public Entity CharacterEntity { get; private set; }
		public bool IsPlayerControlled => _playerControlled;

		public void Init(MmdSampleWorld sample) {
			if (sample == null) {
				throw new ArgumentNullException(nameof(sample));
			}
			Reset();
			if (_configuration == null) {
				throw new InvalidOperationException("A character configuration is required.");
			}
			_sample = sample;
			var spawnTransform = LocalTransform.FromPositionRotation(transform.position, transform.rotation);
			CharacterEntity = sample.Characters.Create(_configuration, spawnTransform, _playerControlled, _drawTrajectory);
		}

		public void Reset() {
			if (_sample != null && _sample.World.IsCreated && _sample.World.EntityManager.Exists(CharacterEntity)) {
				_sample.World.EntityManager.DestroyEntity(CharacterEntity);
			}
			CharacterEntity = Entity.Null;
			_sample = null;
		}

		private void OnDisable() {
			Reset();
		}
	}
}
