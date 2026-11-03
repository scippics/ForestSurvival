using System.Collections.Generic;
using UnityEngine;

// Предмет в руках остаётся обычным динамическим телом: его тянет к HoldPosition сила
// с ограничением. Поэтому тяжёлое отстаёт и провисает, а стены его останавливают.
public class PlayerHands : MonoBehaviour
{
    const float MaxLiftForce = 350f;
    const float PositionGain = 10f;
    const float MaxCarrySpeed = 12f;
    const float VelocityResponse = 15f;
    const float RotationGain = 8f;
    const float MaxAngularSpeed = 10f;
    const float BreakDistance = 2f;
    const float DragBreakDistance = 3.6f;
    const float MaxDragForce = 900f;
    const float ThrowImpulse = 12f;
    const float PourAngle = 72f;

    public FuelSiphon HeldSiphon { get; private set; }
    public Rigidbody Held { get; private set; }
    public bool PourTilt { get; set; }
    public bool IsDragging => Held != null && isDragged;

    PlayerInputReader input;
    BlueprintNotebook notebook;
    Rigidbody playerBody;
    Collider playerCollider;
    Transform view;
    Transform holdPosition;

    Quaternion heldRotationOffset;
    Vector3 dragLocalPoint;
    bool isDragged;
    CollisionDetectionMode savedCollisionMode;
    RigidbodyInterpolation savedInterpolation;
    readonly Dictionary<GameObject, int> savedLayers = new Dictionary<GameObject, int>();
    readonly List<Collider> heldColliders = new List<Collider>();
    readonly List<Collider> pendingCollisionRestore = new List<Collider>();

    void Awake()
    {
        input = GetComponent<PlayerInputReader>();
        notebook = GetComponent<BlueprintNotebook>();
        playerBody = GetComponent<Rigidbody>();
        playerCollider = GetComponent<Collider>();
        view = GetComponentInChildren<PlayerCamera>().transform;
        holdPosition = view.Find("HoldPosition");
    }

    void FixedUpdate()
    {
        RestoreSeparatedCollisions();
        HandleCommands();
        UpdateSiphon();

        if (Held != null)
        {
            Vector3 grip = isDragged ? Held.transform.TransformPoint(dragLocalPoint) : Held.worldCenterOfMass;
            if (Vector3.Distance(grip, holdPosition.position) > (isDragged ? DragBreakDistance : BreakDistance))
                Release();
            else if (isDragged)
                DriveDragged(grip);
            else
                DriveHeld();
        }
    }

    void HandleCommands()
    {
        if (input.ConsumeThrow() && Held != null)
        {
            Rigidbody thrown = Held;
            Release();
            thrown.AddForce(view.forward * ThrowImpulse, ForceMode.Impulse);
        }

        if (!input.ConsumeGrab())
            return;

        // Пока записка перед глазами, клик только убирает её.
        if (StoryUI.Instance.NoteOpen)
        {
            StoryUI.Instance.CloseNote();
            return;
        }

        if (NpcTalk.Current != null)
        {
            NpcTalk.Current.Close();
            return;
        }

        bool aimed = InteractionRay.Cast(view, out RaycastHit hit);
        if (Held == null)
        {
            if (aimed && InteractionRay.IsPickupable(hit))
            {
                dragLocalPoint = hit.rigidbody.transform.InverseTransformPoint(hit.point);
                Grab(hit.rigidbody);
            }
            else if (aimed && InteractionRay.IsDoor(hit, out HouseDoor door))
                door.Toggle();
            else if (aimed && InteractionRay.IsNpc(hit, out NpcTalk npc))
                npc.Open(input, transform);
        }
        else if (aimed && HeldSiphon != null && HeldSiphon.Source == null && InteractionRay.IsFiller(hit, out AbandonedCar wreck))
        {
            HeldSiphon.Insert(wreck);
        }
        else if (aimed && InteractionRay.IsFuelTransfer(HeldSiphon, hit))
        {
            HeldSiphon.BeginPump();
        }
        else
        {
            Release();
        }
    }

    // Качать можно только пока прицел на канистре: отвёл взгляд — попытка сорвалась.
    void UpdateSiphon()
    {
        if (HeldSiphon == null || !HeldSiphon.IsPumping)
            return;

        if (InteractionRay.Cast(view, out RaycastHit hit) && InteractionRay.IsFuelTransfer(HeldSiphon, hit))
            HeldSiphon.TickPump(input.GrabHeld, Time.fixedDeltaTime, hit.rigidbody.GetComponent<FuelCan>());
        else
            HeldSiphon.CancelPump();

        // Бензин пошёл: шланг опускается в канистру, а руки свободны.
        if (HeldSiphon != null && HeldSiphon.IsFlowing)
            Release();
    }

