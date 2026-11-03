using UnityEngine;

// Вода на лобовом стекле: хранится в текстуре, поэтому щётка стирает только то, где прошла,
// а без дворников стекло заливает. На ходу встречный поток гонит капли вверх, а не вниз.
public class WindshieldWater : MonoBehaviour
{
    const int MaskWidth = 384;
    const int MaskHeight = 160;
    const float FillPerSecond = 0.085f;
    const float FillPerMeterPerSecond = 0.006f;
    const float DryPerSecond = 0.016f;
    const float MinChange = 0.008f;
    const float RunGravity = 0.13f;
    const float RunAirflow = 0.013f;

    static readonly int MaskId = Shader.PropertyToID("_ForestWindshieldWater");
    static readonly int RunId = Shader.PropertyToID("_ForestGlassRun");
    static readonly int PivotsId = Shader.PropertyToID("_Pivots");
    static readonly int SweepId = Shader.PropertyToID("_Sweep");
    static readonly int ChangeId = Shader.PropertyToID("_Change");

    public RenderTexture Mask => current;

    RenderTexture current;
    RenderTexture next;
    Material material;
    float run;
    float pendingChange;

    void Awake()
    {
        material = new Material(Resources.Load<Shader>("ForestWiperMask"));
        current = CreateMask();
        next = CreateMask();
        Shader.SetGlobalTexture(MaskId, current);
    }

    static RenderTexture CreateMask()
    {
        var mask = new RenderTexture(MaskWidth, MaskHeight, 0, RenderTextureFormat.RHalf, RenderTextureReadWrite.Linear)
        {
            name = "WindshieldWater",
            wrapMode = TextureWrapMode.Clamp
        };
        mask.Create();

        RenderTexture active = RenderTexture.active;
        RenderTexture.active = mask;
        GL.Clear(false, true, Color.clear);
        RenderTexture.active = active;
        return mask;
    }

    // Углы щёток в градусах, как у шарниров: ноль смотрит вверх, плюс — к левому краю стекла.
    public void Step(float dt, float airSpeed, float angleBefore, float angleNow)
    {
        if (dt <= 0f)
            return;

        float rain = ForestWeather.Rain;
        pendingChange += rain > 0.02f
            ? (FillPerSecond + Mathf.Abs(airSpeed) * FillPerMeterPerSecond) * rain * dt
            : -DryPerSecond * dt;

        // В половинной точности прибавка за один кадр теряется, поэтому вода копится порциями.
        float change = 0f;
        if (Mathf.Abs(pendingChange) >= MinChange)
        {
            change = pendingChange;
            pendingChange = 0f;
        }

        run += (RunGravity - Mathf.Max(airSpeed, 0f) * RunAirflow) * dt;
        Shader.SetGlobalFloat(RunId, run);

        Vector3 left = VehicleExterior.Wipers[0].localPosition;
        Vector3 right = VehicleExterior.Wipers[1].localPosition;
        material.SetVector(PivotsId, new Vector4(left.x, left.y, right.x, right.y));
        material.SetVector(SweepId, new Vector4(
            Mathf.Min(angleBefore, angleNow) * Mathf.Deg2Rad,
            Mathf.Max(angleBefore, angleNow) * Mathf.Deg2Rad,
            VehicleExterior.BladeInner,
            VehicleExterior.BladeOuter));
        material.SetFloat(ChangeId, change);

        // Щётка в покое лежит на месте и ничего не стирает.
        if (Mathf.Approximately(angleBefore, angleNow))
            material.SetVector(SweepId, new Vector4(10f, 10f, VehicleExterior.BladeInner, VehicleExterior.BladeOuter));

        Graphics.Blit(current, next, material);
        (current, next) = (next, current);
        Shader.SetGlobalTexture(MaskId, current);
    }

    void OnDestroy()
    {
        current.Release();
        next.Release();
        Destroy(material);
    }
}
