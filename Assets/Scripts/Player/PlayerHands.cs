using System.Collections.Generic;
using UnityEngine;

// Предмет лежит прямо в кисти: он не весит, не болтается и не выпадает, пока его не отпустят.
// Только длинные завалы остаются физическими: их не несут, а тащат за то место, где взялись.
public class PlayerHands : MonoBehaviour
{
    const float PositionGain = 10f;
    const float MaxCarrySpeed = 12f;
    const float VelocityResponse = 15f;
    const float DragBreakDistance = 3.6f;
    const float MaxDragForce = 900f;
    const float ThrowSpeed = 7f;
    const float DragThrowImpulse = 12f;
    const float PourAngle = 72f;
    const float GripResponse = 16f;
    const float DropClearance = 0.3f;
    const float DropProbeRadius = 0.12f;
    static readonly Vector3 HandOffset = new Vector3(0.36f, -0.42f, 0.72f);

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
    Transform handPoint;

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
        handPoint = new GameObject("HandPoint").transform;
        handPoint.SetParent(view, false);
        handPoint.localPosition = HandOffset;
    }

    void FixedUpdate()
    {
        RestoreSeparatedCollisions();
        HandleCommands();
        UpdateSiphon();

        if (Held != null && isDragged)
        {
            Vector3 grip = Held.transform.TransformPoint(dragLocalPoint);
            if (Vector3.Distance(grip, holdPosition.position) > DragBreakDistance)
                Release();
            else
                DriveDragged(grip);
        }
    }

    // Предмет дочерний к кисти, поэтому идёт вместе с камерой кадр в кадр. Здесь только плавный подъём и наклон при заливке.
    void LateUpdate()
    {
        if (Held == null || isDragged)
            return;

        float blend = 1f - Mathf.Exp(-GripResponse * Time.deltaTime);
        Transform item = Held.transform;
        item.localPosition = Vector3.Lerp(item.localPosition, Vector3.zero, blend);

        Quaternion wanted = handPoint.rotation;
        if (PourTilt)
        {
            Vector3 flat = Vector3.ProjectOnPlane(view.forward, Vector3.up);
            wanted = Quaternion.LookRotation(flat.sqrMagnitude > 0.01f ? flat : Vector3.forward, Vector3.up) * Quaternion.Euler(0f, 0f, -PourAngle);
        }
        item.rotation = Quaternion.Slerp(item.rotation, wanted, blend);
    }

    void HandleCommands()
    {
        if (input.ConsumeThrow() && Held != null)
        {
            Rigidbody thrown = Held;
            bool dragged = isDragged;
            Release();
            if (dragged)
                thrown.AddForce(view.forward * DragThrowImpulse, ForceMode.Impulse);
            else
                thrown.AddForce(view.forward * ThrowSpeed, ForceMode.VelocityChange);
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

        if (!isDragged)
            AttachToHand(target);
    }

    void AttachToHand(Rigidbody target)
    {
        // Лес сам переключает физику у далёких тел: на время в руке он этот предмет не трогает.
        OptimizedForestGenerator.NetworkFollowed.Add(target);
        target.linearVelocity = Vector3.zero;
        target.angularVelocity = Vector3.zero;
        target.isKinematic = true;
        target.interpolation = RigidbodyInterpolation.None;
        target.collisionDetectionMode = CollisionDetectionMode.Discrete;
        foreach (Collider part in heldColliders)
            part.enabled = false;
        target.transform.SetParent(handPoint, true);
    }

    // Предмет остаётся перед игроком: сквозь стену он не вылетит, даже если рука оказалась за ней.
    void DetachFromHand(Rigidbody target)
    {
        Vector3 eye = view.position;
        Vector3 toItem = target.position - eye;
        target.transform.SetParent(null, true);
        if (Physics.SphereCast(eye, DropProbeRadius, toItem.normalized, out RaycastHit hit, toItem.magnitude + DropClearance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            target.transform.position = eye + toItem.normalized * Mathf.Max(0f, hit.distance - DropClearance);

        target.position = target.transform.position;
        target.rotation = target.transform.rotation;
        foreach (Collider part in heldColliders)
        {
            part.enabled = true;

            // Выключенный коллайдер забывает, что игрока надо пропускать.
            Physics.IgnoreCollision(playerCollider, part, true);
        }
        target.isKinematic = false;
        target.linearVelocity = playerBody.linearVelocity;
        OptimizedForestGenerator.NetworkFollowed.Remove(target);
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

        if (!isDragged)
            DetachFromHand(Held);

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
        isDragged = false;
    }

    // Длинное дерево не несут, а тащат за то место, где взялись: один конец поднят, другой волочится по земле.
    void DriveDragged(Vector3 grip)
    {
        Vector3 toTarget = holdPosition.position - grip;
        Vector3 desired = playerBody.linearVelocity + Vector3.ClampMagnitude(toTarget * PositionGain, MaxCarrySpeed);
        Vector3 force = Held.mass * (desired - Held.GetPointVelocity(grip)) * VelocityResponse;
        Held.AddForceAtPosition(Vector3.ClampMagnitude(force, MaxDragForce), grip, ForceMode.Force);
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
