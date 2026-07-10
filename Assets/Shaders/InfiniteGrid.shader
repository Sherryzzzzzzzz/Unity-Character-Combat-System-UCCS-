Shader "Custom/InfiniteGrid"
{
    Properties
    {
        [HDR] _FloorColor ("Floor Color", Color) = (0.25, 0.25, 0.25, 1)
        [HDR] _LineColor  ("Grid Line Color", Color) = (0.5, 0.5, 0.5, 1)
        _GridSize  ("Grid Size", Range(0.5, 50)) = 5
        _LineWidth ("Line Width", Range(0.001, 0.3)) = 0.05
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

                // ---- floor with grid lines on XZ plane ----
                float2 uv = worldPos.xz / _GridSize;
                float2 cellFrac  = frac(uv);

                // distance to nearest cell border [0, 0.5]
                float2 distToBorder = min(cellFrac, 1.0 - cellFrac);
                // smooth line: 1 = at border (draw line), 0 = interior (draw floor)
                float2 ln = 1.0 - smoothstep(0.0, _LineWidth, distToBorder);
                // use max to prevent additive blending at intersections (no thick corners)
                float  gridLine = max(ln.x, ln.y);
                // t=0 → floor color, t=1 → line color
                float3 color = lerp(_FloorColor.rgb, _LineColor.rgb, gridLine);

                // ---- distance fade for infinite feel ----
                float dist = length(worldPos.xz);
                float fade  = 1.0 - saturate((dist - _FadeStart) / max(_FadeEnd - _FadeStart, 0.01));

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
