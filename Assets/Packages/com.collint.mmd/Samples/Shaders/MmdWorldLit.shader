Shader "ColInt/MMD/World Lit" {
	Properties {
		[MainTexture] _BaseMap("Base Map", 2D) = "white" {}
		[MainColor] _BaseColor("Color", Color) = (1, 1, 1, 1)
		_WorldTileSize("Tile Size (World Meters)", Float) = 1
		_WorldRotation("World Rotation (Degrees)", Range(0, 360)) = 0
		_BlendSharpness("Triplanar Blend Sharpness", Range(1, 16)) = 4
		_Metallic("Metallic", Range(0, 1)) = 0
		_Smoothness("Smoothness", Range(0, 1)) = 0
	}
	SubShader {
		Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
		HLSLINCLUDE
		#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
		#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/SurfaceInput.hlsl"
		CBUFFER_START(UnityPerMaterial)
		float4 _BaseMap_ST;
		half4 _BaseColor;
		float _WorldTileSize;
		float _WorldRotation;
		float _BlendSharpness;
		half _Metallic;
		half _Smoothness;
		CBUFFER_END

		half3 SampleWorldAlbedo(float3 positionWS, float3 normalWS) {
			float sine;
			float cosine;
			sincos(radians(_WorldRotation), sine, cosine);
			float2x2 rotation = float2x2(cosine, -sine, sine, cosine);
			// Absolute world position keeps the pattern aligned across separate meshes.
			float3 position = GetAbsolutePositionWS(positionWS);
			position.xz = mul(rotation, position.xz);
			normalWS.xz = mul(rotation, normalWS.xz);
			position /= max(_WorldTileSize, 0.0001);
			float3 weights = pow(abs(normalWS), max(_BlendSharpness, 1.0));
			weights /= max(weights.x + weights.y + weights.z, 0.0001);
			float2 uvX = position.zy * _BaseMap_ST.xy + _BaseMap_ST.zw;
			float2 uvY = position.xz * _BaseMap_ST.xy + _BaseMap_ST.zw;
			float2 uvZ = position.xy * _BaseMap_ST.xy + _BaseMap_ST.zw;
			half3 albedoX = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uvX).rgb;
			half3 albedoY = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uvY).rgb;
			half3 albedoZ = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uvZ).rgb;
			return (albedoX * weights.x + albedoY * weights.y + albedoZ * weights.z) * _BaseColor.rgb;
		}
		ENDHLSL

		Pass {
			Name "ForwardLit"
			Tags { "LightMode"="UniversalForwardOnly" }
			HLSLPROGRAM
			#pragma target 3.5
			#pragma vertex WorldLitVertex
			#pragma fragment WorldLitFragment
			#pragma multi_compile_instancing
			#include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"
			#pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
			#pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
			#pragma multi_compile _ _CLUSTER_LIGHT_LOOP
			#pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
			#pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
			#pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
			#pragma multi_compile_fragment _ _LIGHT_COOKIES
			#pragma multi_compile_fragment _ _LIGHT_LAYERS
			#pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
			#pragma multi_compile_fragment _ _REFLECTION_PROBE_BOX_PROJECTION
			#pragma multi_compile _ LIGHTMAP_ON
			#pragma multi_compile _ DYNAMICLIGHTMAP_ON
			#pragma multi_compile _ DIRLIGHTMAP_COMBINED
			#pragma multi_compile _ LIGHTMAP_SHADOW_MIXING
			#pragma multi_compile _ SHADOWS_SHADOWMASK
			#pragma multi_compile_fog
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

			struct Attributes {
				float3 positionOS : POSITION;
				float3 normalOS : NORMAL;
				float2 staticLightmapUV : TEXCOORD1;
				float2 dynamicLightmapUV : TEXCOORD2;
				UNITY_VERTEX_INPUT_INSTANCE_ID
			};
			struct Varyings {
				float4 positionCS : SV_POSITION;
				float3 positionWS : TEXCOORD0;
				float3 normalWS : TEXCOORD1;
				half4 fogAndVertexLight : TEXCOORD2;
				float4 shadowCoord : TEXCOORD3;
				DECLARE_LIGHTMAP_OR_SH(staticLightmapUV, vertexSH, 4);
				#ifdef DYNAMICLIGHTMAP_ON
				float2 dynamicLightmapUV : TEXCOORD5;
				#endif
				UNITY_VERTEX_INPUT_INSTANCE_ID
				UNITY_VERTEX_OUTPUT_STEREO
			};
			Varyings WorldLitVertex(Attributes input) {
				Varyings output = (Varyings)0;
				UNITY_SETUP_INSTANCE_ID(input);
				UNITY_TRANSFER_INSTANCE_ID(input, output);
				UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
				VertexPositionInputs position = GetVertexPositionInputs(input.positionOS);
				output.positionCS = position.positionCS;
				output.positionWS = position.positionWS;
				output.normalWS = TransformObjectToWorldNormal(input.normalOS);
				output.fogAndVertexLight = half4(ComputeFogFactor(position.positionCS.z), VertexLighting(position.positionWS, output.normalWS));
				output.shadowCoord = GetShadowCoord(position);
				OUTPUT_LIGHTMAP_UV(input.staticLightmapUV, unity_LightmapST, output.staticLightmapUV);
				OUTPUT_SH(output.normalWS, output.vertexSH);
				#ifdef DYNAMICLIGHTMAP_ON
				output.dynamicLightmapUV = input.dynamicLightmapUV * unity_DynamicLightmapST.xy + unity_DynamicLightmapST.zw;
				#endif
				return output;
			}
			half4 WorldLitFragment(Varyings input) : SV_Target {
				UNITY_SETUP_INSTANCE_ID(input);
				UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
				InputData lighting = (InputData)0;
				lighting.positionWS = input.positionWS;
				lighting.positionCS = input.positionCS;
				lighting.normalWS = NormalizeNormalPerPixel(input.normalWS);
				lighting.viewDirectionWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
				#if defined(_MAIN_LIGHT_SHADOWS_SCREEN)
				lighting.shadowCoord = input.shadowCoord;
				#else
				lighting.shadowCoord = TransformWorldToShadowCoord(input.positionWS);
				#endif
				lighting.vertexLighting = input.fogAndVertexLight.yzw;
				lighting.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
				lighting.shadowMask = SAMPLE_SHADOWMASK(input.staticLightmapUV);
				#ifdef DYNAMICLIGHTMAP_ON
				lighting.bakedGI = SAMPLE_GI(input.staticLightmapUV, input.dynamicLightmapUV, input.vertexSH, lighting.normalWS);
				#else
				lighting.bakedGI = SAMPLE_GI(input.staticLightmapUV, input.vertexSH, lighting.normalWS);
				#endif
				SurfaceData surface = (SurfaceData)0;
				surface.albedo = SampleWorldAlbedo(input.positionWS, lighting.normalWS);
				surface.metallic = _Metallic;
				surface.smoothness = _Smoothness;
				surface.normalTS = half3(0, 0, 1);
				surface.occlusion = 1;
				surface.alpha = 1;
				half4 color = UniversalFragmentPBR(lighting, surface);
				color.rgb = MixFog(color.rgb, input.fogAndVertexLight.x);
				return color;
			}
			ENDHLSL
		}
		Pass {
			Name "ShadowCaster"
			Tags { "LightMode"="ShadowCaster" }
			ZWrite On
			ZTest LEqual
			ColorMask 0
			HLSLPROGRAM
			#pragma target 3.5
			#pragma vertex ShadowPassVertex
			#pragma fragment ShadowPassFragment
			#pragma multi_compile_instancing
			#include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"
			#pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
			#include "Packages/com.unity.render-pipelines.universal/Shaders/ShadowCasterPass.hlsl"
			ENDHLSL
		}
		Pass {
			Name "DepthOnly"
			Tags { "LightMode"="DepthOnly" }
			ZWrite On
			ColorMask R
			HLSLPROGRAM
			#pragma target 3.5
			#pragma vertex DepthOnlyVertex
			#pragma fragment DepthOnlyFragment
			#pragma multi_compile_instancing
			#include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/Shaders/DepthOnlyPass.hlsl"
			ENDHLSL
		}
		Pass {
			Name "DepthNormals"
			Tags { "LightMode"="DepthNormalsOnly" }
			ZWrite On
			HLSLPROGRAM
			#pragma target 3.5
			#pragma vertex DepthNormalsVertex
			#pragma fragment DepthNormalsFragment
			#pragma multi_compile_instancing
			#include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"
			#pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
			#include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/Shaders/DepthNormalsPass.hlsl"
			ENDHLSL
		}
		Pass {
			Name "Meta"
			Tags { "LightMode"="Meta" }
			Cull Off
			HLSLPROGRAM
			#pragma target 3.5
			#pragma vertex WorldMetaVertex
			#pragma fragment WorldMetaFragment
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/MetaInput.hlsl"
			struct Attributes {
				float3 positionOS : POSITION;
				float3 normalOS : NORMAL;
				float2 lightmapUV : TEXCOORD1;
				float2 dynamicLightmapUV : TEXCOORD2;
			};
			struct Varyings {
				float4 positionCS : SV_POSITION;
				float3 positionWS : TEXCOORD0;
				float3 normalWS : TEXCOORD1;
			};
			Varyings WorldMetaVertex(Attributes input) {
				Varyings output;
				output.positionCS = UnityMetaVertexPosition(input.positionOS, input.lightmapUV, input.dynamicLightmapUV);
				output.positionWS = TransformObjectToWorld(input.positionOS);
				output.normalWS = TransformObjectToWorldNormal(input.normalOS);
				return output;
			}
			half4 WorldMetaFragment(Varyings input) : SV_Target {
				half3 albedo = SampleWorldAlbedo(input.positionWS, NormalizeNormalPerPixel(input.normalWS));
				half alpha = 1;
				BRDFData brdf;
				InitializeBRDFData(albedo, _Metallic, half3(0, 0, 0), _Smoothness, alpha, brdf);
				MetaInput meta = (MetaInput)0;
				meta.Albedo = brdf.diffuse + brdf.specular * brdf.roughness * 0.5;
				return UnityMetaFragment(meta);
			}
			ENDHLSL
		}
	}
	Fallback Off
}
