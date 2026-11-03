using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

// Общий мир кооператива: машина, предметы в руках, дверь дома, время суток, дождь и задача пролога.
// Сами объекты строятся у всех одинаково из кода, поэтому сетевой здесь один этот объект.
// Физику машины считает тот, кто за рулём, а пустую считает хост; остальные только следуют за ним.
public class WorldSync : NetworkBehaviour
{
    public const ulong NoDriver = ulong.MaxValue;

    const float CarSendPeriod = 0.05f;
    const float ItemSendPeriod = 0.066f;
    const float WorldSendPeriod = 1f;
    const float TimeTolerance = 0.002f;
    const float FollowResponse = 14f;
    const float CarSnapDistance = 8f;
    const float ItemFollowSeconds = 0.5f;
    const float AfterReleaseSeconds = 2f;

    class FollowedItem
    {
        public Rigidbody Body;
        public Vector3 Position;
        public Quaternion Rotation;
        public Vector3 Velocity;
        public float Until;
    }

    public static WorldSync Instance { get; private set; }

    public bool HasClients => IsSpawned && NetworkManager.ConnectedClientsIds.Count > 1;
    public ulong DriverId => driverId.Value;

    readonly NetworkVariable<ulong> driverId = new NetworkVariable<ulong>(NoDriver);
    readonly NetworkVariable<float> timeOfDay = new NetworkVariable<float>();
    readonly NetworkVariable<bool> raining = new NetworkVariable<bool>();
    readonly NetworkVariable<int> storyStage = new NetworkVariable<int>();
    readonly NetworkVariable<bool> doorOpen = new NetworkVariable<bool>();

    readonly Dictionary<string, Rigidbody> items = new Dictionary<string, Rigidbody>();
    readonly Dictionary<string, FollowedItem> followed = new Dictionary<string, FollowedItem>();
    readonly List<string> finished = new List<string>();

    VehicleController car;
    Rigidbody carBody;
    PlayerHands hands;
    DayNightCycle cycle;
    ForestWeather weather;
    HouseDoor door;

    Vector3 carTarget;
    Quaternion carTargetRotation;
    Vector3 carTargetVelocity;
    bool carTargetValid;
    float nextCarSend;
    float nextItemSend;
    float nextWorldSend;
    Rigidbody released;
    float releasedUntil;
    Rigidbody lastHeld;

    public override void OnNetworkSpawn()
    {
        Instance = this;
        car = FindAnyObjectByType<VehicleController>();
        carBody = car.GetComponent<Rigidbody>();

        // Подключившийся позже просит то, что мир не может построить сам: снятые детали и слитые баки.
        if (!IsServer)
            RequestWorldStateRpc();
    }

    [Rpc(SendTo.Server)]
    void RequestWorldStateRpc()
    {
        foreach (KeyValuePair<string, AbandonedCar.State> pair in AbandonedCar.States)
            WreckRpc(pair.Key, pair.Value.Liters, pair.Value.TakenMask);
        foreach (KeyValuePair<CarSlot, CarPartKind> pair in CarUpgrades.Instance.Mounted)
            MountedRpc((int)pair.Key, true, (int)pair.Value);
        foreach (CarPartItem item in FindObjectsByType<CarPartItem>(FindObjectsSortMode.None))
            ItemCreatedRpc(item.name, item.transform.position, item.transform.rotation);
    }

    public void AnnounceWreck(string id, float liters, int takenMask)
    {
        if (HasClients)
            WreckRpc(id, liters, takenMask);
    }

    [Rpc(SendTo.NotMe)]
    void WreckRpc(FixedString64Bytes id, float liters, int takenMask)
    {
        AbandonedCar.ApplyState(id.ToString(), liters, takenMask);
    }

    public void AnnounceMounted(CarSlot slot, bool present, CarPartKind kind)
    {
        if (HasClients)
            MountedRpc((int)slot, present, (int)kind);
    }

    [Rpc(SendTo.NotMe)]
    void MountedRpc(int slot, bool present, int kind)
    {
        CarUpgrades.Instance.SetFromNetwork((CarSlot)slot, present, (CarPartKind)kind);
    }

    public void AnnounceItemCreated(string itemName, Vector3 position, Quaternion rotation)
    {
        if (HasClients)
            ItemCreatedRpc(itemName, position, rotation);
    }

    [Rpc(SendTo.NotMe)]
    void ItemCreatedRpc(FixedString64Bytes itemName, Vector3 position, Quaternion rotation)
    {
        string key = itemName.ToString();
        if (TryGetItem(key, out _) || !CarPartItem.TryParseKind(key, out CarPartKind kind))
            return;
        GameObject created = CarPartItem.Create(key, kind, position, rotation);
        items[key] = created.GetComponent<Rigidbody>();
        OptimizedForestGenerator forest = FindAnyObjectByType<OptimizedForestGenerator>();
        if (forest != null)
            forest.Track(created.GetComponent<Rigidbody>());
    }

    public void AnnounceItemRemoved(string itemName)
    {
        if (HasClients)
            ItemRemovedRpc(itemName);
    }

