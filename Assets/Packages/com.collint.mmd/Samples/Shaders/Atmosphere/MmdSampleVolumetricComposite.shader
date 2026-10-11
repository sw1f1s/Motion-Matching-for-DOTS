Shader "Hidden/Motion Matching for DOTS/Sample/Volumetric Composite"
{
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        Cull Off ZWrite Off
        HLSLINCLUDE
        #pragma target 4.5
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        #include "MmdSampleAtmosphereCommon.hlsl"
        ENDHLSL
        Pass
        {
            Name "LowResolutionVolumes"
            ZTest Always
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment VolumeFragment
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            float4 _VAVolumeSize;
            bool IsSky(float depth)
            {
                #if UNITY_REVERSED_Z
                return depth < .000001;
                #else
                return depth > .999999;
                #endif
            }
            half4 VolumeFragment(Varyings input) : SV_Target
            {
                float2 uv = input.texcoord;
                float raw = SampleSceneDepth(uv);
                bool sky = IsSky(raw);
                #if !UNITY_REVERSED_Z
                raw = lerp(UNITY_NEAR_CLIP_VALUE, 1, raw);
                #endif
                float3 world = ComputeWorldSpacePosition(uv, raw, UNITY_MATRIX_I_VP);
                float3 origin = GetCameraPositionWS();
                float3 delta = world - origin;
                float3 ray = normalize(delta);
                if (unity_OrthoParams.w > .5)
                {
                    ray = -UNITY_MATRIX_V[2].xyz;
                    origin = world - ray * LinearEyeDepth(raw, _ZBufferParams);
                }
                float jitter = lerp(.5, VABayer(input.positionCS.xy), .45);
                float4 fog = VAFog(origin, ray, sky ? _VAFogShape.w : length(delta), jitter, clamp((int)_VAFogDetail.w, 8, 48));
                if (sky)
                {
                    float3 clouds = VACloudSky(origin, ray, jitter);
                    return half4(VAQuantize(clouds * (1 - fog.a) + fog.rgb, input.positionCS.xy), 1);
                }
                return fog;
            }
            ENDHLSL
        }
        Pass
        {
            Name "DepthAwareComposite"
            ZTest Always
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment CompositeFragment
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            TEXTURE2D_X(_VAVolumeTexture);
            float4 _VAVolumeSize;
            half4 CompositeFragment(Varyings input) : SV_Target
            {
                float2 uv = input.texcoord;
                float depth = LinearEyeDepth(SampleSceneDepth(uv), _ZBufferParams);
                float2 cell = floor(uv * _VAVolumeSize.xy - .5);
                float bestError = 1e20;
                float2 bestUV = uv;
                // Choose a low-res sample on the same surface, avoiding bright cloud halos around trees.
                [unroll] for (int y = 0; y < 2; y++)
                [unroll] for (int x = 0; x < 2; x++)
                {
                    float2 sampleUV = (cell + float2(x, y) + .5) * _VAVolumeSize.zw;
                    float other = LinearEyeDepth(SampleSceneDepth(sampleUV), _ZBufferParams);
                    float error = abs(other - depth) / max(depth, .1) + length(sampleUV - uv) * .01;
                    if (error < bestError) { bestError = error; bestUV = sampleUV; }
                }
                half4 original = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv);
                half4 volume = SAMPLE_TEXTURE2D_X(_VAVolumeTexture, sampler_PointClamp, bestUV);
                // At thin leaves none of the four low-res samples may represent this surface.
                // Integrate at its actual depth instead of dropping fog and exposing a dark fringe.
                if (bestError > .25)
                {
                    float raw = SampleSceneDepth(uv);
                    #if !UNITY_REVERSED_Z
                    raw = lerp(UNITY_NEAR_CLIP_VALUE, 1, raw);
                    #endif
                    float3 origin = GetCameraPositionWS();
                    float3 world = ComputeWorldSpacePosition(uv, raw, UNITY_MATRIX_I_VP);
                    float3 delta = world - origin;
                    float3 ray = normalize(delta);
                    bool sky = depth > _ProjectionParams.z * .99;
                    float4 edgeFog = VAFog(origin, ray, sky ? _VAFogShape.w : length(delta), .5, 6);
                    float3 baseColor = sky ? VACloudSky(origin, ray, .5) : original.rgb;
                    return half4(baseColor * (1 - edgeFog.a) + edgeFog.rgb, original.a);
                }
                return half4(original.rgb * (1 - volume.a) + volume.rgb, original.a);
            }
            ENDHLSL
        }
        Pass
        {
            Name "CloudSunlight"
            ZTest Always
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment CloudLightFragment
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            half4 CloudLightFragment(Varyings input) : SV_Target
            {
                float2 worldXZ = _VACloudShadowRegion.xy + (input.texcoord - .5) / _VACloudShadowRegion.z;
                return VACloudLightTransmission(worldXZ).xxxx;
            }
            ENDHLSL
        }

    }
    Fallback Off
}
