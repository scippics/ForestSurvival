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

    // Шланг в руках, канистра под прицелом и источник рядом: ЛКМ качает, а не отпускает шланг.
    public static bool IsFuelTransfer(FuelSiphon siphon, RaycastHit hit)
    {
        return siphon != null && hit.collider.CompareTag(JerrycanTag) && siphon.IsNearFuelSource();
    }

    public static bool IsFreeVehicle(RaycastHit hit, out VehicleController vehicle)
    {
        vehicle = null;
        return hit.rigidbody != null
               && hit.rigidbody.TryGetComponent(out vehicle)
               && vehicle.Driver == null;
    }
}
