using System;
using ColInt.MMD.Samples.Lifecycle;
using Unity.Mathematics;
using UnityEngine;

namespace ColInt.MMD.Samples.Simulation {
	// Drives fixed simulation ticks. Player and bot input are produced by ECS systems.
	public sealed class MmdSampleSimulationDriver : MonoBehaviour {
		[SerializeField, Min(1)] private int _maximumTicksPerFrame = 15;
		private MmdSampleWorld _activeWorld;
		private float _accumulator;

		public void Init(MmdSampleWorld sample) {
			if (sample == null) {
				throw new ArgumentNullException(nameof(sample));
			}
			Reset();
			_activeWorld = sample;
		}

		public void Reset() {
			_activeWorld = null;
			_accumulator = 0f;
		}

		private void Update() {
			if (_activeWorld == null) {
				return;
			}
			_accumulator = math.min(_accumulator + Time.unscaledDeltaTime, _activeWorld.Timestep * math.max(1, _maximumTicksPerFrame));
			while (_accumulator >= _activeWorld.Timestep) {
				_activeWorld.Tick();
				_accumulator -= _activeWorld.Timestep;
			}
		}

		private void OnDisable() {
			Reset();
		}
	}
}
