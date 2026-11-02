using Unity.Netcode;
using UnityEngine;
using UnityEngine.Rendering;

// Сетевое тело игрока. Управление, камера и интерфейс есть только у своего игрока: они собираются
// при появлении в сети. Для остальных это манекен, который двигает NetworkTransform.
public class PlayerAvatar : NetworkBehaviour
{
    const float LookSendThreshold = 0.5f;
    const float LookSmoothing = 12f;

    readonly NetworkVariable<Vector2> look = new NetworkVariable<Vector2>(
        Vector2.zero, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

    // Бот принадлежит серверу, как и хост: отличить своего игрока можно только вместе с IsPlayerObject.
    public bool IsLocal => IsOwner && NetworkObject.IsPlayerObject;

    Transform bodyPivot;
    Transform headPivot;
    PlayerCamera view;
    float shownYaw;
    float shownPitch;

    void Awake()
    {
        bodyPivot = transform.Find(ProceduralModels.MannequinName);
        headPivot = bodyPivot.Find(ProceduralModels.MannequinHeadName);
    }

    public override void OnNetworkSpawn()
    {
        var body = GetComponent<Rigidbody>();
        ProceduralModels.PaintMannequin(bodyPivot, NetworkObject.IsPlayerObject ? OwnerClientId : ulong.MaxValue);

        if (!IsLocal)
        {
            // Чужое тело двигается по сети: физика только мешала бы этому.
            body.isKinematic = true;
            return;
        }

        // Своего тела игрок не видит, но тень от него падает как обычно.
        foreach (Renderer part in bodyPivot.GetComponentsInChildren<Renderer>())
            part.shadowCastingMode = ShadowCastingMode.ShadowsOnly;

        RuntimeBootstrapper.BuildLocalRig(gameObject);
        view = GetComponentInChildren<PlayerCamera>();
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
        }

        float blend = 1f - Mathf.Exp(-LookSmoothing * Time.deltaTime);
        shownYaw = Mathf.LerpAngle(shownYaw, look.Value.x, blend);
        shownPitch = Mathf.Lerp(shownPitch, look.Value.y, blend);

        bodyPivot.rotation = Quaternion.Euler(0f, shownYaw, 0f);
        headPivot.localRotation = Quaternion.Euler(shownPitch, 0f, 0f);
    }

    // Бота никто не ведёт, поэтому взгляд задаёт сервер как его владелец.
    public void SetBotPose(float yaw)
    {
        look.Value = new Vector2(yaw, 0f);
    }

    public Vector2 NetworkLook => look.Value;
    public float ShownPitch => shownPitch;
}
