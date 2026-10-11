Shader "ColInt/MMD/Deformed Lit" {
	Properties {
		_BaseMap("Base Map", 2D) = "white" {}
		_BaseColor("Color", Color) = (1,1,1,1)
		_BumpMap("Normal Map", 2D) = "bump" {}
		_BumpScale("Normal Strength", Float) = 1
		[HideInInspector] _ComputeMeshIndex("Compute Mesh Index", Float) = 0
		[HideInInspector] _DotsDeformationParams("Deformation Parameters", Vector) = (0,0,0,0)
	}
	SubShader {
		Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
		HLSLINCLUDE
		#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
		#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
		CBUFFER_START(UnityPerMaterial)
		float4 _BaseMap_ST;
		float4 _BaseColor;
		float _BumpScale;
		float _ComputeMeshIndex;
		float4 _DotsDeformationParams;
		CBUFFER_END
		TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
		TEXTURE2D(_BumpMap); SAMPLER(sampler_BumpMap);
		#if defined(UNITY_DOTS_INSTANCING_ENABLED)
		UNITY_DOTS_INSTANCING_START(MaterialPropertyMetadata)
			UNITY_DOTS_INSTANCED_PROP(float, _ComputeMeshIndex)
			UNITY_DOTS_INSTANCED_PROP(float4, _DotsDeformationParams)
		UNITY_DOTS_INSTANCING_END(MaterialPropertyMetadata)
		struct MmdDeformedVertex { float3 position; float3 normal; float3 tangent; };
		StructuredBuffer<MmdDeformedVertex> _DeformedMeshData;
		#endif
		struct Attributes {
			float3 positionOS : POSITION;
			float3 normalOS : NORMAL;
			float4 tangentOS : TANGENT;
			float2 uv : TEXCOORD0;
			uint vertexID : SV_VertexID;
			UNITY_VERTEX_INPUT_INSTANCE_ID
		};
		struct Varyings {
			float4 positionCS : SV_POSITION;
			float3 positionWS : TEXCOORD0;
			float3 normalWS : TEXCOORD1;
			float4 tangentWS : TEXCOORD2;
			float2 uv : TEXCOORD3;
			UNITY_VERTEX_INPUT_INSTANCE_ID
			UNITY_VERTEX_OUTPUT_STEREO
		};
		void Deform(inout Attributes input) {
			#if defined(UNITY_DOTS_INSTANCING_ENABLED)
			uint index = asuint(UNITY_ACCESS_DOTS_INSTANCED_PROP(float, _ComputeMeshIndex));
			uint4 parameters = asuint(UNITY_ACCESS_DOTS_INSTANCED_PROP(float4, _DotsDeformationParams));
			uint doubleBufferedIndex = parameters[parameters.z & 1];
			if (doubleBufferedIndex != 0) { index = doubleBufferedIndex; }
			if (index != 0) {
				MmdDeformedVertex vertex = _DeformedMeshData[index + input.vertexID];
				input.positionOS = vertex.position;
				input.normalOS = vertex.normal;
				input.tangentOS.xyz = vertex.tangent;
			}
			#endif
		}
		Varyings Vertex(Attributes input) {
			Varyings output = (Varyings)0;
			UNITY_SETUP_INSTANCE_ID(input);
			UNITY_TRANSFER_INSTANCE_ID(input, output);
			UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
			Deform(input);
			VertexPositionInputs position = GetVertexPositionInputs(input.positionOS);
			VertexNormalInputs normal = GetVertexNormalInputs(input.normalOS, input.tangentOS);
			output.positionCS = position.positionCS;
			output.positionWS = position.positionWS;
			output.normalWS = normal.normalWS;
			output.tangentWS = float4(normal.tangentWS, input.tangentOS.w * GetOddNegativeScale());
			output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
			return output;
		}
		half4 Fragment(Varyings input) : SV_Target {
			UNITY_SETUP_INSTANCE_ID(input);
			half3 normalTS = UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, input.uv), _BumpScale);
			half3 bitangent = cross(input.normalWS, input.tangentWS.xyz) * input.tangentWS.w;
			half3 normal = normalize(TransformTangentToWorld(normalTS, half3x3(input.tangentWS.xyz, bitangent, input.normalWS)));
			Light light = GetMainLight(TransformWorldToShadowCoord(input.positionWS));
			half3 lighting = SampleSH(normal) + light.color * saturate(dot(normal, light.direction)) * light.distanceAttenuation * light.shadowAttenuation;
			return half4(SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv).rgb * _BaseColor.rgb * lighting, 1);
		}
		float3 _LightDirection;
		float3 _LightPosition;
		Varyings ShadowVertex(Attributes input) {
			Varyings output = Vertex(input);
			#if defined(_CASTING_PUNCTUAL_LIGHT_SHADOW)
			float3 direction = normalize(_LightPosition - output.positionWS);
			#else
			float3 direction = _LightDirection;
			#endif
			output.positionCS = TransformWorldToHClip(ApplyShadowBias(output.positionWS, output.normalWS, direction));
			#if UNITY_REVERSED_Z
			output.positionCS.z = min(output.positionCS.z, UNITY_NEAR_CLIP_VALUE * output.positionCS.w);
			#else
			output.positionCS.z = max(output.positionCS.z, UNITY_NEAR_CLIP_VALUE * output.positionCS.w);
			#endif
			return output;
		}
		half4 DepthFragment(Varyings input) : SV_Target { return 0; }
		ENDHLSL
		Pass {
			Name "ForwardLit"
			Tags { "LightMode"="UniversalForwardOnly" }
			HLSLPROGRAM
			#pragma target 4.5
			#pragma vertex Vertex
			#pragma fragment Fragment
			#pragma multi_compile_instancing
			#pragma multi_compile _ DOTS_INSTANCING_ON
			#pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
			#pragma multi_compile_fragment _ _SHADOWS_SOFT
			ENDHLSL
		}
		Pass {
			Name "ShadowCaster"
			Tags { "LightMode"="ShadowCaster" }
			ZWrite On ZTest LEqual ColorMask 0
			HLSLPROGRAM
			#pragma target 4.5
			#pragma vertex ShadowVertex
			#pragma fragment DepthFragment
			#pragma multi_compile_instancing
			#pragma multi_compile _ DOTS_INSTANCING_ON
			#pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
			ENDHLSL
		}
		Pass {
			Name "DepthOnly"
			Tags { "LightMode"="DepthOnly" }
			ZWrite On ColorMask R
			HLSLPROGRAM
			#pragma target 4.5
			#pragma vertex Vertex
			#pragma fragment DepthFragment
			#pragma multi_compile_instancing
			#pragma multi_compile _ DOTS_INSTANCING_ON
			ENDHLSL
		}
	}
}
