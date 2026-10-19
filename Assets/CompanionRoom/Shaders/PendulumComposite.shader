Shader "CompanionRoom/PendulumComposite"
{
    Properties { [PerRendererData] _MainTex("Preview", 2D) = "white" {} }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Blend One OneMinusSrcAlpha
        Cull Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            struct Attributes { float4 vertex:POSITION; float2 uv:TEXCOORD0; float4 color:COLOR; };
            struct Varyings { float4 position:SV_POSITION; float2 uv:TEXCOORD0; float4 color:COLOR; };
            sampler2D _MainTex;
            float4 _MainTex_TexelSize;
            Varyings vert(Attributes input)
            {
                Varyings output;
                output.position=UnityObjectToClipPos(input.vertex);
                output.uv=input.uv;
                output.color=input.color;
                return output;
            }
            fixed4 frag(Varyings input):SV_Target
            {
                fixed4 color=tex2D(_MainTex,input.uv);
                // A small lavender halo belongs to this UI preview, never the room camera.
                float2 radius=_MainTex_TexelSize.xy*3;
                float blur=tex2D(_MainTex,input.uv+float2(radius.x,0)).a;
                blur+=tex2D(_MainTex,input.uv-float2(radius.x,0)).a;
                blur+=tex2D(_MainTex,input.uv+float2(0,radius.y)).a;
                blur+=tex2D(_MainTex,input.uv-float2(0,radius.y)).a;
                blur+=tex2D(_MainTex,input.uv+radius).a;
                blur+=tex2D(_MainTex,input.uv-radius).a;
                blur+=tex2D(_MainTex,input.uv+float2(radius.x,-radius.y)).a;
                blur+=tex2D(_MainTex,input.uv+float2(-radius.x,radius.y)).a;
                float halo=max(0,blur*.125-color.a);
                color.rgb+=float3(.32,.19,.52)*halo*.22;
                color.rgb*=input.color.rgb*input.color.a;
                color.a*=input.color.a;
                return color;
            }
            ENDCG
        }
    }
}
