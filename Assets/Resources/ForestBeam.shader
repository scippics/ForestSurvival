// Луч фары в тумане. Конус рисуется сложением цвета; у земли и деревьев он гаснет по глубине,
// иначе пересечение с ними резало бы луч прямой линией. Красный канал вершин — спад по длине.
Shader "Forest/Beam"
{
    Properties
    {
        _Color ("Color", Color) = (1, 1, 1, 1)
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

            UNITY_DECLARE_DEPTH_TEXTURE(_CameraDepthTexture);
            fixed4 _Color;

            struct v2f
            {
                float4 pos : SV_POSITION;
                float fade : TEXCOORD0;
                float3 normal : TEXCOORD1;
                float3 view : TEXCOORD2;
                float4 screen : TEXCOORD3;
            };

            v2f vert(appdata_full v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.fade = v.color.r;
                o.normal = UnityObjectToWorldNormal(v.normal);
                o.view = WorldSpaceViewDir(v.vertex);
                o.screen = ComputeScreenPos(o.pos);
                COMPUTE_EYEDEPTH(o.screen.z);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                // Сбоку луч плотнее в середине, а края конуса растворяются.
                float edge = abs(dot(normalize(i.normal), normalize(i.view)));
                float scene = LinearEyeDepth(SAMPLE_DEPTH_TEXTURE_PROJ(_CameraDepthTexture, UNITY_PROJ_COORD(i.screen)));
                float soft = saturate((scene - i.screen.z) * 0.7);
                return fixed4(_Color.rgb * i.fade * edge * edge * soft, 1.0);
            }
            ENDCG
        }
    }
}