    void Grab(Rigidbody target)
    {
        if (target.TryGetComponent(out BlueprintItem blueprint))
        {
            notebook.AddRecipe(blueprint.recipeID);
            if (WorldSync.Instance != null)
                WorldSync.Instance.AnnounceRecipe(blueprint.name, blueprint.recipeID);
            Destroy(blueprint.gameObject);
            ForestAudio.Play(ForestSound.Paper, 0.7f);
            StoryUI.Instance.Say("Чертёж в блокноте. Tab - открыть.");
            return;
        }

        if (target.TryGetComponent(out CellPack _))
        {
            GetComponent<TorchCharge>().Refill();
            ForestAudio.Play(ForestSound.Click, 0.6f);
            if (WorldSync.Instance != null)
                WorldSync.Instance.AnnounceItemRemoved(target.name);
            Destroy(target.gameObject);
            return;
        }

        if (target.TryGetComponent(out NoteItem note))
        {
            StoryUI.Instance.ShowNote(note.text);
            ForestAudio.Play(ForestSound.Paper, 0.8f);
            StoryDirector.Instance.OnNoteRead(note.id);
            return;
        }

        // Завал отпускает землю, как только за него взялись.
        isDragged = target.TryGetComponent(out RoadBlock block);
        if (isDragged)
            block.Free();

        Held = target;
        HeldSiphon = target.GetComponent<FuelSiphon>();
        ForestAudio.Play(ForestSound.Click, 0.25f);
        if (target.TryGetComponent(out FuelCan can) && can.Liters > 0f)
            StoryDirector.Instance.OnFuelInHands();
        heldRotationOffset = Quaternion.Inverse(holdPosition.rotation) * target.rotation;

        savedCollisionMode = target.collisionDetectionMode;
        savedInterpolation = target.interpolation;
        target.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        target.interpolation = RigidbodyInterpolation.Interpolate;

        foreach (Transform part in target.GetComponentsInChildren<Transform>())
        {
            savedLayers[part.gameObject] = part.gameObject.layer;
            part.gameObject.layer = InteractionRay.IgnoreRaycastLayer;
        }

        // Иначе можно встать на предмет в собственных руках и поднять себя в воздух.
        target.GetComponentsInChildren(heldColliders);
        foreach (Collider part in heldColliders)
        {
            pendingCollisionRestore.Remove(part);
            Physics.IgnoreCollision(playerCollider, part, true);
        }
    }

    public void ReleaseHeld()
    {
        if (Held != null)
            Release();
    }

    void OnEnable()
    {
        // Нажатия, сделанные пока игрок сидел за рулём, не должны сработать при выходе.
        if (input != null)
        {
            input.ConsumeGrab();
            input.ConsumeThrow();
        }
    }

    void Release()
    {
        if (HeldSiphon != null)
            HeldSiphon.CancelPump();

        Held.collisionDetectionMode = savedCollisionMode;
        Held.interpolation = savedInterpolation;

        foreach (KeyValuePair<GameObject, int> saved in savedLayers)
            saved.Key.layer = saved.Value;
        savedLayers.Clear();

        pendingCollisionRestore.AddRange(heldColliders);
        heldColliders.Clear();

        Held = null;
        HeldSiphon = null;
        PourTilt = false;
    }

    // Длинное дерево не несут, а тащат за то место, где взялись: один конец поднят, другой волочится по земле.
    void DriveDragged(Vector3 grip)
    {
        Vector3 toTarget = holdPosition.position - grip;
        Vector3 desired = playerBody.linearVelocity + Vector3.ClampMagnitude(toTarget * PositionGain, MaxCarrySpeed);
        Vector3 force = Held.mass * (desired - Held.GetPointVelocity(grip)) * VelocityResponse;
        Held.AddForceAtPosition(Vector3.ClampMagnitude(force, MaxDragForce), grip, ForceMode.Force);
    }

    void DriveHeld()
    {
        Vector3 toTarget = holdPosition.position - Held.worldCenterOfMass;
        Vector3 desired = playerBody.linearVelocity + Vector3.ClampMagnitude(toTarget * PositionGain, MaxCarrySpeed);
        Vector3 force = Held.mass * ((desired - Held.linearVelocity) * VelocityResponse - Physics.gravity);
        Held.AddForce(Vector3.ClampMagnitude(force, MaxLiftForce), ForceMode.Force);

        // Когда льём, канистра наклоняется горловиной вперёд и вниз, как бы её ни подняли.
        Quaternion wanted = holdPosition.rotation * heldRotationOffset;
        if (PourTilt)
        {
            Vector3 flat = Vector3.ProjectOnPlane(view.forward, Vector3.up);
            wanted = Quaternion.LookRotation(flat.sqrMagnitude > 0.01f ? flat : Vector3.forward, Vector3.up) * Quaternion.Euler(0f, 0f, -PourAngle);
        }

        Quaternion delta = wanted * Quaternion.Inverse(Held.rotation);
        delta.ToAngleAxis(out float angle, out Vector3 axis);
        if (angle > 180f)
            angle -= 360f;
        Vector3 spin = float.IsInfinity(axis.x) ? Vector3.zero : axis * (angle * Mathf.Deg2Rad * RotationGain);
        Held.angularVelocity = Vector3.ClampMagnitude(spin, MaxAngularSpeed);
    }

    // Столкновение с игроком возвращается только когда предмет уже вышел из капсулы,
    // иначе физика вытолкнет его рывком.
    void RestoreSeparatedCollisions()
    {
        for (int i = pendingCollisionRestore.Count - 1; i >= 0; i--)
        {
            Collider part = pendingCollisionRestore[i];
            if (part == null)
            {
                pendingCollisionRestore.RemoveAt(i);
            }
            else if (!part.bounds.Intersects(playerCollider.bounds))
            {
                Physics.IgnoreCollision(playerCollider, part, false);
                pendingCollisionRestore.RemoveAt(i);
            }
        }
    }
}
