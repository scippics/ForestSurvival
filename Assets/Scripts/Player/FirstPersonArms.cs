using UnityEngine;

// Руки в кадре: тянутся к тому, что игрок держит, а правая с включённым фонариком поднята вперёд.
// Своё тело игрок видит только так: манекен рисует лишь тень.
public class FirstPersonArms : MonoBehaviour
{
    const float Reach = 0.72f;
    const float HandSpread = 0.15f;
    const float Response = 13f;
    const float HiddenDrop = 0.55f;
    const float WrenchSwingSpeed = 9f;
    static readonly Vector3 GripBelowItem = new Vector3(0f, -0.1f, -0.03f);

    static readonly Vector3 LeftShoulder = new Vector3(-0.3f, -0.42f, -0.05f);
    static readonly Vector3 RightShoulder = new Vector3(0.3f, -0.42f, -0.05f);
    static readonly Vector3 TorchHand = new Vector3(0.2f, -0.22f, 0.42f);
    const float CabinArmShade = 0.7f;
    static readonly Color SleeveColor = new Color(0.11f, 0.115f, 0.09f);
    static readonly Color GloveColor = new Color(0.16f, 0.12f, 0.08f);
    static readonly Color TorchColor = new Color(0.09f, 0.09f, 0.08f);

    class Arm
    {
        public Transform Sleeve;
        public Transform Hand;
        public Vector3 Shoulder;
        public Vector3 Current;
    }

    PlayerHands hands;
    PlayerSeat seat;
    PlayerCamera view;
    PlayerWrench wrench;
    Transform wrenchModel;
    Arm left;
    Arm right;
    Transform torch;
    readonly System.Collections.Generic.List<Material> cloths = new System.Collections.Generic.List<Material>();
    readonly System.Collections.Generic.List<Material> gloves = new System.Collections.Generic.List<Material>();
    bool shaded;

    public bool LeftVisible => left.Sleeve.gameObject.activeSelf;
    public bool RightVisible => right.Sleeve.gameObject.activeSelf;

    void Awake()
    {
        hands = GetComponentInParent<PlayerHands>();
        seat = GetComponentInParent<PlayerSeat>();
        view = GetComponent<PlayerCamera>();
        wrench = GetComponentInParent<PlayerWrench>();
        left = BuildArm(LeftShoulder);
        right = BuildArm(RightShoulder);

        torch = new GameObject("Torch").transform;
        torch.SetParent(right.Hand, false);
        Material metal = ProceduralModels.Lit(TorchColor, 0f, 0f, 0.5f);
        Bare(ProceduralModels.Part(torch, "TorchBody", PrimitiveType.Cylinder, new Vector3(0f, 0.03f, 0.05f), new Vector3(0.042f, 0.09f, 0.042f), metal, Quaternion.Euler(90f, 0f, 0f)));
        Bare(ProceduralModels.Part(torch, "TorchHead", PrimitiveType.Cylinder, new Vector3(0f, 0.03f, 0.15f), new Vector3(0.062f, 0.025f, 0.062f), metal, Quaternion.Euler(90f, 0f, 0f)));

        // Гаечный ключ: появляется в руке на время работы и ходит туда-обратно.
        wrenchModel = new GameObject("WrenchModel").transform;
        wrenchModel.SetParent(right.Hand, false);
        Material steel = ProceduralModels.Lit(new Color(0.34f, 0.34f, 0.34f), 0f, 0f, 0.55f);
        Bare(ProceduralModels.Part(wrenchModel, "WrenchShaft", PrimitiveType.Cube, new Vector3(0f, 0f, 0.12f), new Vector3(0.022f, 0.01f, 0.2f), steel));
        Bare(ProceduralModels.Part(wrenchModel, "WrenchHead", PrimitiveType.Cylinder, new Vector3(0f, 0f, 0.235f), new Vector3(0.05f, 0.008f, 0.05f), steel));
        wrenchModel.gameObject.SetActive(false);
    }

