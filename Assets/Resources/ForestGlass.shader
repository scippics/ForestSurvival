// Стекло кабины: почти прозрачное в упор, отражает небо под углом и покрыто разводами пыли.
Shader "Forest/Glass"
{
    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "IgnoreProjector" = "True" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _ForestNoise;
            float4x4 _ForestVehicleWorldToLocal;
            float _ForestRain;
            float _ForestWiperPhase;
            float _ForestWiperOn;

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 normal : TEXCOORD0;
                float3 view : TEXCOORD1;
                float3 local : TEXCOORD2;
            };

            v2f vert(appdata_base v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                float3 world = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.normal = UnityObjectToWorldNormal(v.normal);
                o.view = _WorldSpaceCameraPos - world;
                o.local = mul(_ForestVehicleWorldToLocal, float4(world, 1.0)).xyz;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float facing = abs(dot(normalize(i.normal), normalize(i.view)));
                float fresnel = pow(1.0 - facing, 3.0);
                float dust = tex2D(_ForestNoise, i.local.xy * 0.9 + i.local.z).g;
                dust = smoothstep(0.55, 0.85, dust) * 0.035;
                float3 color = unity_FogColor.rgb * 1.6;
                float alpha = 0.02 + dust + fresnel * 0.45;

                if (_ForestRain > 0.01)
                {
                    float2 glassUv = float2(i.local.x + i.local.z * 0.6, i.local.y);

                    // Сидящие капли и струйки, которые стекают вниз.
                    float beads = smoothstep(0.8, 0.86, tex2D(_ForestNoise, glassUv * 11.0).b);
                    float runs = smoothstep(0.84, 0.9, tex2D(_ForestNoise, float2(glassUv.x * 7.0, glassUv.y * 0.11 + _Time.y * 0.13)).a);
                    float water = saturate(beads * 0.7 + runs * 0.5) * _ForestRain;

                    // На лобовом стекле дворник стирает воду, и она набирается снова с момента его прохода.
                    if (i.local.z > 0.9 && _ForestWiperOn > 0.5)
                    {
                        float sweep = saturate((atan2(i.local.x, i.local.y - 0.18) / 1.45 + 1.0) * 0.5);
                        float phase = _ForestWiperPhase;
                        float age = phase < 1.0
                            ? (sweep < phase ? phase - sweep : phase + sweep)
                            : (sweep > 2.0 - phase ? sweep - (2.0 - phase) : phase - sweep);
                        water *= saturate(age * 1.1);
                    }

                    alpha += water * 0.13 + _ForestRain * 0.015;
                }

                return fixed4(color, saturate(alpha));
            }
            ENDCG
        }
    }
}
