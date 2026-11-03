using UnityEngine;

// Дверь дома: физическая на петле, поэтому её можно и толкнуть плечом, и открыть рукой.
// Со срубом она не сталкивается: иначе полотно клинит о косяк, и игрок остаётся внутри.
public class HouseDoor : MonoBehaviour
{
    const float OpenAngle = -90f;
    const float ClosedAngle = 0f;
    const float Spring = 90f;
    const float Damper = 22f;
    const float SoundVolume = 0.6f;

    public bool IsOpen { get; private set; }

    HingeJoint hinge;
    Rigidbody body;

    public void Setup(HingeJoint joint, float startAngle)
    {
        hinge = joint;
        body = joint.GetComponent<Rigidbody>();
        hinge.useSpring = true;
        Aim(startAngle);
    }

    public void IgnoreWalls(Transform house)
    {
        var box = GetComponent<Collider>();
        foreach (Collider part in house.GetComponentsInChildren<Collider>())
        {
            if (part != box)
                Physics.IgnoreCollision(box, part, true);
        }
    }

    // В сети состояние ведёт хост, иначе у игроков дверь разойдётся при одновременном нажатии.
    public void Toggle()
    {
        if (WorldSync.Instance != null)
            WorldSync.Instance.RequestDoor(!IsOpen);
        else
            SetOpen(!IsOpen);
    }

    public void SetOpen(bool open)
    {
        IsOpen = open;
        Aim(open ? OpenAngle : ClosedAngle);
        ForestAudio.Play(ForestSound.Door, SoundVolume);
    }

    void Aim(float angle)
    {
        hinge.spring = new JointSpring { spring = Spring, damper = Damper, targetPosition = angle };
        body.WakeUp();
    }
}
