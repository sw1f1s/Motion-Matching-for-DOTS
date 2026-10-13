using System.Collections.Generic;
using System;
using ColInt.MMD.Samples.Lifecycle;
using ColInt.MMD.Samples.Physics.Helpers;
using Unity.Entities;
using UnityEngine;

namespace ColInt.MMD.Samples.Physics {
	[DisallowMultipleComponent]
	public sealed class MmdSampleWorldPhysicsGeometry : MonoBehaviour {
		public const int CacheVersion = 1;
		[SerializeField, Min(0f)] private float _friction = 0.8f;
		[SerializeField, HideInInspector] private int _cacheVersion;
		[SerializeField, HideInInspector] private string _unityVersion;
		[SerializeField, HideInInspector] private string _sourceFingerprint;
		[SerializeField, HideInInspector] private Matrix4x4 _sourceMatrix;
		[SerializeField, HideInInspector] private float _cachedFriction;
		[SerializeField, HideInInspector] private MmdSampleWorldPhysicsCacheBody[] _bodies = Array.Empty<MmdSampleWorldPhysicsCacheBody>();
		private MmdSampleWorld _sample;
		private Entity[] _entities = Array.Empty<Entity>();

		public float Friction => _friction;
		public string SourceFingerprint => _sourceFingerprint;
		public IReadOnlyList<MmdSampleWorldPhysicsCacheBody> Bodies => _bodies;
		public bool HasCache => _cacheVersion == CacheVersion;
		public bool IsCacheCompatible => HasCache && _unityVersion == Application.unityVersion &&
			_sourceMatrix == transform.localToWorldMatrix && _cachedFriction == _friction;

#if UNITY_EDITOR
		public void SetCache(string fingerprint, MmdSampleWorldPhysicsCacheBody[] bodies) {
			_bodies = bodies;
			_cacheVersion = CacheVersion;
			_unityVersion = Application.unityVersion;
			_sourceMatrix = transform.localToWorldMatrix;
			_cachedFriction = _friction;
			_sourceFingerprint = fingerprint;
		}
#endif

		public void Init(MmdSampleWorld sample) {
			if (sample == null) {
				throw new ArgumentNullException(nameof(sample));
			}
			Reset();
			_sample = sample;
			_entities = MmdSampleWorldPhysicsLoader.Load(sample.World, this);
		}

		public void Reset() {
			if (_sample != null && _sample.World.IsCreated) {
				foreach (Entity entity in _entities) {
					if (_sample.World.EntityManager.Exists(entity)) {
						_sample.World.EntityManager.DestroyEntity(entity);
					}
				}
			}
			_entities = Array.Empty<Entity>();
			_sample = null;
		}

		private void OnDisable() {
			Reset();
		}
	}
}
