// Свет фар, рассеянный туманом: мягкое тёплое пятно впереди машины. Лучи снизу и из кабины почти не видны,
// потому что на них смотришь вдоль, а это пятно видно именно оттуда. Пересвечивать ему нечем: свет мягко гаснет к краям.
Shader "Forest/HeadGlow"
{
    Properties
    {
        _Color ("Color", Color) = (1, 0.8, 0.5, 1)
    }

    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "IgnoreProjector" = "True" }
        Blend One One
        ZWrite Off
        Cull Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            fixed4 _Color;
            UNITY_DECLARE_DEPTH_TEXTURE(_CameraDepthTexture);

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 screen : TEXCOORD1;
            };

            v2f vert(appdata_base v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.texcoord.xy - 0.5;
                o.screen = ComputeScreenPos(o.pos);
                COMPUTE_EYEDEPTH(o.screen.z);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float falloff = saturate(1.0 - length(i.uv * float2(2.0, 2.0)));
                float scene = LinearEyeDepth(SAMPLE_DEPTH_TEXTURE_PROJ(_CameraDepthTexture, UNITY_PROJ_COORD(i.screen)));
                float soft = saturate((scene - i.screen.z) * 0.18);
                return fixed4(_Color.rgb * falloff * falloff * soft, 1.0);
            }
            ENDCG
        }
    }
}
