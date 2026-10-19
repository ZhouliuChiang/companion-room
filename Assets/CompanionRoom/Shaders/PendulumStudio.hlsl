#ifndef PENDULUM_STUDIO_INCLUDED
#define PENDULUM_STUDIO_INCLUDED
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
// RoomLighting updates the ambient probe throughout the day/night transition.
// Attenuate every studio highlight as well as the base tint so neither finish glows at night.
float3 PendulumRoomLight(float3 normalWS, float previewLighting)
{
    float3 room = clamp(SampleSH(normalize(normalWS))*1.8, float3(.08,.08,.08), float3(1,1,1));
    // Gentle presentation fill: leave headroom for the crystal's soft emission.
    float key = saturate(dot(normalize(normalWS), normalize(float3(-.35,.55,-1))));
    float3 presentation = float3(.78,.70,.90)*(.92+key*.12);
    return lerp(room, presentation, saturate(previewLighting));
}
struct PendulumAttributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
struct PendulumVaryings { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; float3 normalWS : TEXCOORD1; float2 uv : TEXCOORD2; };
PendulumVaryings PendulumVertex(PendulumAttributes input)
{
    PendulumVaryings output;
    UNITY_SETUP_INSTANCE_ID(input);
    VertexPositionInputs p = GetVertexPositionInputs(input.positionOS.xyz);
    output.positionCS = p.positionCS;
    output.positionWS = p.positionWS;
    output.normalWS = TransformObjectToWorldNormal(input.normalOS);
    output.uv = input.uv;
    return output;
}
// Reflected softboxes make the finish readable in both the room and the model stage.
float3 PendulumStudio(float3 direction)
{
    float3 r = normalize(direction);
    float3 color = lerp(float3(.045,.025,.075), float3(.42,.35,.49), saturate(r.y*.5+.5));
    float key = pow(saturate(dot(r, normalize(float3(-.6,.7,-.4)))), 48);
    float strip = pow(saturate(dot(r, normalize(float3(.8,.1,-.5)))), 100);
    float rim = pow(saturate(dot(r, normalize(float3(.1,.5,.8)))), 55);
    float front = pow(saturate(dot(r, normalize(float3(.15,.28,-1)))), 32);
    return color + key*float3(3.8,3.4,2.8) + strip*float3(2.5,3.2,4.2) + rim*float3(2.8,1.8,4) + front*float3(1.4,1.5,1.9);
}
#endif