    [Rpc(SendTo.NotMe)]
    void ItemRemovedRpc(FixedString64Bytes itemName)
    {
        string key = itemName.ToString();
        if (TryGetItem(key, out Rigidbody body))
            Destroy(body.gameObject);
        items.Remove(key);
    }

    public override void OnNetworkDespawn()
    {
        if (Instance == this)
            Instance = null;
    }

    bool SimulatesCar()
    {
        return driverId.Value == NoDriver ? IsServer : driverId.Value == NetworkManager.LocalClientId;
    }

    // Посадка видна всем сразу: занято место или нет, решает только этот идентификатор.
    public void SetLocalDriver(bool driving)
    {
        if (IsSpawned)
            RequestDriverRpc(driving);
    }

    [Rpc(SendTo.Server)]
    void RequestDriverRpc(bool driving, RpcParams rpc = default)
    {
        ulong sender = rpc.Receive.SenderClientId;
        if (driving && driverId.Value == NoDriver)
            driverId.Value = sender;
        else if (!driving && driverId.Value == sender)
            driverId.Value = NoDriver;
    }

    public void RequestDoor(bool open)
    {
        if (IsSpawned)
            RequestDoorRpc(open);
    }

    [Rpc(SendTo.Server)]
    void RequestDoorRpc(bool open)
    {
        doorOpen.Value = open;
    }

    void UpdateDoor()
    {
        if (door == null)
            door = FindAnyObjectByType<HouseDoor>();
        if (door != null && door.IsOpen != doorOpen.Value)
            door.SetOpen(doorOpen.Value);
    }

    public void AnnounceFuelAdded(float liters)
    {
        if (HasClients)
            FuelAddedRpc(liters);
    }

    [Rpc(SendTo.NotMe)]
    void FuelAddedRpc(float liters)
    {
        car.AddFuelFromNetwork(liters);
    }

    public void AnnounceRecipe(string itemName, string recipe)
    {
        if (HasClients)
            ItemTakenRpc(itemName, recipe);
    }

    [Rpc(SendTo.NotMe)]
    void ItemTakenRpc(FixedString64Bytes itemName, FixedString64Bytes recipe)
    {
        if (TryGetItem(itemName.ToString(), out Rigidbody body))
            Destroy(body.gameObject);
        items.Remove(itemName.ToString());
        FindAnyObjectByType<BlueprintNotebook>().AddRecipe(recipe.ToString());
    }

    void Update()
    {
        if (!IsSpawned)
            return;

        UpdateCar();
        UpdateHeldItems();
        UpdateFollowedItems();
        UpdateWorldState();
        UpdateDoor();
    }

    void UpdateCar()
    {
        bool local = SimulatesCar();
        car.RemoteDriven = driverId.Value != NoDriver && driverId.Value != NetworkManager.LocalClientId;

        if (local)
        {
            // Машина вернулась под свою физику. Дальше её замораживает только лес, если она далеко.
            if (OptimizedForestGenerator.NetworkFollowed.Remove(carBody))
                carBody.isKinematic = false;
            carTargetValid = false;
            if (HasClients && Time.unscaledTime >= nextCarSend)
            {
                nextCarSend = Time.unscaledTime + CarSendPeriod;
                CarStateRpc(carBody.position, carBody.rotation, carBody.linearVelocity, car.Throttle, car.SteerInput, car.FuelLiters, car.LightsOn, car.HandbrakeOn, car.WiperMode, car.EngineOn, car.Condition, car.Battery);
            }
            return;
        }

        OptimizedForestGenerator.NetworkFollowed.Add(carBody);
        if (!carBody.isKinematic)
            carBody.isKinematic = true;

        if (!carTargetValid)
            return;

        // Между пакетами машина идёт по последней скорости: так она не дёргается на 20 пакетах в секунду.
        carTarget += carTargetVelocity * Time.deltaTime;
        float blend = 1f - Mathf.Exp(-FollowResponse * Time.deltaTime);
        bool far = (carBody.position - carTarget).sqrMagnitude > CarSnapDistance * CarSnapDistance;
        Vector3 position = far ? carTarget : Vector3.Lerp(carBody.position, carTarget, blend);
        Quaternion rotation = far ? carTargetRotation : Quaternion.Slerp(carBody.rotation, carTargetRotation, blend);
        carBody.position = position;
        carBody.rotation = rotation;
        car.transform.SetPositionAndRotation(position, rotation);
    }

    [Rpc(SendTo.NotMe, Delivery = RpcDelivery.Unreliable)]
    void CarStateRpc(Vector3 position, Quaternion rotation, Vector3 velocity, float throttle, float steer, float fuel, bool lights, bool handbrake, int wipers, bool engine, float condition, float battery)
    {
        if (SimulatesCar())
            return;

        carTarget = position;
        carTargetRotation = rotation;
        carTargetVelocity = velocity;
        carTargetValid = true;
        car.SetDriverInput(new Vector2(steer, throttle));
        car.SetFuel(fuel);
        car.HandbrakeOn = handbrake;
        car.WiperMode = wipers;
        car.EngineOn = engine;
        car.Condition = condition;
        car.Battery = battery;
        if (car.LightsOn != lights)
            car.SetLights(lights);
    }

