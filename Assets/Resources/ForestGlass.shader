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
                return fixed4(color, saturate(0.02 + dust + fresnel * 0.45));
            }
            ENDCG
        }
    }
}
