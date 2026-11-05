using UnityEngine;

// Постобработка на камере. Затенение и лучи считаются в уменьшенном разрешении, поэтому
// почти не зависят от размера окна. Сами формулы в Resources/ForestPost.shader.
[RequireComponent(typeof(Camera))]
public class ForestPostProcess : MonoBehaviour
{
    const int PassAo = 0;
    const int PassAoBlur = 1;
    const int PassBloomPrefilter = 2;
    const int PassBloomDown = 3;
    const int PassBloomUp = 4;
    const int PassRayMask = 5;
    const int PassRayBlur = 6;
    const int PassComposite = 7;

    const int MaxBloomLevels = 4;
    const float AoRadius = 1.3f;
    const float AoStrength = 2.6f;
    const float BloomThreshold = 1f;
    const float BloomStrength = 0.32f;
    const float RayStepFirst = 0.065f;
    const float RayStepSecond = 0.022f;
    const float RayStrength = 0.28f;

    // На сколько солнце может уйти за край кадра (доля экрана), пока лучи плавно гаснут.
    const float RayFadeStart = 0.15f;
    const float RayFadeEnd = 0.8f;
    const float Exposure = 1.25f;
    const float Vignette = 1.15f;

    static readonly int AoTexId = Shader.PropertyToID("_AoTex");
    static readonly int BloomTexId = Shader.PropertyToID("_BloomTex");
    static readonly int RayTexId = Shader.PropertyToID("_RayTex");
    static readonly int AoDirectionId = Shader.PropertyToID("_AoDirection");
    static readonly int AoRadiusId = Shader.PropertyToID("_AoRadius");
    static readonly int AoStrengthId = Shader.PropertyToID("_AoStrength");
    static readonly int BloomThresholdId = Shader.PropertyToID("_BloomThreshold");
    static readonly int BloomStrengthId = Shader.PropertyToID("_BloomStrength");
    static readonly int SunViewportId = Shader.PropertyToID("_SunViewport");
    static readonly int RayStepId = Shader.PropertyToID("_RayStep");
    static readonly int RayStrengthId = Shader.PropertyToID("_RayStrength");
    static readonly int RayColorId = Shader.PropertyToID("_RayColor");
    static readonly int ExposureId = Shader.PropertyToID("_Exposure");
    static readonly int FogDensityId = Shader.PropertyToID("_FogDensity");
    static readonly int GrainTimeId = Shader.PropertyToID("_GrainTime");
    static readonly int VignetteId = Shader.PropertyToID("_VignetteStrength");

    Camera view;
    Light sun;
    Material material;
    readonly RenderTexture[] bloomChain = new RenderTexture[MaxBloomLevels];

    void Awake()
    {
        view = GetComponent<Camera>();
        view.allowHDR = true;
        view.allowMSAA = true;
        view.depthTextureMode |= DepthTextureMode.Depth;
        material = new Material(Resources.Load<Shader>("ForestPost"));
        sun = FindAnyObjectByType<DayNightCycle>().GetComponent<Light>();

        material.SetFloat(AoRadiusId, AoRadius);
        material.SetFloat(AoStrengthId, AoStrength);
        material.SetFloat(BloomThresholdId, BloomThreshold);
        material.SetFloat(BloomStrengthId, BloomStrength);
        material.SetFloat(ExposureId, Exposure);
        material.SetFloat(VignetteId, Vignette);
    }

    void OnRenderImage(RenderTexture source, RenderTexture destination)
    {
        // На низком качестве нет затенения углов, на низком и среднем нет лучей солнца:
        // проход пропускается, а в сборку идёт текстура, которая ничего не меняет.
        RenderTexture ao = GameSettings.AmbientOcclusion ? RenderAo(source) : Blank(Texture2D.whiteTexture);
        RenderTexture bloom = RenderBloom(source);
        RenderTexture rays = GameSettings.SunRays ? RenderRays(source) : Blank(Texture2D.blackTexture);
        if (!GameSettings.SunRays)
            material.SetFloat(RayStrengthId, 0f);

        material.SetFloat(FogDensityId, RenderSettings.fogDensity);
        material.SetFloat(GrainTimeId, Time.unscaledTime % 97f * 61f);
        material.SetTexture(AoTexId, ao);
        material.SetTexture(BloomTexId, bloom);
        material.SetTexture(RayTexId, rays);
        Graphics.Blit(source, destination, material, PassComposite);

        RenderTexture.ReleaseTemporary(ao);
        RenderTexture.ReleaseTemporary(bloom);
        RenderTexture.ReleaseTemporary(rays);
    }

