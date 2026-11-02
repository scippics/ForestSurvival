using System.Collections.Generic;
using UnityEngine;

// Передаёт включённые фонарики и фары в шейдер растительности (ForestDynamicLights.cginc).
public static class ForestLights
{
    const int MaxLights = 8;

    static readonly int CountId = Shader.PropertyToID("_ForestLightCount");
    static readonly int PositionId = Shader.PropertyToID("_ForestLightPosition");
    static readonly int DirectionId = Shader.PropertyToID("_ForestLightDirection");
    static readonly int ColorId = Shader.PropertyToID("_ForestLightColor");

    static readonly List<Light> Lights = new List<Light>();
    static readonly Vector4[] Positions = new Vector4[MaxLights];
    static readonly Vector4[] Directions = new Vector4[MaxLights];
    static readonly Vector4[] Colors = new Vector4[MaxLights];

    public static void Register(Light forestLight)
    {
        Lights.Add(forestLight);
    }

    public static void Push()
    {
        int count = 0;
        foreach (Light forestLight in Lights)
        {
            if (count == MaxLights)
                break;
            if (forestLight == null || !forestLight.enabled)
                continue;

            bool isSpot = forestLight.type == LightType.Spot;
            Transform source = forestLight.transform;
            Vector4 position = source.position;
            position.w = forestLight.range;
            Vector4 direction = source.forward;
            direction.w = isSpot ? Mathf.Cos(forestLight.spotAngle * 0.5f * Mathf.Deg2Rad) : -2f;
            Vector4 color = forestLight.color * forestLight.intensity;
            color.w = isSpot ? Mathf.Cos(forestLight.spotAngle * 0.3f * Mathf.Deg2Rad) : -1f;

            Positions[count] = position;
            Directions[count] = direction;
            Colors[count] = color;
            count++;
        }

        Shader.SetGlobalInt(CountId, count);
        Shader.SetGlobalVectorArray(PositionId, Positions);
        Shader.SetGlobalVectorArray(DirectionId, Directions);
        Shader.SetGlobalVectorArray(ColorId, Colors);
    }
}
