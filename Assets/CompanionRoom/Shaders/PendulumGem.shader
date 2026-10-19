Shader "CompanionRoom/PendulumGem"
{
    Properties
    {
        _BaseColor("Amethyst tint", Color) = (.84,.70,1,1)
        _IOR("Refraction index", Range(1,2.5)) = 1.54
        _Opacity("Crystal body", Range(0,1)) = .18
        _Sparkle("Sparkle", Range(0,4)) = 1.3
        _Iridescence("Iridescent film", Range(0,1)) = .65
        _PreviewLighting("Presentation fill", Range(0,1)) = 0
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
            ZWrite Off
            Cull Back
            HLSLPROGRAM
            #pragma vertex PendulumVertex
            #pragma fragment Fragment
            #pragma multi_compile_instancing
            #include "PendulumStudio.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor; float _IOR; float _Opacity; float _Sparkle; float _Iridescence; float _PreviewLighting;
            CBUFFER_END
            half4 Fragment(PendulumVaryings input) : SV_Target
            {
                float3 n=normalize(input.normalWS), v=normalize(GetWorldSpaceViewDir(input.positionWS));
                float facing=saturate(dot(n,v));
                float fresnel=pow(1-facing,5);
                float3 reflected=PendulumStudio(reflect(-v,n));
                float3 refracted=refract(-v,n,1/_IOR);
                float3 internalLight=PendulumStudio(refracted)*.55+PendulumStudio(reflect(refracted,n))*.45;
                float2 screenUV=GetNormalizedScreenSpaceUV(input.positionCS);
                float2 offset=TransformWorldToViewDir(n).xy*.018*(1-facing);
                float3 behind=SampleSceneColor(saturate(screenUV+offset));
                float3 color=_BaseColor.rgb*(.22+internalLight*.6);
                color+=reflected*(.08+fresnel*.8);
                float3 key=normalize(float3(-.22,.35,-1));
                float flash=pow(saturate(dot(n,normalize(v+key))),90);
                float rimFlash=pow(saturate(dot(n,normalize(v+normalize(float3(.45,-.3,-1))))),130);
                color+=(flash*float3(1.8,1.6,2.2)+rimFlash*float3(.9,1.3,2))*_Sparkle;
                // Angle-dependent thin-film interference; highlights shift as the crystal swings.
                float opticalPath=facing*1.8+dot(n,normalize(float3(.4,.6,-.7)))*.22;
                float3 spectrum=.5+.5*cos(6.2831853*(opticalPath+float3(0,.333333,.666667)));
                float3 film=lerp(float3(1,1,1),spectrum,.72);
                color+=film*_Iridescence*(.25+pow(1-facing,2)*.65+flash*.4);
                color*=PendulumRoomLight(n,_PreviewLighting);
                // Local emission is applied after ambient attenuation, only on the active preview.
                float3 emission=float3(.32,.13,.58)*(.55+pow(1-facing,2)*.45);
                color+=saturate(_PreviewLighting)*emission;
                // The refracted room is already lit; do not darken that contribution twice.
                color+=behind*lerp(float3(1,1,1),_BaseColor.rgb,.35)*.45;
                return half4(color,saturate(_Opacity+fresnel*.22+flash*.10));
            }
            ENDHLSL
        }
    }
}