    static RenderTexture Blank(Texture fill)
    {
        RenderTexture blank = RenderTexture.GetTemporary(4, 4, 0, RenderTextureFormat.ARGBHalf);
        Graphics.Blit(fill, blank);
        return blank;
    }

    RenderTexture RenderAo(RenderTexture source)
    {
        int width = source.width / 2;
        int height = source.height / 2;
        RenderTexture first = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.RGHalf);
        RenderTexture second = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.RGHalf);
        first.filterMode = FilterMode.Bilinear;
        second.filterMode = FilterMode.Bilinear;

        Graphics.Blit(source, first, material, PassAo);
        material.SetVector(AoDirectionId, new Vector2(1f / width, 0f));
        Graphics.Blit(first, second, material, PassAoBlur);
        material.SetVector(AoDirectionId, new Vector2(0f, 1f / height));
        Graphics.Blit(second, first, material, PassAoBlur);

        RenderTexture.ReleaseTemporary(second);
        return first;
    }

    RenderTexture RenderBloom(RenderTexture source)
    {
        int width = source.width / 2;
        int height = source.height / 2;
        RenderTextureFormat format = source.format;

        int levels = Mathf.Clamp(GameSettings.BloomLevels, 1, MaxBloomLevels);
        bloomChain[0] = RenderTexture.GetTemporary(width, height, 0, format);
        Graphics.Blit(source, bloomChain[0], material, PassBloomPrefilter);
        for (int level = 1; level < levels; level++)
        {
            width = Mathf.Max(width / 2, 2);
            height = Mathf.Max(height / 2, 2);
            bloomChain[level] = RenderTexture.GetTemporary(width, height, 0, format);
            Graphics.Blit(bloomChain[level - 1], bloomChain[level], material, PassBloomDown);
        }

        for (int level = levels - 1; level > 0; level--)
        {
            Graphics.Blit(bloomChain[level], bloomChain[level - 1], material, PassBloomUp);
            RenderTexture.ReleaseTemporary(bloomChain[level]);
        }
        return bloomChain[0];
    }

    RenderTexture RenderRays(RenderTexture source)
    {
        int width = source.width / 4;
        int height = source.height / 4;
        RenderTexture first = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.RHalf);
        first.filterMode = FilterMode.Bilinear;

        Vector3 sunPoint = view.WorldToViewportPoint(view.transform.position - sun.transform.forward * view.farClipPlane);
        if (!sun.enabled || sunPoint.z <= 0f)
        {
            Graphics.Blit(Texture2D.blackTexture, first);
            material.SetFloat(RayStrengthId, 0f);
            return first;
        }

        RenderTexture second = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.RHalf);
        second.filterMode = FilterMode.Bilinear;

        // Чем дальше солнце за краем кадра, тем слабее лучи: иначе они идут почти параллельно
        // и при повороте камеры вспыхивают и обрываются.
        float outside = Mathf.Max(Mathf.Abs(sunPoint.x - 0.5f), Mathf.Abs(sunPoint.y - 0.5f)) - 0.5f;
        float fade = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(RayFadeStart, RayFadeEnd, outside));

        material.SetVector(SunViewportId, sunPoint);
        material.SetFloat(RayStrengthId, RayStrength * fade);
        material.SetColor(RayColorId, sun.color * sun.intensity);

        Graphics.Blit(source, first, material, PassRayMask);
        material.SetFloat(RayStepId, RayStepFirst);
        Graphics.Blit(first, second, material, PassRayBlur);
        material.SetFloat(RayStepId, RayStepSecond);
        Graphics.Blit(second, first, material, PassRayBlur);

        RenderTexture.ReleaseTemporary(second);
        return first;
    }
}
