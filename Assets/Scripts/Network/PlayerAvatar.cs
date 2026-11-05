using Unity.Netcode;
using UnityEngine;
using UnityEngine.Rendering;

// Сетевое тело игрока. Управление, камера и интерфейс есть только у своего игрока: они собираются
// при появлении в сети. Для остальных это манекен, который двигает NetworkTransform.
public class PlayerAvatar : NetworkBehaviour
{
    const float LookSendThreshold = 0.5f;
    const float LookSmoothing = 12f;
    const float FaceSmoothing = 4f;
    const float MaxFacePitch = 25f;
    const int NpcLayer = 0;

    readonly NetworkVariable<Vector2> look = new NetworkVariable<Vector2>(
        Vector2.zero, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

    readonly NetworkVariable<bool> seated = new NetworkVariable<bool>(
        false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

    readonly NetworkVariable<int> seatIndex = new NetworkVariable<int>(
        PlayerSeat.NoSeat, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

    // Бот принадлежит серверу, как и хост: отличить своего игрока можно только вместе с IsPlayerObject.
    public bool IsLocal => IsOwner && NetworkObject.IsPlayerObject;
    public bool IsSeatedNetwork => seated.Value;
    public int NetworkSeat => seatIndex.Value;

    // Кузьмич поворачивается к тому, кто подошёл. У каждого на экране к нему самому.
    public Transform FaceTarget { get; set; }

    Transform bodyPivot;
    Transform headPivot;
    PlayerCamera view;
    PlayerSeat seat;
    CapsuleCollider capsule;
    VehicleController car;
    const float CarClearWidth = 1.35f;
    const float CarClearLength = 3.1f;
    const float CarClearHeight = 2f;
    const string KuzmichModel = "Kuzmich";
    const float KuzmichFeet = -0.9f;
    float shownYaw;
    float shownPitch;

    void Awake()
    {
        capsule = GetComponent<CapsuleCollider>();
        bodyPivot = transform.Find(ProceduralModels.MannequinName);
        headPivot = bodyPivot.Find(ProceduralModels.MannequinHeadName);
    }

    bool InsideCar()
    {
        if (car == null)
            car = FindAnyObjectByType<VehicleController>();
        if (car == null)
            return false;
        Vector3 local = car.transform.InverseTransformPoint(transform.position);
        return Mathf.Abs(local.x) < CarClearWidth && Mathf.Abs(local.z) < CarClearLength && Mathf.Abs(local.y) < CarClearHeight;
    }

    public override void OnNetworkSpawn()
    {
        var body = GetComponent<Rigidbody>();
        ProceduralModels.PaintMannequin(bodyPivot, NetworkObject.IsPlayerObject ? OwnerClientId : ulong.MaxValue);
        gameObject.AddComponent<CharacterAnimator>();
        if (!NetworkObject.IsPlayerObject)
            SkinnedCharacter.TryApply(bodyPivot, KuzmichModel, KuzmichFeet);

        if (!IsLocal)
        {
            // Чужое тело двигается по сети: физика только мешала бы этому.
            body.isKinematic = true;

            // Луч взгляда проходит сквозь игроков, а с Кузьмичом можно заговорить.
            if (!NetworkObject.IsPlayerObject)
            {
                gameObject.layer = NpcLayer;
                gameObject.AddComponent<NpcTalk>();
            }
            return;
        }

        // Своего тела игрок не видит, но тень от него падает как обычно.
        foreach (Renderer part in bodyPivot.GetComponentsInChildren<Renderer>())
            part.shadowCastingMode = ShadowCastingMode.ShadowsOnly;

        RuntimeBootstrapper.BuildLocalRig(gameObject);
        view = GetComponentInChildren<PlayerCamera>();
        seat = GetComponent<PlayerSeat>();
        RuntimeBootstrapper.OnLocalPlayerReady();
    }

    void Update()
    {
        if (IsLocal && view != null)
        {
            Vector3 angles = view.transform.eulerAngles;
            var current = new Vector2(angles.y, Mathf.DeltaAngle(0f, angles.x));
            if (Mathf.Abs(Mathf.DeltaAngle(look.Value.x, current.x)) > LookSendThreshold || Mathf.Abs(current.y - look.Value.y) > LookSendThreshold)
                look.Value = current;
            if (seated.Value != seat.IsSeated)
                seated.Value = seat.IsSeated;
            if (seatIndex.Value != seat.SeatIndex)
                seatIndex.Value = seat.SeatIndex;
        }
        else if (!IsLocal)
        {
            // Чужой водитель сидит внутри кабины: его капсула не должна толкать машину. После выхода его тело
            // приходит по сети с задержкой, а твёрдая капсула внутри кузова перевернула бы машину.
            capsule.enabled = !seated.Value && !InsideCar();
        }

        Vector2 target = look.Value;
        float smoothing = LookSmoothing;
        if (FaceTarget != null)
        {
            Vector3 toTarget = (FaceTarget.position - headPivot.position).normalized;
            target = new Vector2(Mathf.Atan2(toTarget.x, toTarget.z) * Mathf.Rad2Deg, Mathf.Clamp(-Mathf.Asin(toTarget.y) * Mathf.Rad2Deg, -MaxFacePitch, MaxFacePitch));
            smoothing = FaceSmoothing;
        }

        float blend = 1f - Mathf.Exp(-smoothing * Time.deltaTime);
        shownYaw = Mathf.LerpAngle(shownYaw, target.x, blend);
        shownPitch = Mathf.Lerp(shownPitch, target.y, blend);

        bodyPivot.rotation = Quaternion.Euler(0f, shownYaw, 0f);
        headPivot.localRotation = Quaternion.Euler(shownPitch, 0f, 0f);
    }

    // Кузьмича никто не ведёт, поэтому взгляд задаёт сервер как его владелец.
    public void SetBotPose(float yaw)
    {
        look.Value = new Vector2(yaw, 0f);
    }

    public Vector2 NetworkLook => look.Value;
    public float ShownPitch => shownPitch;
}
