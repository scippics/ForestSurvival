using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

// Одно сохранение в JSON: положение игрока, машины и предметов, время суток и рецепты.
// Лес в файл не пишется: он заново получается из координат чанков.
public static class SaveSystem
{
    const string FileName = "save.json";
    const int CurrentVersion = 3;
    const int OldestReadable = 2;

    [Serializable]
    class ItemState
    {
        public string name;
        public Vector3 position;
        public Quaternion rotation;
        public float liters;
        public bool moved;
    }

    [Serializable]
    class WreckState
    {
        public string id;
        public float liters;
        public int takenMask;
    }

    [Serializable]
    class SaveData
    {
        public int version;
        public string savedAt;
        public float timeOfDay;
        public Vector3 playerPosition;
        public float playerYaw;
        public float playerPitch;
        public bool flashlightOn;
        public bool seated;
        public Vector3 vehiclePosition;
        public Quaternion vehicleRotation;
        public bool lightsOn;
        public bool handbrakeOn;
        public float fuelLiters;
        public int storyStage;
        public bool campNoteRead;
        public List<Vector2Int> visitedCamps = new List<Vector2Int>();
        public List<string> recipes = new List<string>();
        public List<ItemState> items = new List<ItemState>();
        public List<WreckState> wrecks = new List<WreckState>();
        public List<int> mountedParts = new List<int>();
        public List<Vector2Int> visitedBlocks = new List<Vector2Int>();
    }

    static string SavePath => Path.Combine(Application.persistentDataPath, FileName);

    public static bool HasSave => TryRead(out _);

    public static string SaveDescription => TryRead(out SaveData data) ? data.savedAt : "";

    public static bool Save()
    {
        var player = UnityEngine.Object.FindAnyObjectByType<PlayerSeat>();
        var view = player.GetComponentInChildren<PlayerCamera>();
        var vehicle = UnityEngine.Object.FindAnyObjectByType<VehicleController>();
        var forest = UnityEngine.Object.FindAnyObjectByType<OptimizedForestGenerator>();
        var story = UnityEngine.Object.FindAnyObjectByType<StoryDirector>();

        var data = new SaveData
        {
            version = CurrentVersion,
            savedAt = DateTime.Now.ToString("dd.MM.yyyy HH:mm"),
            timeOfDay = UnityEngine.Object.FindAnyObjectByType<DayNightCycle>().TimeOfDay,
            playerPosition = player.transform.position,
            playerYaw = view.Yaw,
            playerPitch = view.Pitch,
            flashlightOn = view.Flashlight.enabled,
            seated = player.IsSeated,
            vehiclePosition = vehicle.transform.position,
            vehicleRotation = vehicle.transform.rotation,
            lightsOn = vehicle.LightsOn,
            handbrakeOn = vehicle.HandbrakeOn,
            fuelLiters = vehicle.FuelLiters,
            storyStage = (int)story.Stage,
            campNoteRead = story.CampNoteRead,
            visitedCamps = forest.VisitedCamps,
            visitedBlocks = forest.VisitedBlocks,
            recipes = new List<string>(player.GetComponent<BlueprintNotebook>().unlockedRecipes)
        };

        foreach (KeyValuePair<string, AbandonedCar.State> wreck in AbandonedCar.States)
            data.wrecks.Add(new WreckState { id = wreck.Key, liters = wreck.Value.Liters, takenMask = wreck.Value.TakenMask });
        foreach (CarPartKind kind in vehicle.GetComponent<CarUpgrades>().Mounted.Values)
            data.mountedParts.Add((int)kind);

        foreach (Rigidbody body in UnityEngine.Object.FindObjectsByType<Rigidbody>(FindObjectsSortMode.None))
        {
            if (body.CompareTag(InteractionRay.PickupableTag))
                data.items.Add(new ItemState { name = body.name, position = body.position, rotation = body.rotation, liters = body.TryGetComponent(out FuelCan can) ? can.Liters : 0f, moved = body.TryGetComponent(out RoadBlock block) && !block.IsLodged });
        }

        try
        {
            // Через временный файл: сбой при записи не должен испортить прошлое сохранение.
            string temporary = SavePath + ".tmp";
            File.WriteAllText(temporary, JsonUtility.ToJson(data, true));
            if (File.Exists(SavePath))
                File.Delete(SavePath);
            File.Move(temporary, SavePath);
            return true;
        }
        catch (Exception exception)
        {
            Debug.LogError("Не удалось сохранить игру: " + exception.Message);
            return false;
        }
    }

