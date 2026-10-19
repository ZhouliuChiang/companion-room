// Paper, covers and pop-up cards of the 3D record journal. Unlit, with a soft fixed light from
// over the viewer's shoulder (in view space), so the pages read the same at any time of day and
// a turning page still shades as it bends. Alpha-tested for the pop-up cards' rounded corners.
Shader "CompanionRoom/JournalPaper"
{
    Properties
    {
        _BaseMap ("Texture", 2D) = "white" {}
        _BaseColor ("Tint", Color) = (1, 1, 1, 1)
        _Cutoff ("Alpha Cutoff", Range(0, 1)) = 0.5
        _Shade ("Shading Amount", Range(0, 1)) = 1
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 2
    }
    SubShader
    {
        Tags { "RenderType" = "TransparentCutout" "Queue" = "AlphaTest" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        Pass
        {
            Name "Paper"
            Tags { "LightMode" = "SRPDefaultUnlit" }
            Cull [_Cull]
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
                half _Cutoff;
                half _Shade;
                float _Cull;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 normalVS : TEXCOORD1;
                half4 color : COLOR;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.normalVS = TransformWorldToViewDir(TransformObjectToWorldNormal(input.normalOS));
                output.color = input.color;
                return output;
            }

            half4 frag(Varyings input, FRONT_FACE_TYPE face : FRONT_FACE_SEMANTIC) : SV_Target
            {
                half4 color = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv) * _BaseColor * input.color;
                clip(color.a - _Cutoff);
                // View space looks down -Z: a page facing the viewer has normal (0, 0, 1).
                float3 normal = normalize(input.normalVS) * IS_FRONT_VFACE(face, 1.0, -1.0);
                float3 light = normalize(float3(-0.35, 0.55, 0.76));
                half shade = saturate(0.56 + 0.6 * dot(normal, light));
                color.rgb *= lerp(1.0, shade, _Shade);
                return half4(color.rgb, 1.0);
            }
            ENDHLSL
        }
    }
}
