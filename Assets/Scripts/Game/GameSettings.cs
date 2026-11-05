using UnityEngine;

// Настройки игрока: хранятся в PlayerPrefs и применяются сразу при изменении.
public static class GameSettings
{
    public static readonly string[] QualityNames = { "НИЗКОЕ", "СРЕДНЕЕ", "ВЫСОКОЕ" };
    public static readonly string[] FrameLimitNames = { "30", "60", "БЕЗ ПРЕДЕЛА" };
    static readonly int[] FrameLimits = { 30, 60, -1 };

    const string SensitivityKey = "sensitivity";
    const string VolumeKey = "volume";
    const string QualityKey = "quality";
    const string FrameLimitKey = "frameLimit";
    const int DefaultFrameLimit = 1;
    const int MaxRefreshesPerFrame = 4;
    const float DefaultSensitivity = 0.1f;
    const float DefaultVolume = 0.85f;
    const int HighQuality = 2;
    const int MediumQuality = 1;

    // Ноутбук без вентилятора не должен перегреваться на настройках по умолчанию.
    static int DefaultQuality => Application.platform == RuntimePlatform.OSXPlayer ? MediumQuality : HighQuality;

    static float sensitivity = -1f;

    public const float MinSensitivity = 0.02f;
    public const float MaxSensitivity = 0.4f;

    public static float Sensitivity
    {
        get
        {
            if (sensitivity < 0f)
                sensitivity = PlayerPrefs.GetFloat(SensitivityKey, DefaultSensitivity);
            return sensitivity;
        }
        set
        {
            sensitivity = value;
            PlayerPrefs.SetFloat(SensitivityKey, value);
        }
    }

    public static float Volume
    {
        get => PlayerPrefs.GetFloat(VolumeKey, DefaultVolume);
        set
        {
            PlayerPrefs.SetFloat(VolumeKey, value);
            AudioListener.volume = value;
        }
    }

    public static int Quality
    {
        get => PlayerPrefs.GetInt(QualityKey, DefaultQuality);
        set
        {
            PlayerPrefs.SetInt(QualityKey, value);
            ApplyQuality(value);
        }
    }

    public static int FrameLimit
    {
        get => PlayerPrefs.GetInt(FrameLimitKey, DefaultFrameLimit);
        set
        {
            PlayerPrefs.SetInt(FrameLimitKey, value);
            ApplyFrameLimit(value);
        }
    }

    public static void Apply()
    {
        AudioListener.volume = Volume;
        ApplyQuality(Quality);
        ApplyFrameLimit(FrameLimit);
    }

    // Кадр всегда ждёт обновления экрана: иначе картинка рвётся горизонтальной линией, а движение дёргается.
    // Предел задаётся через целое число обновлений на кадр, поэтому темп остаётся ровным на любом экране.
    static void ApplyFrameLimit(int index)
    {
        int limit = FrameLimits[Mathf.Clamp(index, 0, FrameLimits.Length - 1)];
        double refresh = Screen.currentResolution.refreshRateRatio.value;
        int perFrame = limit > 0 && refresh > 1.0 ? Mathf.Clamp(Mathf.RoundToInt((float)(refresh / limit)), 1, MaxRefreshesPerFrame) : 1;
        QualitySettings.vSyncCount = perFrame;
        Application.targetFrameRate = -1;
    }

    // Доля травы от полной густоты и включённые эффекты постобработки. Лес и камера читают их каждый кадр,
    // поэтому смена качества видна сразу, без перезапуска.
    public static float GrassDensity { get; private set; } = 1f;
    public static bool AmbientOcclusion { get; private set; } = true;
    public static bool SunRays { get; private set; } = true;
    public static int BloomLevels { get; private set; } = 4;

    static void ApplyQuality(int level)
    {
        level = Mathf.Clamp(level, 0, QualityNames.Length - 1);
        QualitySettings.shadows = level == 0 ? ShadowQuality.HardOnly : ShadowQuality.All;
        QualitySettings.shadowResolution = level == 0 ? ShadowResolution.Low : level == 1 ? ShadowResolution.High : ShadowResolution.VeryHigh;
        QualitySettings.shadowCascades = level == 0 ? 1 : level == 1 ? 2 : 4;
        QualitySettings.shadowDistance = level == 0 ? 18f : level == 1 ? 32f : 50f;
        QualitySettings.pixelLightCount = level == 0 ? 2 : level == 1 ? 4 : 6;
        QualitySettings.antiAliasing = level == 0 ? 0 : level == 1 ? 2 : 8;
        QualitySettings.globalTextureMipmapLimit = level == 0 ? 1 : 0;
        QualitySettings.anisotropicFiltering = level == 0 ? AnisotropicFiltering.Disable : AnisotropicFiltering.ForceEnable;
        QualitySettings.lodBias = level == 0 ? 0.6f : level == 1 ? 1f : 1.6f;

        GrassDensity = level == 0 ? 0.45f : level == 1 ? 0.75f : 1f;
        AmbientOcclusion = level > 0;
        SunRays = level == 2;
        BloomLevels = level == 0 ? 2 : 4;
    }
}
