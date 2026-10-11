using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace ColInt.MMD.Samples.Environment.Atmosphere {
	internal sealed class MmdSampleAtmospherePass : ScriptableRenderPass {
		private static readonly int VolumeTexture = Shader.PropertyToID("_VAVolumeTexture");
		private static readonly int VolumeSize = Shader.PropertyToID("_VAVolumeSize");
		private static readonly int CloudShadowTexture = Shader.PropertyToID("_VACloudShadowTexture");
		private static readonly int CloudShadowRegion = Shader.PropertyToID("_VACloudShadowRegion");
		public Material Material { get; set; }
		public int PixelHeight { get; set; }

		public MmdSampleAtmospherePass() {
			renderPassEvent = RenderPassEvent.BeforeRenderingTransparents;
			ConfigureInput(ScriptableRenderPassInput.Depth);
			requiresIntermediateTexture = true;
		}

		public override void RecordRenderGraph(RenderGraph graph, ContextContainer frameData) {
			var resources = frameData.Get<UniversalResourceData>();
			if (resources.isActiveTargetBackBuffer || !resources.cameraDepthTexture.IsValid()) {
				return;
			}
			var source = resources.activeColorTexture;
			var full = graph.GetTextureDesc(source);
			var atmosphere = MmdSampleAtmosphere.Active;
			var settings = atmosphere.Settings;
			var camera = frameData.Get<UniversalCameraData>().camera;
			var light = atmosphere.Sun != null ? atmosphere.Sun : RenderSettings.sun;
			Vector3 sun = light != null ? -light.transform.forward : Vector3.up;
			Vector3 cameraPosition = camera.transform.position;
			Vector2 center = new Vector2(cameraPosition.x, cameraPosition.z)
				+ new Vector2(sun.x, sun.z) * ((settings.CloudBase - cameraPosition.y) / Mathf.Max(sun.y, .05f));
			float span = Mathf.Clamp(settings.FogDistance * (1 + new Vector2(sun.x, sun.z).magnitude / Mathf.Max(sun.y, .05f)) * 2.2f, 4096, 16384);
			float texelSize = span / 256;
			center.x = Mathf.Floor(center.x / texelSize) * texelSize;
			center.y = Mathf.Floor(center.y / texelSize) * texelSize;
			var cloudRegion = new Vector4(center.x, center.y, 1 / span, 1);
			var cloudDesc = full;
			cloudDesc.name = "MMD Sample Cloud Sunlight Transmittance";
			cloudDesc.width = 256;
			cloudDesc.height = 256;
			cloudDesc.colorFormat = GraphicsFormat.R16_SFloat;
			cloudDesc.msaaSamples = MSAASamples.None;
			cloudDesc.depthBufferBits = DepthBits.None;
			cloudDesc.filterMode = FilterMode.Bilinear;
			cloudDesc.clearBuffer = false;
			var cloudShadow = graph.CreateTexture(cloudDesc);
			using (var builder = graph.AddRasterRenderPass<MmdSampleAtmospherePassData>("MMD Sample: cloud sunlight", out var data)) {
				data.Material = Material;
				data.Size = cloudRegion;
				builder.SetRenderAttachment(cloudShadow, 0);
				builder.SetGlobalTextureAfterPass(cloudShadow, CloudShadowTexture);
				builder.AllowGlobalStateModification(true);
				builder.SetRenderFunc(static (MmdSampleAtmospherePassData p, RasterGraphContext context) => {
					context.cmd.SetGlobalVector(CloudShadowRegion, p.Size);
					context.cmd.SetGlobalVector(Shader.PropertyToID("_BlitScaleBias"), new Vector4(1, 1, 0, 0));
					context.cmd.DrawProcedural(Matrix4x4.identity, p.Material, 2, MeshTopology.Triangles, 3);
				});
			}
			var low = full;
			low.name = "MMD Sample Atmosphere Volume";
			low.height = Mathf.Min(full.height, PixelHeight);
			low.width = Mathf.Max(1, Mathf.RoundToInt((float)full.width / full.height * low.height));
			low.colorFormat = GraphicsFormat.R16G16B16A16_SFloat;
			low.msaaSamples = MSAASamples.None;
			low.depthBufferBits = DepthBits.None;
			low.filterMode = FilterMode.Point;
			low.clearBuffer = false;
			var volume = graph.CreateTexture(low);
			var size = new Vector4(low.width, low.height, 1f / low.width, 1f / low.height);
			using (var builder = graph.AddRasterRenderPass<MmdSampleAtmospherePassData>("MMD Sample: raymarch clouds and fog", out var data)) {
				data.Material = Material;
				data.Size = size;
				builder.UseTexture(resources.cameraDepthTexture);
				builder.UseTexture(cloudShadow);
				if (resources.mainShadowsTexture.IsValid()) {
					builder.UseTexture(resources.mainShadowsTexture);
				}
				builder.SetRenderAttachment(volume, 0);
				builder.SetGlobalTextureAfterPass(volume, VolumeTexture);
				builder.AllowGlobalStateModification(true);
				builder.SetRenderFunc(static (MmdSampleAtmospherePassData p, RasterGraphContext context) => {
					context.cmd.SetGlobalVector(VolumeSize, p.Size);
					context.cmd.SetGlobalVector(Shader.PropertyToID("_BlitScaleBias"), new Vector4(1, 1, 0, 0));
					context.cmd.DrawProcedural(Matrix4x4.identity, p.Material, 0, MeshTopology.Triangles, 3);
				});
			}
			full.name = "MMD Sample Atmosphere Composite";
			full.clearBuffer = false;
			var result = graph.CreateTexture(full);
			using (var builder = graph.AddRasterRenderPass<MmdSampleAtmospherePassData>("MMD Sample: depth-aware atmosphere composite", out var data)) {
				data.Material = Material;
				data.Source = source;
				builder.UseTexture(source);
				builder.UseTexture(volume);
				builder.UseTexture(cloudShadow);
				if (resources.mainShadowsTexture.IsValid()) {
					builder.UseTexture(resources.mainShadowsTexture);
				}
				builder.UseTexture(resources.cameraDepthTexture);
				builder.SetRenderAttachment(result, 0);
				builder.SetRenderFunc(static (MmdSampleAtmospherePassData p, RasterGraphContext context) => {
					Blitter.BlitTexture(context.cmd, p.Source, new Vector4(1, 1, 0, 0), p.Material, 1);
				});
			}
			resources.cameraColor = result;
		}
	}
}
