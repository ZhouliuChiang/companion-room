Shader "CompanionRoom/PendulumMetal"
{
    Properties
    {
        _BaseMap("Original finish", 2D) = "white" {}
        _BaseColor("Tint", Color) = (1,.96,.91,1)
        _Metallic("Metallic", Range(0,1)) = .9
        _Smoothness("Polish", Range(0,1)) = .82
        _PreviewLighting("Presentation fill", Range(0,1)) = 0
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma vertex PendulumVertex
            #pragma fragment Fragment
            #pragma multi_compile_instancing
            #include "PendulumStudio.hlsl"
            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor; float _Metallic; float _Smoothness; float _PreviewLighting;
            CBUFFER_END
            half4 Fragment(PendulumVaryings input) : SV_Target
            {
                float3 n=normalize(input.normalWS), v=normalize(GetWorldSpaceViewDir(input.positionWS));
                float3 base=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,input.uv).rgb*_BaseColor.rgb;
                float3 reflected=PendulumStudio(reflect(-v,n));
                float edge=pow(1-saturate(dot(n,v)),5);
                float diffuse=.25+.35*saturate(dot(n,normalize(float3(-.6,.7,-.6))));
                float3 color=base*diffuse*(1-_Metallic*.65) + reflected*lerp(.12,base,_Metallic)*(.45+_Smoothness*.75);
                color+=reflected*edge*.25;
                // Broad softbox sheen for the close-up; room furniture keeps its normal finish.
                float presentationSheen=pow(saturate(dot(n,normalize(v+normalize(float3(.3,.45,-1))))),36);
                color+=saturate(_PreviewLighting)*presentationSheen*float3(.08,.065,.10);
                return half4(color*PendulumRoomLight(n,_PreviewLighting),1);
            }
            ENDHLSL
        }
    }
}
