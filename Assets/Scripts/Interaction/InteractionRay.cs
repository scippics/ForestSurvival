using UnityEngine;

// Общий луч взгляда для рук и подсказок: то, что подсказка обещает, руки и выполнят.
// В маске нет слоя Ignore Raycast, куда попадают капсула игрока и предмет в руках,
// а триггеры пропускаются, чтобы зона толкания не закрывала предметы за ней.
public static class InteractionRay
{
    public const float Range = 2.5f;
    public const string PickupableTag = "Pickupable";
    public const string PushTriggerTag = "PushTrigger";
    public const string JerrycanTag = "Jerrycan";
    public const string FuelSourceTag = "FuelSource";
    public const int IgnoreRaycastLayer = 2;

    public static bool Cast(Transform view, out RaycastHit hit)
    {
        return Physics.Raycast(view.position, view.forward, out hit, Range, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
    }

    // Тег у объекта один, поэтому у канистры Pickupable стоит на корне с Rigidbody,
    // а Jerrycan — на дочернем коллайдере, в который попадает луч.
    public static bool IsPickupable(RaycastHit hit)
    {
        return hit.rigidbody != null && hit.rigidbody.CompareTag(PickupableTag);
    }

    // Шланг вставлен в бак, конец в руках, канистра под прицелом: ЛКМ качает, а не отпускает шланг.
    public static bool IsFuelTransfer(FuelSiphon siphon, RaycastHit hit)
    {
        return siphon != null && !siphon.IsFlowing && hit.collider.CompareTag(JerrycanTag)
               && hit.rigidbody.TryGetComponent(out FuelCan can) && !can.IsFull
               && siphon.CanReach(can);
    }

    // Точка прицела раскрывается только там, где действие сейчас действительно доступно:
    // с полными руками другой предмет взять нельзя. Триггер PushTrigger луч пропускает,
    // поэтому зону толкания проверяет InteractionUI, а место водителя — это свободная машина.
    public static bool IsInteractable(RaycastHit hit, bool handsFull, FuelSiphon heldSiphon)
    {
        if (!handsFull && IsPickupable(hit))
            return true;
        return IsFiller(hit, out _)
               || IsFuelTransfer(heldSiphon, hit)
               || hit.collider.GetComponent<SalvagePart>() != null
               || IsFreeVehicle(hit, out _)
               || (!handsFull && (IsDoor(hit, out _) || IsNpc(hit, out _)));
    }

    public static bool IsFiller(RaycastHit hit, out AbandonedCar car)
    {
        car = null;
        if (!hit.collider.CompareTag(FuelSourceTag))
            return false;
        car = hit.collider.GetComponentInParent<AbandonedCar>();
        return car != null;
    }

    public static bool IsDoor(RaycastHit hit, out HouseDoor door)
    {
        door = null;
        return hit.rigidbody != null && hit.rigidbody.TryGetComponent(out door);
    }

    public static bool IsNpc(RaycastHit hit, out NpcTalk npc)
    {
        npc = null;
        return hit.rigidbody != null && hit.rigidbody.TryGetComponent(out npc);
    }

    public static bool IsCar(RaycastHit hit, out VehicleController vehicle)
    {
        vehicle = null;
        return hit.rigidbody != null && hit.rigidbody.TryGetComponent(out vehicle);
    }

    public static bool IsFreeVehicle(RaycastHit hit, out VehicleController vehicle)
    {
        vehicle = null;
        return hit.rigidbody != null
               && hit.rigidbody.TryGetComponent(out vehicle)
               && !vehicle.IsOccupied;
    }
}
