// Inverted-hull outline drawn around hovered room items. Width is in screen pixels at 1080p,
// so the glow looks the same near and far. Used by HoverOutline shells only.
Shader "CompanionRoom/HoverOutline"
{
    Properties
    {
        _OutlineColor ("Color", Color) = (1, 0.86, 0.62, 1)
        _OutlineWidth ("Width (px @1080p)", Float) = 3
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry+50" }

        Pass
        {
            Name "Outline"
            Tags { "LightMode" = "SRPDefaultUnlit" }
            Cull Front
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _OutlineColor;
                float _OutlineWidth;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                float4 positionCS = TransformObjectToHClip(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                float2 normalCS = mul((float3x3)GetWorldToHClipMatrix(), normalWS).xy;
                float len = length(normalCS);
                float2 dir = len > 1e-5 ? normalCS / len : float2(0, 0);
                // Pixels → clip space; scaled by w so the width holds at any distance.
                float pixels = _OutlineWidth * (_ScreenParams.y / 1080.0);
                positionCS.xy += dir * pixels * 2.0 / _ScreenParams.xy * positionCS.w;
                output.positionCS = positionCS;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                return _OutlineColor;
            }
            ENDHLSL
        }
    }
}
