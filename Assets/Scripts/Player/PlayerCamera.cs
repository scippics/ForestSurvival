using UnityEngine;

// Вешается на камеру, дочернюю к капсуле игрока. Капсула сама не вращается, поэтому
// взгляд обновляется каждый кадр и не привязан к частоте шагов физики.
public class PlayerCamera : MonoBehaviour
{
    const float MaxPitch = 85f;
    const float Sensitivity = 0.1f;
    const float SeatedMaxYaw = 120f;
    const float SeatedMaxPitchUp = 35f;
    const float SeatedMaxPitchDown = 60f;
    const float FlashlightRange = 45f;
    const float FlashlightAngle = 55f;
    const float FlashlightIntensity = 3.2f;

    static readonly Color FlashlightColor = new Color(0.78f, 0.87f, 1f);

    // Покачивание: фаза идёт от пройденного пути, поэтому шаг совпадает со скоростью.
    // С грузом шаг реже, а голова ходит сильнее.
    const float BobPhasePerMeter = 3.2f;
    const float BobPhasePerMeterBurdened = 2.6f;
    const float BobHeight = 0.035f;
    const float BobHeightBurdened = 0.055f;
    const float BobSideRatio = 0.5f;
    const float BobFadeSpeed = 8f;

    public bool IsSeated { get; private set; }
    public Light Flashlight { get; private set; }
    public float Yaw { get; private set; }
    public float Pitch { get; private set; }

    PlayerInputReader input;
    PlayerMovement movement;
    Vector3 restPosition;
    float bobPhase;
    float bobAmount;

    void Awake()
    {
        input = GetComponentInParent<PlayerInputReader>();
        restPosition = transform.localPosition;

        var lightObject = new GameObject("Flashlight");
        lightObject.transform.SetParent(transform, false);
        lightObject.transform.localPosition = new Vector3(0.2f, -0.2f, 0.1f);

        Flashlight = lightObject.AddComponent<Light>();
        Flashlight.type = LightType.Spot;
        Flashlight.range = FlashlightRange;
        Flashlight.spotAngle = FlashlightAngle;
        Flashlight.intensity = FlashlightIntensity;
        Flashlight.color = FlashlightColor;
        Flashlight.renderMode = LightRenderMode.ForcePixel;
        Flashlight.enabled = false;
        ForestLights.Register(Flashlight);
    }

    // Движение создаётся после камеры, поэтому его нельзя получить в Awake.
    void Start()
    {
        movement = GetComponentInParent<PlayerMovement>();
    }

    void UpdateHeadBob()
    {
        float speed = movement.GroundSpeed;
        bool burdened = movement.IsBurdened;

        bobPhase = Mathf.Repeat(bobPhase + speed * (burdened ? BobPhasePerMeterBurdened : BobPhasePerMeter) * Time.deltaTime, Mathf.PI * 4f);

        float height = burdened ? BobHeightBurdened : BobHeight;
        float target = height * Mathf.Clamp01(speed / (PlayerMovement.NormalWalkSpeed * 0.7f));
        bobAmount = Mathf.MoveTowards(bobAmount, target, BobFadeSpeed * height * Time.deltaTime);

        // Вверх-вниз за каждый шаг, вбок вдвое реже: вес переносится с ноги на ногу.
        transform.localPosition = restPosition + new Vector3(
            Mathf.Sin(bobPhase * 0.5f) * bobAmount * BobSideRatio,
            (Mathf.Cos(bobPhase) - 1f) * 0.5f * bobAmount,
            0f);
    }

    public void AddYaw(float degrees)
    {
        Yaw = Mathf.Repeat(Yaw + degrees, 360f);
    }

    // За рулём взгляд считается от машины: садимся лицом вперёд и крутим головой в пределах шеи.
    public void EnterSeat()
    {
        IsSeated = true;
        Yaw = 0f;
        Pitch = 0f;
    }

    public void SetLook(float yaw, float pitch)
    {
        Yaw = yaw;
        Pitch = pitch;
        transform.localRotation = Quaternion.Euler(Pitch, Yaw, 0f);
    }

    // Меню водит камеру само; перед игрой она возвращается в голову игрока.
    public void ResetToBody()
    {
        transform.localPosition = restPosition;
        transform.localRotation = Quaternion.Euler(Pitch, Yaw, 0f);
    }

    public void ExitSeat(float vehicleYaw)
    {
        IsSeated = false;
        AddYaw(vehicleYaw);
    }

    void Update()
    {
        if (input.ConsumeFlashlight())
            Flashlight.enabled = !Flashlight.enabled;

        Vector2 delta = input.LookDelta * Sensitivity;
        if (IsSeated)
        {
            Yaw = Mathf.Clamp(Yaw + delta.x, -SeatedMaxYaw, SeatedMaxYaw);
            Pitch = Mathf.Clamp(Pitch - delta.y, -SeatedMaxPitchUp, SeatedMaxPitchDown);
        }
        else
        {
            Yaw = Mathf.Repeat(Yaw + delta.x, 360f);
            Pitch = Mathf.Clamp(Pitch - delta.y, -MaxPitch, MaxPitch);
        }
        transform.localRotation = Quaternion.Euler(Pitch, Yaw, 0f);

        UpdateHeadBob();
    }
}