    Arm BuildArm(Vector3 shoulder)
    {
        Material cloth = ProceduralModels.Lit(SleeveColor, 0f, 0f, 0.05f, 9);
        Material glove = ProceduralModels.Lit(GloveColor, 0f, 0f, 0.05f, 9);
        cloths.Add(cloth);
        gloves.Add(glove);

        var arm = new Arm { Shoulder = shoulder, Current = shoulder + Vector3.down * HiddenDrop };
        arm.Sleeve = ProceduralModels.Part(transform, "ViewSleeve", PrimitiveType.Cylinder, shoulder, Vector3.one, cloth).transform;
        arm.Hand = new GameObject("ViewHand").transform;
        arm.Hand.SetParent(transform, false);
        Bare(arm.Sleeve.gameObject);
        Bare(ProceduralModels.Part(arm.Hand, "Mitt", PrimitiveType.Sphere, Vector3.zero, new Vector3(0.1f, 0.075f, 0.13f), glove));
        Bare(ProceduralModels.Part(arm.Hand, "Thumb", PrimitiveType.Sphere, new Vector3(0.015f, 0.04f, 0.03f), new Vector3(0.035f, 0.035f, 0.07f), glove));
        return arm;
    }

    // Тени от рук в кадре только мешали бы: настоящую тень уже даёт манекен.
    static void Bare(GameObject part)
    {
        Renderer renderer = part.GetComponent<Renderer>();
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
    }

    // В кабине руки в тени крыши, как и всё вокруг: иначе они светятся на фоне тёмной панели.
    void UpdateCabinShade()
    {
        if (shaded == seat.IsSeated)
            return;
        shaded = seat.IsSeated;
        float shade = shaded ? CabinArmShade : 1f;
        foreach (Material cloth in cloths)
            cloth.color = SleeveColor * shade;
        foreach (Material glove in gloves)
            glove.color = GloveColor * shade;
    }

    void LateUpdate()
    {
        UpdateCabinShade();
        bool holding = !seat.IsSeated && hands.Held != null;
        bool torchOn = !seat.IsSeated && !holding && view.Flashlight.enabled;
        torch.gameObject.SetActive(torchOn);

        Vector3 leftTarget = left.Shoulder + Vector3.down * HiddenDrop;
        Vector3 rightTarget = right.Shoulder + Vector3.down * HiddenDrop;

        if (holding)
        {
            Vector3 item = transform.InverseTransformPoint(hands.Held.worldCenterOfMass);
            if (hands.IsDragging)
                leftTarget = Clamp(left.Shoulder, item + Vector3.left * HandSpread);
            rightTarget = hands.IsDragging ? Clamp(right.Shoulder, item + Vector3.right * HandSpread) : Clamp(right.Shoulder, item + GripBelowItem);
        }
        else if (torchOn)
        {
            rightTarget = TorchHand;
        }

        bool working = wrench.WorkPoint.HasValue;
        wrenchModel.gameObject.SetActive(working);
        if (working)
        {
            torch.gameObject.SetActive(false);
            Vector3 point = transform.InverseTransformPoint(wrench.WorkPoint.Value);
            float swing = Mathf.Sin(Time.time * WrenchSwingSpeed);
            rightTarget = Clamp(right.Shoulder, point + new Vector3(swing * 0.05f, -0.06f, -0.2f));
            wrenchModel.localRotation = Quaternion.Euler(0f, swing * 35f, 0f);
        }

        Pose(left, leftTarget, holding && hands.IsDragging);
        Pose(right, rightTarget, holding || torchOn || working);
    }

    static Vector3 Clamp(Vector3 shoulder, Vector3 target)
    {
        return shoulder + Vector3.ClampMagnitude(target - shoulder, Reach);
    }

    void Pose(Arm arm, Vector3 target, bool wanted)
    {
        arm.Current = Vector3.Lerp(arm.Current, target, 1f - Mathf.Exp(-Response * Time.deltaTime));

        // Опущенная рука уже ниже кадра: незачем её рисовать.
        bool visible = wanted || (arm.Current - (arm.Shoulder + Vector3.down * HiddenDrop)).sqrMagnitude > 0.004f;
        arm.Sleeve.gameObject.SetActive(visible);
        arm.Hand.gameObject.SetActive(visible);
        if (!visible)
            return;

        Vector3 along = arm.Current - arm.Shoulder;
        arm.Sleeve.localPosition = arm.Shoulder + along * 0.5f;
        arm.Sleeve.localRotation = Quaternion.FromToRotation(Vector3.up, along);
        arm.Sleeve.localScale = new Vector3(0.085f, along.magnitude * 0.5f, 0.085f);

        arm.Hand.localPosition = arm.Current;
        arm.Hand.localRotation = Quaternion.LookRotation(along.sqrMagnitude > 0.0001f ? along : Vector3.forward, Vector3.up);
    }
}
