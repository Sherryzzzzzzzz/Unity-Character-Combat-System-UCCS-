Shader "Custom/InfiniteGrid"
{
    Properties
    {
        [HDR] _FloorColor ("Floor Color", Color) = (0.25, 0.25, 0.25, 1)
        [HDR] _LineColor  ("Grid Line Color", Color) = (0.5, 0.5, 0.5, 1)
        _GridSize  ("Grid Size", Range(0.5, 50)) = 5
        _LineWidth ("Line Width", Range(0.001, 0.3)) = 0.05
        _GridDrawDistance ("Grid Draw Distance", Range(5, 100)) = 25
        _FadeStart ("Fade Start Distance", Range(0, 200)) = 30
        _FadeEnd   ("Fade End Distance", Range(0, 200)) = 80
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Geometry"
        }

        // ============================================================
        // Forward Lit
        // ============================================================
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex ForwardVert
            #pragma fragment ForwardFrag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct VaryingsForward
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _FloorColor;
                float4 _LineColor;
                float  _GridSize;
                float  _LineWidth;
                float  _GridDrawDistance;
                float  _FadeStart;
                float  _FadeEnd;
            CBUFFER_END

            VaryingsForward ForwardVert(Attributes input)
            {
                VaryingsForward output;
                VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = vertexInput.positionCS;
                output.positionWS = vertexInput.positionWS;
                return output;
            }

            half4 ForwardFrag(VaryingsForward input) : SV_Target
            {
                float3 worldPos = input.positionWS;

                // ---- distance from camera ----
                float distToCamera = distance(worldPos, _WorldSpaceCameraPos);

                // ---- fade (world-space for infinite horizon feel) ----
                float distXZ = length(worldPos.xz);
                float fade = 1.0 - saturate((distXZ - _FadeStart) / max(_FadeEnd - _FadeStart, 0.01));

                float3 color;

                // ▸ 远离相机 → 跳过格子计算，直接用纯色地板 + fade
                if (distToCamera > _GridDrawDistance)
                {
                    color = _FloorColor.rgb;
                }
                else
                {
                    // ---- grid lines on XZ plane (仅近处计算) ----
                    float2 uv = worldPos.xz / _GridSize;
                    float2 cellFrac  = frac(uv);
                    float2 distToBorder = min(cellFrac, 1.0 - cellFrac);
                    float2 ln = 1.0 - smoothstep(0.0, _LineWidth, distToBorder);
                    float  gridLine = max(ln.x, ln.y);
                    color = lerp(_FloorColor.rgb, _LineColor.rgb, gridLine);
                }

                // ---- simple directional light ----
                Light mainLight = GetMainLight();
                float3 normal = float3(0, 1, 0);
                float NdotL = saturate(dot(normal, mainLight.direction));
                float3 ambient = SampleSH(normal);
                float3 lit = color * (mainLight.color * NdotL * mainLight.shadowAttenuation + ambient);

                // blend to horizon
                float3 horizon = float3(0.12, 0.12, 0.12);
                lit = lerp(horizon, lit, fade);

                return half4(lit, 1.0);
            }
            ENDHLSL
        }

        // ============================================================
        // Shadow Caster
        // ============================================================
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual
            ColorMask 0

            HLSLPROGRAM
            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            struct ShadowAttributes
            {
                float4 positionOS : POSITION;
            };

            struct VaryingsShadow
            {
                float4 positionCS : SV_POSITION;
            };

            float3 _LightDirection;
            float3 _LightPosition;

            VaryingsShadow ShadowVert(ShadowAttributes input)
            {
                VaryingsShadow output;
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = float3(0, 1, 0);
                output.positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, _LightDirection));
                return output;
            }

            half4 ShadowFrag(VaryingsShadow input) : SV_TARGET
            {
                return 0;
            }
            ENDHLSL
        }

        // ============================================================
        // Depth Only
        // ============================================================
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            ZWrite On
            ColorMask R

            HLSLPROGRAM
            #pragma vertex DepthVert
            #pragma fragment DepthFrag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct DepthAttributes
            {
                float4 positionOS : POSITION;
            };

            struct VaryingsDepth
            {
                float4 positionCS : SV_POSITION;
            };

            VaryingsDepth DepthVert(DepthAttributes input)
            {
                VaryingsDepth output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                return output;
            }

            half4 DepthFrag(VaryingsDepth input) : SV_TARGET
            {
                return input.positionCS.z / input.positionCS.w;
            }
            ENDHLSL
        }
    }

    FallBack "Universal Render Pipeline/Lit"
}
