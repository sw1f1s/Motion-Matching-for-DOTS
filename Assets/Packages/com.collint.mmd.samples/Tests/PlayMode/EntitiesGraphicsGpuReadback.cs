using System.Linq;
using System.Reflection;
using System;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using UnityEngine;

namespace ColInt.MMD.Tests {
	// Verification only: private buffer access is pinned to Entities Graphics 1.4.3.
	public static class EntitiesGraphicsGpuReadback {
		private const BindingFlags InstanceFields = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

		public static GpuVertexData[] Read(World world, int vertexCount) {
			object pushSystem = null;
			foreach (var system in world.Systems) {
				if (system.GetType().FullName == "Unity.Rendering.PushMeshDataSystem") {
					pushSystem = system;
				}
			}
			if (pushSystem == null) {
				throw new InvalidOperationException("The deformation upload system was not installed.");
			}
			var manager = pushSystem.GetType().GetField("m_MeshBufferManager", InstanceFields).GetValue(pushSystem);
			var wrapper = manager.GetType().GetField("m_DeformedMeshData", InstanceFields).GetValue(manager);
			var activeBuffer = wrapper.GetType().GetProperty("ActiveBuffer", InstanceFields);
			if (activeBuffer != null) {
				wrapper = activeBuffer.GetValue(wrapper);
			}
			var buffer = (ComputeBuffer)wrapper.GetType().GetField("m_Buffer", InstanceFields).GetValue(wrapper);
			Type indexType = typeof(EntitiesGraphicsSystem).Assembly.GetType("Unity.Rendering.DeformedMeshIndex", true);
			uint offset;
			using (var query = world.EntityManager.CreateEntityQuery(ComponentType.ReadOnly<MaterialMeshInfo>(), ComponentType.ReadOnly(indexType))) {
				using (var entities = query.ToEntityArray(Unity.Collections.Allocator.Temp)) {
					if (entities.Length != 1) {
						throw new InvalidOperationException("This fixture requires exactly one enabled GPU mesh.");
					}
					var entity = entities[0];
					var getter = typeof(EntityManager).GetMethods().Single(m => m.Name == "GetComponentData" && m.IsGenericMethodDefinition && m.GetParameters().Length == 1 && m.GetParameters()[0].ParameterType == typeof(Entity));
					object component = getter.MakeGenericMethod(indexType).Invoke(world.EntityManager, new object[] { entity });
					object index = indexType.GetField("Value").GetValue(component);
					if (index is uint value) {
						offset = value;
					} else {
						var values = (uint4)index;
						offset = values[(int)(values.z & 1)];
					}
				}
			}
			if (offset == 0 || buffer.stride != 36 || offset + vertexCount > buffer.count) {
				throw new InvalidOperationException("The GPU mesh allocation is not ready or its vertex layout changed.");
			}
			var result = new GpuVertexData[vertexCount];
			buffer.GetData(result, 0, (int)offset, vertexCount);
			return result;
		}
	}
}
