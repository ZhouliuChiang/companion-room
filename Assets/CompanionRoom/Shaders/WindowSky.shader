// The view outside the window: a vertical gradient with a soft sun/moon disc and twinkling
// stars. RoomLighting sets every property per time of day through a MaterialPropertyBlock.
Shader "CompanionRoom/WindowSky"
{
    Properties
    {
        _TopColor ("Top", Color) = (0.53, 0.76, 0.94, 1)
        _BottomColor ("Bottom", Color) = (0.98, 0.91, 0.94, 1)
        _DiscColor ("Disc", Color) = (1, 0.98, 0.9, 1)
        _Disc ("Disc (x, y, radius)", Vector) = (0.72, 0.78, 0.09, 0)
        _Stars ("Stars", Range(0, 1)) = 0
        _Brightness ("Brightness", Float) = 1.15
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        Pass
        {
            Name "Sky"
            Tags { "LightMode" = "UniversalForward" }
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _TopColor;
                half4 _BottomColor;
                half4 _DiscColor;
                float4 _Disc;
                half _Stars;
                half _Brightness;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }

            float Hash(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            half4 frag(Varyings input) : SV_Target
            {
                float2 uv = input.uv;
                half3 color = lerp(_BottomColor.rgb, _TopColor.rgb, smoothstep(0.0, 1.0, uv.y));

                float d = distance(uv, _Disc.xy);
                float r = max(_Disc.z, 0.001);
                color = lerp(color, _DiscColor.rgb, saturate((r - d) / (r * 0.12)));
                color += _DiscColor.rgb * saturate(1.0 - d / (r * 3.5)) * 0.35;

                float2 cell = floor(uv * 60.0);
                float h = Hash(cell);
                float2 center = (cell + 0.5 + (float2(Hash(cell + 7.1), Hash(cell + 3.3)) - 0.5) * 0.6) / 60.0;
                float twinkle = 0.6 + 0.4 * sin(_Time.y * (1.5 + h * 3.0) + h * 40.0);
                float star = step(0.93, h) * saturate(1.0 - distance(uv, center) * 60.0 * 2.2) * twinkle;
                color += star * _Stars * uv.y;

                return half4(color * _Brightness, 1);
            }
            ENDHLSL
        }
    }
}
