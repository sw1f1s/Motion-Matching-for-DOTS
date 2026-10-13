using System;
using ColInt.MMD.Playback.Systems;
using ColInt.MMD.Samples.Benchmark.Spawning;
using ColInt.MMD.Samples.Benchmark.Systems;
using ColInt.MMD.Samples.Camera;
using ColInt.MMD.Samples.Lifecycle;
using ColInt.MMD.Samples.Physics;
using ColInt.MMD.Samples.Simulation;
using Unity.Entities;
using UnityEngine;

namespace ColInt.MMD.Samples.Benchmark {
	public sealed class MmdSampleBenchmarkBootstrap : MonoBehaviour {
		[SerializeField] private MmdSampleWorldPhysicsGeometry _geometry;
		[SerializeField] private MmdSampleBenchmarkSpawner _characters;
		[SerializeField] private MmdSampleSimulationDriver _simulationDriver;
		[SerializeField] private MmdSampleFreeCamera _camera;
		[SerializeField, Min(1f)] private float _simulationRate = 60f;

		public MmdSampleWorld SampleWorld { get; private set; }
		public bool IsRunning => SampleWorld != null;

		private void OnEnable() {
			try {
				if (_geometry == null || _characters == null || _simulationDriver == null || _camera == null ||
					!float.IsFinite(_simulationRate) || _simulationRate < 1f) {
					throw new InvalidOperationException("Assign benchmark geometry, characters, simulation driver and free camera.");
				}
				SampleWorld = new MmdSampleWorld(1f / _simulationRate);
				var world = SampleWorld.World;
				var intents = world.GetOrCreateSystemManaged<MmdSampleBenchmarkIntentGroup>();
				intents.AddSystemToUpdateList(world.GetOrCreateSystem<MmdSampleBenchmarkTargetSystem>());
				var simulation = world.GetExistingSystemManaged<MmdSimulationGroup>();
				simulation.AddSystemToUpdateList(intents);
				simulation.SortSystems();
				_geometry.Init(SampleWorld);
				_characters.Init(SampleWorld);
				_simulationDriver.Init(SampleWorld);
				_camera.Init();
			} catch (Exception exception) {
				Reset();
				Debug.LogException(exception, this);
			}
		}

		private void OnDisable() {
			Reset();
		}

		private void Reset() {
			if (SampleWorld == null) {
				return;
			}
			var sample = SampleWorld;
			try {
				try {
					_camera.Reset();
				} finally {
					try {
						_simulationDriver.Reset();
					} finally {
						try {
							_characters.Reset();
						} finally {
							_geometry.Reset();
						}
					}
				}
			} finally {
				SampleWorld = null;
				sample.Dispose();
			}
		}
	}
}