    // Предмет передаёт тот, у кого он в руках, и ещё немного после броска, пока летит.
    void UpdateHeldItems()
    {
        if (hands == null)
        {
            var seat = FindAnyObjectByType<PlayerSeat>();
            if (seat != null)
                hands = seat.GetComponent<PlayerHands>();
            if (hands == null)
                return;
        }

        Rigidbody held = hands.Held;
        if (held != lastHeld && lastHeld != null)
        {
            released = lastHeld;
            releasedUntil = Time.time + AfterReleaseSeconds;
        }
        lastHeld = held;

        if (!HasClients || Time.unscaledTime < nextItemSend)
            return;
        nextItemSend = Time.unscaledTime + ItemSendPeriod;

        if (held != null)
            SendItem(held);
        if (released != null)
        {
            if (Time.time < releasedUntil)
                SendItem(released);
            else
                released = null;
        }
    }

    public void SendItem(Rigidbody body)
    {
        if (!HasClients || body == null)
            return;
        float liters = body.TryGetComponent(out FuelCan can) ? can.Liters : -1f;
        ItemStateRpc(body.name, body.position, body.rotation, body.linearVelocity, liters);
    }

    [Rpc(SendTo.NotMe, Delivery = RpcDelivery.Unreliable)]
    void ItemStateRpc(FixedString64Bytes itemName, Vector3 position, Quaternion rotation, Vector3 velocity, float liters)
    {
        string key = itemName.ToString();
        if (!TryGetItem(key, out Rigidbody body) || body == (hands != null ? hands.Held : null))
            return;

        if (!followed.TryGetValue(key, out FollowedItem item))
        {
            item = new FollowedItem { Body = body };
            followed[key] = item;
        }
        item.Position = position;
        item.Rotation = rotation;
        item.Velocity = velocity;
        item.Until = Time.time + ItemFollowSeconds;
        if (body.TryGetComponent(out RoadBlock block))
            block.Free();
        OptimizedForestGenerator.NetworkFollowed.Add(body);
        if (!body.isKinematic)
            body.isKinematic = true;

        if (liters >= 0f && body.TryGetComponent(out FuelCan can))
            can.SetLiters(liters);
    }

    void UpdateFollowedItems()
    {
        float blend = 1f - Mathf.Exp(-FollowResponse * Time.deltaTime);
        foreach (KeyValuePair<string, FollowedItem> pair in followed)
        {
            FollowedItem item = pair.Value;
            if (item.Body == null)
            {
                finished.Add(pair.Key);
                continue;
            }

            item.Position += item.Velocity * Time.deltaTime;
            item.Body.position = Vector3.Lerp(item.Body.position, item.Position, blend);
            item.Body.rotation = Quaternion.Slerp(item.Body.rotation, item.Rotation, blend);

            // Владелец замолчал: дальше предмет падает сам, с той скоростью, что пришла последней.
            if (Time.time > item.Until)
            {
                OptimizedForestGenerator.NetworkFollowed.Remove(item.Body);
                item.Body.isKinematic = false;
                item.Body.linearVelocity = item.Velocity;
                finished.Add(pair.Key);
            }
        }

        foreach (string key in finished)
            followed.Remove(key);
        finished.Clear();
    }

    bool TryGetItem(string key, out Rigidbody body)
    {
        if (items.TryGetValue(key, out body) && body != null)
            return true;

        foreach (Rigidbody candidate in FindObjectsByType<Rigidbody>(FindObjectsSortMode.None))
        {
            if (candidate.CompareTag(InteractionRay.PickupableTag))
                items[candidate.name] = candidate;
        }
        return items.TryGetValue(key, out body) && body != null;
    }

    // Часы, погоду и задачу ведёт хост: иначе у игроков разошлись бы день и ночь.
    void UpdateWorldState()
    {
        if (cycle == null)
            cycle = FindAnyObjectByType<DayNightCycle>();
        if (weather == null)
            weather = FindAnyObjectByType<ForestWeather>();
        if (cycle == null || weather == null)
            return;

        if (IsServer)
        {
            if (Time.unscaledTime < nextWorldSend)
                return;
            nextWorldSend = Time.unscaledTime + WorldSendPeriod;
            timeOfDay.Value = cycle.TimeOfDay;
            raining.Value = weather.IsRaining;
            if (StoryDirector.Instance != null)
                storyStage.Value = (int)StoryDirector.Instance.Stage;
            return;
        }

        if (Mathf.Abs(Mathf.DeltaAngle(cycle.TimeOfDay * 360f, timeOfDay.Value * 360f)) / 360f > TimeTolerance)
            cycle.TimeOfDay = timeOfDay.Value;
        weather.SetFromNetwork(raining.Value);
        if (StoryDirector.Instance != null)
            StoryDirector.Instance.FollowHost((StoryStage)storyStage.Value);
    }
}
