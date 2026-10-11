using System;
using System.Collections.Generic;
using ColInt.MMD.Samples.Benchmark.Components;
using ColInt.MMD.Samples.Benchmark.Helpers;
using ColInt.MMD.Samples.Character.Configurations;
using ColInt.MMD.Samples.Lifecycle;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace ColInt.MMD.Samples.Benchmark.Spawning {
	[DisallowMultipleComponent]
	public sealed class MmdSampleBenchmarkSpawner : MonoBehaviour {
		[SerializeField] private MmdSampleCharacterConfiguration _configuration;
		[SerializeField, Min(1)] private int _characterCount = 500;
		[SerializeField] private int _seed = 12345;
		[Tooltip("World X/Z area centred on this transform. Keep it inside the floor collider.")]
		[SerializeField] private Vector2 _areaSize = new Vector2(90f, 90f);
		[SerializeField, Min(0.1f)] private float _arrivalRadius = 2f;
		private readonly List<Entity> _characters = new List<Entity>();
		private MmdSampleWorld _sample;
		private Entity _areaEntity;

		public int CharacterCount => _characterCount;
		public int Seed => _seed;
		public int SpawnedCount => _characters.Count;

		public void Init(MmdSampleWorld sample) {
			if (sample == null) {
				throw new ArgumentNullException(nameof(sample));
			}
			Reset();
			if (_configuration == null || _configuration.Assets.Database == null || !_configuration.Assets.RenderPrefab.IsReferenceValid) {
				throw new InvalidOperationException("Benchmark needs a character configuration with a database and render prefab.");
			}
			if (_characterCount < 1 || !math.all(math.isfinite((float2)_areaSize)) || !math.isfinite(_arrivalRadius) ||
				_arrivalRadius <= 0f || math.cmin((float2)_areaSize) <= _arrivalRadius * 6f) {
				throw new InvalidOperationException("Benchmark needs a positive count and an area wider than six arrival radii.");
			}
			_sample = sample;
			var manager = sample.World.EntityManager;
			var area = new MmdSampleBenchmarkArea { Center = transform.position, HalfSize = (float2)_areaSize * 0.5f, ArrivalRadius = _arrivalRadius };
			try {
				_areaEntity = manager.CreateEntity(typeof(MmdSampleBenchmarkArea));
				manager.SetComponentData(_areaEntity, area);
				for (int character = 0; character < _characterCount; character++) {
					var random = MmdSampleBenchmarkRandom.CreateStream(_seed, character);
					float3 position = MmdSampleBenchmarkRandom.Position(ref random, area);
					quaternion rotation = quaternion.RotateY(random.NextFloat(0f, math.PI * 2f));
					Entity entity = sample.Characters.CreateAutonomous(_configuration, LocalTransform.FromPositionRotation(position, rotation));
					_characters.Add(entity);
					manager.AddComponentData(entity, new MmdSampleBenchmarkAgent {
						Target = MmdSampleBenchmarkRandom.Target(ref random, area, position), Random = random
					});
				}
			} catch {
				Reset();
				throw;
			}
		}

		public void Reset() {
			if (_sample != null && _sample.World.IsCreated) {
				var manager = _sample.World.EntityManager;
				foreach (Entity character in _characters) {
					if (manager.Exists(character)) {
						manager.DestroyEntity(character);
					}
				}
				if (manager.Exists(_areaEntity)) {
					manager.DestroyEntity(_areaEntity);
				}
			}
			_characters.Clear();
			_areaEntity = Entity.Null;
			_sample = null;
		}

		private void OnDisable() {
			Reset();
		}
	}
}
