using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System;
using ColInt.MMD.Graphics.Helpers;
using ColInt.MMD.Lifecycle.Components;
using ColInt.MMD.Lifecycle.Helpers;
using ColInt.MMD.Physics.Helpers;
using ColInt.MMD.Playback.Systems;
using ColInt.MMD.Samples.Character.Helpers;
using ColInt.MMD.Samples.Character.Input.Components;
using ColInt.MMD.Samples.Character.Movement.Shared.Helpers;
using ColInt.MMD.Samples.Physics.Systems;
using Unity.Core;
using Unity.Entities;
using Unity.Physics;

namespace ColInt.MMD.Samples.Lifecycle {
	// Sample world owner. Host projects provide their own world lifecycle.
	public sealed class MmdSampleWorld : IDisposable {
		private readonly FixedStepSimulationSystemGroup _tick;
		private readonly Entity _stateEntity;
		private double _elapsed;
		private bool _disposed;

		public World World { get; }
		public float Timestep { get; }
		public MmdSampleCharacterFactory Characters { get; }

		public MmdSampleWorld(float timestep) {
			if (!(timestep > 0f) || float.IsInfinity(timestep)) {
				throw new ArgumentException("A finite positive timestep is required.", nameof(timestep));
			}
			Timestep = timestep;
			World = new World("Motion Matching for DOTS Sample", WorldFlags.Game);
			Characters = new MmdSampleCharacterFactory(World);
			try {
				var systems = new List<Type>();
				foreach (Type type in DefaultWorldInitialization.GetAllSystems(WorldSystemFilterFlags.Default)) {
					string name = type.Namespace;
					if (name != null && (name.StartsWith("Unity.Entities", StringComparison.Ordinal) ||
						name.StartsWith("Unity.Transforms", StringComparison.Ordinal) || name.StartsWith("Unity.Scenes", StringComparison.Ordinal) ||
						name.StartsWith("Unity.Rendering", StringComparison.Ordinal) || name.StartsWith("Unity.Physics", StringComparison.Ordinal))) {
						systems.Add(type);
					}
				}
				DefaultWorldInitialization.AddSystemsToRootLevelSystemGroups(World, systems);
				_stateEntity = World.EntityManager.CreateEntity(typeof(MmdWorldState), typeof(MmdSampleInput), typeof(PhysicsStep));
				World.EntityManager.SetComponentData(_stateEntity, PhysicsStep.Default);
				var fixedStep = World.GetExistingSystemManaged<FixedStepSimulationSystemGroup>();
				World.GetExistingSystemManaged<SimulationSystemGroup>().RemoveSystemFromUpdateList(fixedStep);
				fixedStep.RateManager = null;
				_tick = fixedStep;
				MmdSystemInstaller.Install(World, fixedStep);
				MmdSystemInstaller.InstallInitialization(World, World.GetExistingSystemManaged<InitializationSystemGroup>());
				MmdPhysicsInstaller.Install(World, fixedStep);
				var cleanup = World.GetOrCreateSystemManaged<MmdSamplePhysicsCleanupGroup>();
				cleanup.AddSystemToUpdateList(World.GetOrCreateSystem<MmdSamplePhysicsColliderCleanupSystem>());
				cleanup.AddSystemToUpdateList(World.GetOrCreateSystem<MmdSamplePhysicsColliderDisposalSystem>());
				World.GetExistingSystemManaged<InitializationSystemGroup>().AddSystemToUpdateList(cleanup);
				MmdGraphicsInstaller.Install(World, World.GetExistingSystemManaged<PresentationSystemGroup>());
				var group = World.GetExistingSystemManaged<MmdSimulationGroup>();
				MmdSampleMovementInstaller.Install(World, fixedStep);
				group.SortSystems();
				ScriptBehaviourUpdateOrder.AppendWorldToCurrentPlayerLoop(World);
			} catch {
				ScriptBehaviourUpdateOrder.RemoveWorldFromCurrentPlayerLoop(World);
				World.Dispose();
				Characters.Dispose();
				throw;
			}
		}

		public void SetPaused(bool paused) {
			ThrowIfDisposed();
			World.EntityManager.SetComponentData(_stateEntity, new MmdWorldState { IsPaused = paused });
			if (paused) {
				var input = World.EntityManager.GetComponentData<MmdSampleInput>(_stateEntity);
				input.JumpPressed = false;
				input.CrouchPressed = false;
				World.EntityManager.SetComponentData(_stateEntity, input);
			}
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void SetInput(in MmdSampleInput input) {
			ThrowIfDisposed();
			World.EntityManager.SetComponentData(_stateEntity, input);
		}

		public void Tick() {
			ThrowIfDisposed();
			if (World.EntityManager.GetComponentData<MmdWorldState>(_stateEntity).IsPaused) {
				return;
			}
			_elapsed += Timestep;
			World.PushTime(new TimeData(_elapsed, Timestep));
			try {
				_tick.Update();
				World.EntityManager.CompleteAllTrackedJobs();
			} finally {
				World.PopTime();
			}
		}

		public void Dispose() {
			if (_disposed) {
				return;
			}
			_disposed = true;
			if (World.IsCreated) {
				ScriptBehaviourUpdateOrder.RemoveWorldFromCurrentPlayerLoop(World);
				World.EntityManager.CompleteAllTrackedJobs();
				World.Dispose();
			}
			Characters.Dispose();
		}

		private void ThrowIfDisposed() {
			if (_disposed) {
				throw new ObjectDisposedException(nameof(MmdSampleWorld));
			}
		}

	}
}
