using UnityEngine;

// Заряд фонарика: тратится, только пока он горит. Садящиеся батарейки дают мигающий тусклый свет.
public class TorchCharge : MonoBehaviour
{
    const float SecondsOnFull = 540f;
    const float LowCharge = 0.18f;
    const float RefillAmount = 0.65f;

    public float Charge { get; private set; } = 1f;

    PlayerCamera view;
    float baseIntensity;

    void Start()
    {
        view = GetComponentInChildren<PlayerCamera>();
        baseIntensity = view.Flashlight.intensity;
    }

    public void SetCharge(float charge)
    {
        Charge = Mathf.Clamp01(charge);
    }

    // Сухими батарейками фонарик не зажечь: щелчок есть, а света нет.
    public void Toggle()
    {
        Light torch = view.Flashlight;
        ForestAudio.Play(ForestSound.Click, 0.35f);
        if (!torch.enabled && Charge <= 0f)
            return;
        torch.enabled = !torch.enabled;
    }

    public void Refill()
    {
        Charge = Mathf.Min(1f, Charge + RefillAmount);
    }

    void Update()
    {
        Light torch = view.Flashlight;
        if (!torch.enabled)
            return;

        if (GameFlow.State == GameState.Playing)
            Charge = Mathf.Max(0f, Charge - Time.deltaTime / SecondsOnFull);

        if (Charge <= 0f)
        {
            torch.enabled = false;
            ForestAudio.Play(ForestSound.Click, 0.2f);
            return;
        }

        float level = 1f;
        if (Charge < LowCharge)
        {
            float low = Charge / LowCharge;
            float dip = Mathf.PerlinNoise(Time.time * 9f, 2.3f);
            level = Mathf.Lerp(0.2f, 1f, low) * (dip < 0.28f ? 0.25f : 1f);
        }
        torch.intensity = baseIntensity * level;
    }
}

// Пара батареек в бумажной обёртке: берётся одним кликом и сразу идёт в фонарик.
public class CellPack : MonoBehaviour
{
}
