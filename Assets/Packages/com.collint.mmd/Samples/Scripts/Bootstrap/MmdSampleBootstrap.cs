using System.Collections.Generic;
using System;
using ColInt.MMD.Samples.Camera;
using ColInt.MMD.Samples.Character.Spawning;
using ColInt.MMD.Samples.Lifecycle;
using ColInt.MMD.Samples.Physics;
using ColInt.MMD.Samples.Simulation;
using UnityEngine;

namespace ColInt.MMD.Samples.Bootstrap {
	public sealed class MmdSampleBootstrap : MonoBehaviour {
		[SerializeField] private MmdSampleWorldPhysicsGeometry[] _worldGeometry = Array.Empty<MmdSampleWorldPhysicsGeometry>();
		[SerializeField] private MmdSampleCharacterSpawner[] _characters = Array.Empty<MmdSampleCharacterSpawner>();
		[SerializeField] private MmdSampleSimulationDriver _simulationDriver;
		[SerializeField] private MmdSampleThirdPersonCamera _playerCamera;

		public MmdSampleWorld SampleWorld { get; private set; }
		public bool IsRunning => SampleWorld != null;

		private void OnEnable() {
			try {
				ValidateReferences(_worldGeometry);
				ValidateReferences(_characters);
				SampleWorld = new MmdSampleWorld(1f / 60f);
				foreach (var geometry in _worldGeometry) {
					if (ShouldInitialize(geometry)) {
						geometry.Init(SampleWorld);
					}
				}
				MmdSampleCharacterSpawner cameraPlayer = null;
				foreach (var character in _characters) {
					if (ShouldInitialize(character)) {
						character.Init(SampleWorld);
						if (cameraPlayer == null && character.IsPlayerControlled) {
							cameraPlayer = character;
						}
					}
				}
				if (_playerCamera != null && ShouldInitialize(_playerCamera) && cameraPlayer != null) {
					_playerCamera.Init(SampleWorld, cameraPlayer);
				}
				if (_simulationDriver != null && ShouldInitialize(_simulationDriver)) {
					_simulationDriver.Init(SampleWorld, cameraPlayer != null ? _playerCamera : null);
				}
			} catch (Exception exception) {
				ResetSample();
				Debug.LogException(exception, this);
			}
		}

		private void OnDisable() {
			ResetSample();
		}

		private void ResetSample() {
			if (SampleWorld == null) {
				return;
			}
			MmdSampleWorld sample = SampleWorld;
			try {
				ResetCameraAndDriver();
			} finally {
				try {
					foreach (var character in _characters) {
						if (character != null) {
							character.Reset();
						}
					}
				} finally {
					try {
						foreach (var geometry in _worldGeometry) {
							if (geometry != null) {
								geometry.Reset();
							}
						}
					} finally {
						SampleWorld = null;
						sample.Dispose();
					}
				}
			}
		}

		private void ResetCameraAndDriver() {
			try {
				if (_playerCamera != null) {
					_playerCamera.Reset();
				}
			} finally {
				if (_simulationDriver != null) {
					_simulationDriver.Reset();
				}
			}
		}

		private static bool ShouldInitialize(MonoBehaviour participant) {
			if (!participant.enabled) {
				return false;
			}
			// During a parent's OnEnable, Unity may not have activated child behaviours yet.
			// Read the configured hierarchy so initialization never depends on callback order.
			for (Transform ancestor = participant.transform; ancestor != null; ancestor = ancestor.parent) {
				if (!ancestor.gameObject.activeSelf) {
					return false;
				}
			}
			return true;
		}

		private static void ValidateReferences<T>(T[] references) where T : MonoBehaviour {
			if (references == null) {
				throw new InvalidOperationException("Sample bootstrap references must be an initialized array.");
			}
			var unique = new HashSet<T>();
			foreach (T reference in references) {
				if (reference == null || !unique.Add(reference)) {
					throw new InvalidOperationException("Sample bootstrap references must be assigned and unique.");
				}
			}
		}
	}
}
