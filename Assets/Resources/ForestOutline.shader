// Лёгкая подсветка предмета под прицелом: оболочка чуть больше самой детали, видна только по её краю.
Shader "Forest/Outline"
{
    Properties
    {
        _Color ("Color", Color) = (0.95, 0.9, 0.72, 0.6)
        _Width ("Width per meter", Float) = 0.005
    }
    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "IgnoreProjector" = "True" }
        Pass
        {
            Cull Front
            ZWrite Off
            Blend SrcAlpha OneMinusSrcAlpha

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            fixed4 _Color;
            float _Width;

            float4 vert(float4 vertex : POSITION, float3 normal : NORMAL) : SV_POSITION
            {
                // Ширина растёт с расстоянием: на экране кайма всегда одной толщины.
                float3 world = mul(unity_ObjectToWorld, vertex).xyz;
                world += UnityObjectToWorldNormal(normal) * _Width * distance(_WorldSpaceCameraPos, world);
                return UnityWorldToClipPos(world);
            }

            fixed4 frag() : SV_Target
            {
                return _Color;
            }
            ENDCG
        }
    }
}
