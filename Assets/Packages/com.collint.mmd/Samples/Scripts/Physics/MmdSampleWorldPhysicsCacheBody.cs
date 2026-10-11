using System.IO.Compression;
using System.IO;
using System.Runtime.InteropServices;
using System;
using ColInt.MMD.Samples.Physics.Helpers;
using Unity.Entities.Serialization;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace ColInt.MMD.Samples.Physics {
	[Serializable]
	public sealed class MmdSampleWorldPhysicsCacheBody {
		[SerializeField] private string _name;
		[SerializeField] private Vector3 _position;
		[SerializeField] private Quaternion _rotation;
		[SerializeField] private string _blob;

		public unsafe MmdSampleWorldPhysicsBody Load() {
			using (var input = new MemoryStream(Convert.FromBase64String(_blob), false)) {
				using (var gzip = new GZipStream(input, CompressionMode.Decompress)) {
					using (var output = new MemoryStream()) {
						gzip.CopyTo(output);
						byte[] bytes = output.GetBuffer();
						fixed (byte* data = bytes) {
							using (var reader = new MemoryBinaryReader(data, output.Length)) {
								var collider = reader.Read<Unity.Physics.Collider>();
								return new MmdSampleWorldPhysicsBody(_name, new RigidTransform(_rotation, _position), collider);
							}
						}
					}
				}
			}
		}

		// Takes ownership of the source collider, even if packing fails.
		public static unsafe MmdSampleWorldPhysicsCacheBody Pack(in MmdSampleWorldPhysicsBody body) {
			var collider = body.Collider;
			try {
				using (var writer = new MemoryBinaryWriter()) {
					writer.Write(collider);
					var bytes = new byte[writer.Length];
					Marshal.Copy((IntPtr)writer.Data, bytes, 0, bytes.Length);
					using (var output = new MemoryStream()) {
						using (var gzip = new GZipStream(output, System.IO.Compression.CompressionLevel.Fastest, true)) {
							gzip.Write(bytes, 0, bytes.Length);
						}
						return new MmdSampleWorldPhysicsCacheBody {
							_name = body.Name,
							_position = body.Transform.pos,
							_rotation = body.Transform.rot,
							_blob = Convert.ToBase64String(output.GetBuffer(), 0, checked((int)output.Length))
						};
					}
				}
			} finally {
				collider.Dispose();
			}
		}
	}
}