    public static bool Load()
    {
        if (!TryRead(out SaveData data))
            return false;

        var player = UnityEngine.Object.FindAnyObjectByType<PlayerSeat>();
        var view = player.GetComponentInChildren<PlayerCamera>();
        var vehicle = UnityEngine.Object.FindAnyObjectByType<VehicleController>();

        UnityEngine.Object.FindAnyObjectByType<DayNightCycle>().TimeOfDay = data.timeOfDay;

        PlaceBody(vehicle.GetComponent<Rigidbody>(), data.vehiclePosition, data.vehicleRotation);
        vehicle.SetLights(data.lightsOn);
        vehicle.HandbrakeOn = data.handbrakeOn;

        // Сначала детали: от них зависит размер бака.
        var upgrades = vehicle.GetComponent<CarUpgrades>();
        upgrades.ClearAll();
        foreach (int kind in data.mountedParts)
            upgrades.Mount((CarPartKind)kind, out _);
        vehicle.SetFuel(data.fuelLiters);

        foreach (WreckState wreck in data.wrecks)
            AbandonedCar.ApplyState(wreck.id, wreck.liters, wreck.takenMask);

        UnityEngine.Object.FindAnyObjectByType<StoryDirector>().Resume((StoryStage)data.storyStage, data.campNoteRead);

        var notebook = player.GetComponent<BlueprintNotebook>();
        notebook.unlockedRecipes = new List<string>(data.recipes);

        var saved = new Dictionary<string, ItemState>();
        foreach (ItemState item in data.items)
            saved[item.name] = item;

        foreach (Rigidbody body in UnityEngine.Object.FindObjectsByType<Rigidbody>(FindObjectsSortMode.None))
        {
            if (!body.CompareTag(InteractionRay.PickupableTag))
                continue;

            // Предмета нет в файле — значит, его уже забрали, как чертёж.
            if (saved.TryGetValue(body.name, out ItemState item))
            {
                PlaceBody(body, item.position, item.rotation);
                if (body.TryGetComponent(out FuelCan can))
                    can.SetLiters(item.liters);
                if (item.moved && body.TryGetComponent(out RoadBlock block))
                    block.Free();
                saved.Remove(body.name);
            }
            else
            {
                UnityEngine.Object.Destroy(body.gameObject);
            }
        }

        // Лут стоянок появляется, только когда игрок к ним подходит, поэтому после запуска
        // его ещё нет на месте: всё, что осталось в словаре, создаётся заново.
        var forest = UnityEngine.Object.FindAnyObjectByType<OptimizedForestGenerator>();
        forest.VisitedCamps = data.visitedCamps ?? new List<Vector2Int>();
        forest.VisitedBlocks = data.visitedBlocks ?? new List<Vector2Int>();
        foreach (ItemState item in saved.Values)
        {
            if (RoadBlock.TryParseKind(item.name, out int blockKind))
            {
                GameObject fallen = RoadBlock.Create(item.name, blockKind, item.position, item.rotation);
                if (item.moved)
                    fallen.GetComponent<RoadBlock>().Free();
                continue;
            }

            if (CarPartItem.TryParseKind(item.name, out CarPartKind partKind))
            {
                GameObject part = CarPartItem.Create(item.name, partKind, item.position, item.rotation);
                forest.Track(part.GetComponent<Rigidbody>());
                continue;
            }

            if (!item.name.StartsWith(ProceduralModels.LootPrefix))
                continue;

            GameObject loot = ProceduralModels.CreateLoot(item.name, item.position, item.rotation);
            forest.Track(loot.GetComponent<Rigidbody>());
            if (loot.TryGetComponent(out FuelCan can))
                can.SetLiters(item.liters);
        }

        view.Flashlight.enabled = data.flashlightOn;

        if (data.seated)
        {
            player.Enter(vehicle);
            view.SetLook(data.playerYaw, data.playerPitch);
        }
        else
        {
            PlaceBody(player.GetComponent<Rigidbody>(), data.playerPosition, Quaternion.identity);
            view.SetLook(data.playerYaw, data.playerPitch);
        }
        return true;
    }

    static void PlaceBody(Rigidbody body, Vector3 position, Quaternion rotation)
    {
        body.position = position;
        body.rotation = rotation;
        body.transform.SetPositionAndRotation(position, rotation);
        if (!body.isKinematic)
        {
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
        }
    }

    static bool TryRead(out SaveData data)
    {
        data = null;
        try
        {
            if (!File.Exists(SavePath))
                return false;
            data = JsonUtility.FromJson<SaveData>(File.ReadAllText(SavePath));
            return data != null && data.version >= OldestReadable && data.version <= CurrentVersion;
        }
        catch (Exception exception)
        {
            Debug.LogWarning("Сохранение не прочитано: " + exception.Message);
            return false;
        }
    }
}
