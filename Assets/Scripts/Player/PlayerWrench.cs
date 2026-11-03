using UnityEngine;

// Работа ключом на зажатой G: снять деталь с брошенной машины, поставить на свою, снять со своей.
// Рука с ключом тянется к месту работы, деталь шатается, а шкала показывает, сколько осталось.
public class PlayerWrench : MonoBehaviour
{
    const float RatchetPeriod = 0.42f;
    const float WobbleMax = 0.012f;
    const float DropDistance = 1.3f;
    const float BatterySeconds = 3f;
    const float RepairSeconds = 6f;

    enum Job
    {
        None,
        Salvage,
        Mount,
        Unmount,
        Battery,
        Repair
    }

    public string Prompt { get; private set; } = "";

    // Точка, к которой тянется рука; null, пока никто не работает.
    public Vector3? WorkPoint { get; private set; }

    PlayerInputReader input;
    PlayerHands hands;
    PlayerSeat seat;
    Transform view;

    float progress;
    float nextRatchet;
    Object lastTarget;
    int lastJobStamp;

    void Awake()
    {
        input = GetComponent<PlayerInputReader>();
        hands = GetComponent<PlayerHands>();
        seat = GetComponent<PlayerSeat>();
        view = GetComponentInChildren<PlayerCamera>().transform;
    }

    Job Evaluate(out SalvagePart part, out CarPartItem item, out CarSlot slot, out VehicleController car, out Vector3 point)
    {
        part = null;
        item = null;
        slot = default;
        car = null;
        point = default;

        if (seat.IsSeated || GameFlow.State != GameState.Playing || !InteractionRay.Cast(view, out RaycastHit hit))
            return Job.None;
        point = hit.point;

        if (hands.Held != null)
        {
            if (hands.Held.TryGetComponent(out item) && InteractionRay.IsFreeVehicle(hit, out car) && car.TryGetComponent(out CarUpgrades _))
                return Job.Mount;
            if (hands.Held.TryGetComponent(out CarBatteryItem _) && InteractionRay.IsFreeVehicle(hit, out car))
                return Job.Battery;
            if (hands.Held.TryGetComponent(out RepairKit _) && InteractionRay.IsFreeVehicle(hit, out car) && car.Condition < 1f)
                return Job.Repair;
            return Job.None;
        }

        part = hit.collider.GetComponent<SalvagePart>();
        if (part != null)
            return Job.Salvage;

        if (InteractionRay.IsFreeVehicle(hit, out car) && car.TryGetComponent(out CarUpgrades upgrades) && upgrades.FindNearest(hit.point, out slot))
            return Job.Unmount;
        return Job.None;
    }

    void Update()
    {
        Job job = Evaluate(out SalvagePart part, out CarPartItem item, out CarSlot slot, out VehicleController car, out _);
        switch (job)
        {
            case Job.Salvage:
                Prompt = "Зажать G - Снять: " + CarParts.Info[part.Kind].Name;
                break;
            case Job.Mount:
                bool busy = car.GetComponent<CarUpgrades>().Mounted.ContainsKey(item.Info.Slot);
                Prompt = "Зажать G - " + (busy ? "Заменить" : "Поставить") + ": " + item.Info.Name;
                break;
            case Job.Unmount:
                Prompt = "Зажать G - Снять: " + CarParts.Info[car.GetComponent<CarUpgrades>().Mounted[slot]].Name;
                break;
            case Job.Battery:
                Prompt = "Зажать G - Поменять аккумулятор: в машине " + Mathf.RoundToInt(car.Battery * 100f) + "%, этот " + Mathf.RoundToInt(hands.Held.GetComponent<CarBatteryItem>().Charge * 100f) + "%";
                break;
            case Job.Repair:
                Prompt = "Зажать G - Чинить: состояние " + Mathf.RoundToInt(car.Condition * 100f) + "%";
                break;
            default:
                Prompt = "";
                break;
        }
    }

