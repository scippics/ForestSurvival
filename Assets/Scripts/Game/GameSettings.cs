using UnityEngine;

// Настройки игрока: хранятся в PlayerPrefs и применяются сразу при изменении.
public static class GameSettings
{
    public static readonly string[] QualityNames = { "НИЗКОЕ", "СРЕДНЕЕ", "ВЫСОКОЕ" };

    const string SensitivityKey = "sensitivity";
    const string VolumeKey = "volume";
    const string QualityKey = "quality";
    const float DefaultSensitivity = 0.1f;
    const float DefaultVolume = 0.85f;
    const int HighQuality = 2;

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
        get => PlayerPrefs.GetInt(QualityKey, HighQuality);
        set
        {
            PlayerPrefs.SetInt(QualityKey, value);
            ApplyQuality(value);
        }
    }

    public static void Apply()
    {
        AudioListener.volume = Volume;
        ApplyQuality(Quality);
    }

    static void ApplyQuality(int level)
    {
        QualitySettings.shadows = ShadowQuality.All;
        QualitySettings.shadowResolution = level == 0 ? ShadowResolution.Medium : level == 1 ? ShadowResolution.High : ShadowResolution.VeryHigh;
        QualitySettings.shadowCascades = level == 0 ? 2 : 4;
        QualitySettings.shadowDistance = level == 0 ? 28f : level == 1 ? 36f : 45f;
        QualitySettings.pixelLightCount = level == 0 ? 3 : level == 1 ? 4 : 6;
        QualitySettings.antiAliasing = level == 0 ? 0 : level == 1 ? 2 : 4;
    }
}
