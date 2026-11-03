// Следы на мокрой земле: затемняют то, что уже нарисовано, поэтому не спорят с освещением и лужами.
// Красный канал цвета вершин — глубина следа, зелёный — вид: 0 колесо, 1 сапог. Туман следы растворяет.
Shader "Forest/Track"
{
    SubShader
    {
        Tags { "Queue" = "Geometry+60" "RenderType" = "Transparent" "IgnoreProjector" = "True" }
        Blend DstColor Zero
        ZWrite Off
        Cull Off
        Offset -2, -2

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            float _ForestRain;

            struct appdata
            {
                float4 vertex : POSITION;
                fixed4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                fixed4 color : COLOR;
                float2 uv : TEXCOORD0;
                float depth : TEXCOORD1;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.color = v.color;
                o.uv = v.uv;
                COMPUTE_EYEDEPTH(o.depth);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float strength = i.color.r;
                float shape;
                if (i.color.g > 0.5)
                {
                    // Отпечаток сапога: подошва и каблук, поперёк редкие грунтозацепы.
                    float2 p = (i.uv - 0.5) * float2(2.0, 2.0);
                    float sole = smoothstep(1.0, 0.8, length(float2(p.x * 1.3, (p.y - 0.28) * 1.5)));
                    float heel = smoothstep(1.0, 0.8, length(float2(p.x * 1.5, (p.y + 0.62) * 3.0)));
                    float lugs = 0.72 + 0.28 * step(0.5, frac(i.uv.y * 7.0));
                    shape = max(sole, heel) * lugs;
                }
                else
                {
                    // Протектор: ёлочка, а края колеи мягче середины.
                    float across = abs(i.uv.x - 0.5) * 2.0;
                    float tread = 0.68 + 0.32 * step(0.5, frac(i.uv.y * 3.0 + across * 0.9));
                    shape = smoothstep(1.0, 0.72, across) * tread;
                }

                float fog = exp(-pow(i.depth * unity_FogParams.x, 2.0));
                float dark = saturate(shape * strength * fog);
                return fixed4(lerp(1.0.xxx, float3(0.46, 0.43, 0.4), dark), 1.0);
            }
            ENDCG
        }
    }
}