    void FixedUpdate()
    {
        Job job = Evaluate(out SalvagePart part, out CarPartItem item, out CarSlot slot, out VehicleController car, out Vector3 point);
        if (job == Job.None || !input.WrenchHeld)
        {
            Stop();
            return;
        }

        Object target = job == Job.Salvage ? part : job == Job.Mount ? item : job == Job.Battery || job == Job.Repair ? hands.Held : (Object)car;
        int stamp = (int)job * 100 + (int)slot;
        if (target != lastTarget || stamp != lastJobStamp)
            progress = 0f;
        lastTarget = target;
        lastJobStamp = stamp;
        WorkPoint = point;

        string workName;
        float seconds;
        if (job == Job.Battery)
        {
            workName = "Аккумулятор";
            seconds = BatterySeconds;
        }
        else if (job == Job.Repair)
        {
            workName = "Ремонт";
            seconds = RepairSeconds;
        }
        else
        {
            CarPartInfo info = job == Job.Salvage ? CarParts.Info[part.Kind]
                : job == Job.Mount ? item.Info
                : CarParts.Info[car.GetComponent<CarUpgrades>().Mounted[slot]];
            workName = info.Name;
            seconds = info.Seconds;
        }

        progress += Time.fixedDeltaTime;
        float done = progress / seconds;
        StoryUI.Instance.ShowBar(workName, done);
        if (part != null)
            part.Wobble(WobbleMax * done);

        if (Time.time > nextRatchet)
        {
            nextRatchet = Time.time + RatchetPeriod;
            ForestAudio.Play(ForestSound.Ratchet, 0.45f);
        }

        if (done < 1f)
            return;

        switch (job)
        {
            case Job.Salvage: FinishSalvage(part); break;
            case Job.Mount: FinishMount(item, car); break;
            case Job.Unmount: FinishUnmount(car, slot, point); break;
            case Job.Battery: FinishBattery(car); break;
            case Job.Repair: FinishRepair(car); break;
        }
        ForestAudio.Play(ForestSound.Click, 0.6f);
        Stop();
    }

    void Stop()
    {
        progress = 0f;
        lastTarget = null;
        WorkPoint = null;
    }

    void FinishBattery(VehicleController car)
    {
        var item = hands.Held.GetComponent<CarBatteryItem>();
        float old = car.Battery;
        car.Battery = item.Charge;
        item.Charge = old;
        StoryUI.Instance.Say("Аккумулятор заменён: теперь " + Mathf.RoundToInt(car.Battery * 100f) + "%.");
    }

    void FinishRepair(VehicleController car)
    {
        GameObject kit = hands.Held.gameObject;
        hands.ReleaseHeld();
        car.GetComponent<CarUpgrades>().Repair();
        if (WorldSync.Instance != null)
            WorldSync.Instance.AnnounceItemRemoved(kit.name);
        Destroy(kit);
        StoryUI.Instance.Say("Подлатал как мог: состояние " + Mathf.RoundToInt(car.Condition * 100f) + "%.");
    }

    void FinishSalvage(SalvagePart part)
    {
        AbandonedCar wreck = part.Car;
        string itemName = CarPartItem.MakeName(part.Kind, wreck.Id + "-" + part.Index);
        Vector3 position = part.transform.position;
        Quaternion rotation = part.transform.rotation;
        wreck.TakePart(part);
        wreck.Announce();

        SpawnItem(itemName, part.Kind, position, rotation);
        StoryUI.Instance.Say(CarParts.Info[part.Kind].Name + " снята. Теперь донести до машины.");
    }

    void FinishMount(CarPartItem item, VehicleController car)
    {
        var upgrades = car.GetComponent<CarUpgrades>();
        CarPartKind kind = item.Kind;
        string name = item.name;
        Vector3 at = item.transform.position;

        hands.ReleaseHeld();
        if (WorldSync.Instance != null)
            WorldSync.Instance.AnnounceItemRemoved(name);
        Destroy(item.gameObject);

        if (upgrades.Mount(kind, out CarPartKind replaced))
        {
            string dropName = CarPartItem.MakeName(replaced, "car-" + Time.frameCount);
            SpawnItem(dropName, replaced, at + Vector3.up * 0.3f, Quaternion.identity);
        }
        if (WorldSync.Instance != null)
            WorldSync.Instance.AnnounceMounted(CarParts.Info[kind].Slot, true, kind);

        StoryUI.Instance.Say(CarParts.Info[kind].Name + " стоит. Машина: " + Mathf.RoundToInt(car.GetComponent<Rigidbody>().mass) + " кг.");
    }

    void FinishUnmount(VehicleController car, CarSlot slot, Vector3 point)
    {
        var upgrades = car.GetComponent<CarUpgrades>();
        if (!upgrades.Unmount(slot, out CarPartKind kind))
            return;
        if (WorldSync.Instance != null)
            WorldSync.Instance.AnnounceMounted(slot, false, kind);

        // Деталь падает к ногам, а не внутрь кузова.
        Vector3 toPlayer = transform.position - point;
        toPlayer.y = 0f;
        Vector3 drop = point + toPlayer.normalized * Mathf.Min(DropDistance, toPlayer.magnitude * 0.5f) + Vector3.up * 0.3f;
        SpawnItem(CarPartItem.MakeName(kind, "car-" + Time.frameCount), kind, drop, Quaternion.identity);
    }

    static void SpawnItem(string name, CarPartKind kind, Vector3 position, Quaternion rotation)
    {
        GameObject item = CarPartItem.Create(name, kind, position, rotation);
        OptimizedForestGenerator forest = FindAnyObjectByType<OptimizedForestGenerator>();
        if (forest != null)
            forest.Track(item.GetComponent<Rigidbody>());
        if (WorldSync.Instance != null)
            WorldSync.Instance.AnnounceItemCreated(name, position, rotation);
    }
}
