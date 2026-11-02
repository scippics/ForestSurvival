using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

// Одно сохранение в JSON: положение игрока, машины и предметов, время суток и рецепты.
// Лес в файл не пишется: он заново получается из координат чанков.
public static class SaveSystem
{
    const string FileName = "save.json";
    const int CurrentVersion = 1;

    [Serializable]
    class ItemState
    {
        public string name;
        public Vector3 position;
        public Quaternion rotation;
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
        public bool lowGear;
        public bool lightsOn;
        public List<string> recipes = new List<string>();
        public List<ItemState> items = new List<ItemState>();
    }

    static string SavePath => Path.Combine(Application.persistentDataPath, FileName);

    public static bool HasSave => TryRead(out _);

    public static string SaveDescription => TryRead(out SaveData data) ? data.savedAt : "";

    public static bool Save()
    {
        var player = UnityEngine.Object.FindAnyObjectByType<PlayerSeat>();
        var view = player.GetComponentInChildren<PlayerCamera>();
        var vehicle = UnityEngine.Object.FindAnyObjectByType<VehicleController>();

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
            lowGear = vehicle.isLowGear,
            lightsOn = vehicle.LightsOn,
            recipes = new List<string>(player.GetComponent<BlueprintNotebook>().unlockedRecipes)
        };

        foreach (Rigidbody body in UnityEngine.Object.FindObjectsByType<Rigidbody>(FindObjectsSortMode.None))
        {
            if (body.CompareTag(InteractionRay.PickupableTag))
                data.items.Add(new ItemState { name = body.name, position = body.position, rotation = body.rotation });
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
        vehicle.isLowGear = data.lowGear;
        vehicle.SetLights(data.lightsOn);

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
                PlaceBody(body, item.position, item.rotation);
            else
                UnityEngine.Object.Destroy(body.gameObject);
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
            return data != null && data.version == CurrentVersion;
        }
        catch (Exception exception)
        {
            Debug.LogWarning("Сохранение не прочитано: " + exception.Message);
            return false;
        }
    }
}
