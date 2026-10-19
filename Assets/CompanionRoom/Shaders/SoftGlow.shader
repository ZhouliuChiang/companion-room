// A soft additive halo that always faces the camera (lamp glow). _Intensity is driven per renderer.
Shader "CompanionRoom/SoftGlow"
{
    Properties
    {
        _Color ("Color", Color) = (1, 0.8, 0.55, 1)
        _Intensity ("Intensity", Float) = 1
        _Falloff ("Falloff", Range(0.5, 6)) = 2.2
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent+10" "IgnoreProjector" = "True" }

        Pass
        {
            Name "Glow"
            Tags { "LightMode" = "UniversalForward" }
            Blend One One
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half _Intensity;
                half _Falloff;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };

            Varyings vert(Attributes input)
            {
                Varyings output;
                float3 centerWS = TransformObjectToWorld(float3(0, 0, 0));
                float3 scale = float3(length(UNITY_MATRIX_M._m00_m10_m20), length(UNITY_MATRIX_M._m01_m11_m21), length(UNITY_MATRIX_M._m02_m12_m22));
                float3 centerVS = TransformWorldToView(centerWS);
                float3 positionVS = centerVS + float3(input.positionOS.xy * scale.xy, 0);
                output.positionCS = TransformWViewToHClip(positionVS);
                output.uv = input.uv;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float d = saturate(1.0 - length(input.uv - 0.5) * 2.0);
                float glow = pow(d, _Falloff);
                return half4(_Color.rgb * glow * _Intensity, 1);
            }
            ENDHLSL
        }
    }
}
