// Сцена собирается кодом, поэтому на встроенный Standard никто не ссылается и он не попадает
// в сборку. Этот шейдер лежит в Resources и включается всегда.
Shader "Forest/Lit"
{
    Properties
    {
        _Color ("Color", Color) = (1, 1, 1, 1)
        _Smoothness ("Smoothness", Range(0, 1)) = 0.15
        _EmissionColor ("Emission", Color) = (0, 0, 0, 1)
        _Grime ("Vehicle Grime", Range(0, 1)) = 0
        _Detail ("World Detail", Range(0, 1)) = 0
        _Surface ("Interior Surface", Float) = 0
        _MainTex ("Albedo", 2D) = "white" {}
        _EmissionMap ("Emission Map", 2D) = "white" {}
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" }

        CGPROGRAM
        #pragma surface surf Standard vertex:vert fullforwardshadows addshadow
        #pragma target 3.0

        struct Input
        {
            float3 worldPos;
            float3 vehiclePos;
            float2 uv_MainTex;
        };

        sampler2D _ForestNoise;
        sampler2D _MainTex;
        sampler2D _EmissionMap;
        float4x4 _ForestVehicleWorldToLocal;
        float3 _ForestCabinFill;
        fixed4 _Color;
        half _Smoothness;
        fixed4 _EmissionColor;
        half _Grime;
        half _Detail;
        half _Surface;

        void vert(inout appdata_full v, out Input o)
        {
            UNITY_INITIALIZE_OUTPUT(Input, o);
            // Грязь привязана к машине: в мировых координатах она плыла бы по кузову на ходу.
            o.vehiclePos = mul(_ForestVehicleWorldToLocal, mul(unity_ObjectToWorld, v.vertex)).xyz;
        }

        void surf(Input IN, inout SurfaceOutputStandard o)
        {
            float3 albedo = _Color.rgb * tex2D(_MainTex, IN.uv_MainTex).rgb;
            float smoothness = _Smoothness;

            // Материалы мира считаются от самого объекта: волокна идут вдоль бревна как ни поверни.
            // 7 — дерево, 8 — стёганый ватник, 9 — грубая ткань.
            if (_Surface > 6.5)
            {
                float3 q = mul(unity_WorldToObject, float4(IN.worldPos, 1.0)).xyz;
                float seed = unity_ObjectToWorld._m03 * 1.73 + unity_ObjectToWorld._m13 * 3.11 + unity_ObjectToWorld._m23 * 2.37;
                if (_Surface < 7.5)
                {
                    // Волокна идут вдоль самой длинной стороны детали, в метрах: и у бревна, и у доски пола.
                    float3 size = float3(length(unity_ObjectToWorld._m00_m10_m20), length(unity_ObjectToWorld._m01_m11_m21), length(unity_ObjectToWorld._m02_m12_m22));
                    float3 m = q * size;
                    float along = m.y;
                    float across = m.x + m.z;
                    if (size.x > size.y && size.x >= size.z)
                    {
                        along = m.x;
                        across = m.y + m.z;
                    }
                    else if (size.z > size.y)
                    {
                        along = m.z;
                        across = m.x + m.y;
                    }
                    float fibre = tex2D(_ForestNoise, float2(across * 0.8 + seed, along * 0.07 + seed * 0.37)).a;
                    float knots = tex2D(_ForestNoise, float2(across * 0.35 + seed * 0.61, along * 0.2)).g;
                    albedo *= (0.5 + 1.0 * fibre) * (0.7 + 0.6 * knots);
                }
                else if (_Surface < 8.5)
                {
                    float channel = abs(sin(q.y * 44.0));
                    float wear = tex2D(_ForestNoise, q.xy * 1.3 + q.z).g;
                    albedo *= (0.62 + 0.38 * pow(channel, 0.35)) * (0.75 + 0.5 * wear);
                }
                else if (_Surface < 9.5)
                {
                    float cloth = tex2D(_ForestNoise, q.xy * 5.0 + q.z * 3.0).b;
                    float wear = tex2D(_ForestNoise, q.yz * 1.1 + q.x).g;
                    albedo *= (0.78 + 0.44 * cloth) * (0.75 + 0.5 * wear);
                }
                else
                {
                    // 10 — брошенное железо: краска сходит пятнами, а наверху деталей нарос мох.
                    float3 size = float3(length(unity_ObjectToWorld._m00_m10_m20), length(unity_ObjectToWorld._m01_m11_m21), length(unity_ObjectToWorld._m02_m12_m22));
                    float3 m = q * size;
                    float2 flat = m.xy + m.zx * 0.7;
                    float patch = tex2D(_ForestNoise, flat * 0.47 + seed * 0.13).r;
                    float blotch = tex2D(_ForestNoise, flat * 1.3 + seed * 0.29).g;
                    float fine = tex2D(_ForestNoise, flat * 4.3 + seed).b;

                    // Краска выцвела и посерела. Ржавеет только то, что было окрашено: чёрная резина и пластик нет.
                    float luma = dot(albedo, float3(0.3, 0.6, 0.1));
                    float3 faded = lerp(luma.xxx, albedo, 0.55) * (0.62 + 0.46 * blotch) * (0.88 + 0.24 * fine);
                    float rust = smoothstep(0.5, 0.74, patch * 0.62 + blotch * 0.38 + (fine - 0.5) * 0.2) * saturate(luma * 7.0);
                    float3 oxide = lerp(float3(0.075, 0.038, 0.021), float3(0.03, 0.019, 0.014), fine);
                    albedo = lerp(faded, oxide, rust);
                    smoothness *= 1.0 - rust * 0.85;

                    // Мох и грязь только на верхних гранях и едва заметно.
                    float moss = smoothstep(0.42, 0.5, q.y) * smoothstep(0.55, 0.78, blotch) * saturate(luma * 7.0);
                    albedo = lerp(albedo, float3(0.024, 0.036, 0.016) * (0.7 + 0.6 * fine), moss * 0.75);
                    smoothness *= 1.0 - moss;
                }
            }
            // Материалы салона: 1 — ткань, 2 — пластик с шагренью, 3 — резина.
            else if (_Surface > 0.5)
            {
                float3 p = IN.vehiclePos;
                float wear = tex2D(_ForestNoise, p.xz * 0.9 + p.y * 1.3).g;
                float grain = tex2D(_ForestNoise, (p.xy + p.zx) * 4.5).b;
                if (_Surface < 1.5)
                {
                    // Ткань из текстуры шума: вдали её сглаживают мипмапы, а рисунок из синусов бился с пикселями в муар.
                    float cloth = tex2D(_ForestNoise, (p.xy + p.zx) * 21.0).b;
                    albedo *= (0.97 + 0.06 * cloth) * (0.92 + 0.16 * wear);
                }
                else if (_Surface < 2.5)
                {
                    albedo *= (0.93 + 0.14 * grain) * (0.92 + 0.16 * wear);
                    smoothness *= 0.85 + 0.3 * grain;
                }
                else if (_Surface < 3.5)
                {
                    albedo *= 0.88 + 0.24 * grain;
                }
            }

            if (_Detail > 0)
            {
                float2 uv = float2(IN.worldPos.x + IN.worldPos.z, IN.worldPos.y * 0.4);
                float fibre = tex2D(_ForestNoise, uv * 0.9).a;
                float blotch = tex2D(_ForestNoise, (IN.worldPos.xz + IN.worldPos.y) * 0.21).g;
                albedo *= lerp(1.0, (0.55 + 0.9 * fibre) * (0.7 + 0.6 * blotch), _Detail);
            }

            if (_Grime > 0)
            {
                float3 p = IN.vehiclePos;
                float blotch = tex2D(_ForestNoise, p.xz * 0.5 + p.y * 0.5).g;
                float fine = tex2D(_ForestNoise, p.zy * 2.3 + p.x).b;

                // Снизу кузов забрызган, выше остаются только потёки и выгоревшая краска.
                float mud = saturate((-0.12 - p.y) * 2.2 + (blotch - 0.5) * 1.1 + (fine - 0.5) * 0.7) * _Grime;
                albedo *= lerp(1.0, 0.72 + 0.56 * blotch, _Grime);

                // Сколы и ржавчина: на старой машине краска сходит пятнами. Царапин нет: длинные штрихи читались как полосы.
                float patch = tex2D(_ForestNoise, p.zy * 0.8 + p.x * 0.7 + 0.37).r;
                float rust = smoothstep(0.7, 0.8, patch + (0.02 - p.y) * 0.12) * smoothstep(0.35, 0.55, fine) * _Grime;

                // На чёрных деталях ржавчины не видно: они пластиковые или в краске-грунте.
                rust *= saturate(dot(albedo, float3(0.3, 0.6, 0.1)) * 30.0);
                albedo = lerp(albedo, float3(0.05, 0.024, 0.012) * (0.6 + 0.8 * fine), rust * 0.8);
                smoothness *= 1.0 - rust;
                albedo = lerp(albedo, float3(0.016, 0.012, 0.009), mud);
                smoothness *= (1.0 - mud) * lerp(1.0, 0.5 + fine, _Grime);
            }

            // Рассеянный свет неба не знает про крышу и залил бы кабину серым: внутри он слабее, а у пола почти гаснет.
            bool cabin = _Surface > 0.5 && _Surface < 6.5;
            if (cabin && dot(IN.vehiclePos, IN.vehiclePos) < 16.0)
                albedo *= lerp(0.42, 0.7, saturate((IN.vehiclePos.y - 0.05) / 0.75));

            o.Albedo = albedo;
            o.Smoothness = smoothness;
            o.Emission = _EmissionColor.rgb * tex2D(_EmissionMap, IN.uv_MainTex).rgb;

            // В закрытой кабине тени чёрные, хотя свет идёт через стёкла: это рассеянная подсветка от них.
            if (cabin)
                o.Emission += albedo * _ForestCabinFill;
            o.Alpha = 1;
        }
        ENDCG
    }
}
