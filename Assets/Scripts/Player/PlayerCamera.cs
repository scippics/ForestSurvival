using UnityEngine;

// Вешается на камеру, дочернюю к капсуле игрока. Капсула сама не вращается, поэтому
// взгляд обновляется каждый кадр и не привязан к частоте шагов физики.
public class PlayerCamera : MonoBehaviour
{
    const float MaxPitch = 85f;
    const float Sensitivity = 0.1f;
    const float FlashlightRange = 45f;
    const float FlashlightAngle = 55f;
    const float FlashlightIntensity = 5f;

    static readonly Color FlashlightColor = new Color(1f, 0.93f, 0.8f);

    public Light Flashlight { get; private set; }
    public float Yaw { get; private set; }
    public float Pitch { get; private set; }

    PlayerInputReader input;

    void Awake()
    {
        input = GetComponentInParent<PlayerInputReader>();

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

    public void AddYaw(float degrees)
    {
        Yaw = Mathf.Repeat(Yaw + degrees, 360f);
    }

    void Update()
    {
        if (input.ConsumeFlashlight())
            Flashlight.enabled = !Flashlight.enabled;

        Vector2 delta = input.LookDelta * Sensitivity;
        Yaw = Mathf.Repeat(Yaw + delta.x, 360f);
        Pitch = Mathf.Clamp(Pitch - delta.y, -MaxPitch, MaxPitch);
        transform.localRotation = Quaternion.Euler(Pitch, Yaw, 0f);
    }
}
