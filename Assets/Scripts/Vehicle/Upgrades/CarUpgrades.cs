using System.Collections.Generic;
using UnityEngine;

// Модернизация нашей машины: в каждом гнезде одна деталь. Всё, что деталь даёт, идёт через настоящие
// параметры физики: массу, момент, радиус колёс и сцепление. На брошенных машинах такого нет.
[RequireComponent(typeof(VehicleController))]
public class CarUpgrades : MonoBehaviour
{
    const float BaseMass = 1500f;
    const float BaseRadius = 0.42f;
    const float UnmountReach = 1.5f;
    const float DamageSpeed = 5f;
    const float DamagePerSpeed = 0.018f;
    const float ArmorProtection = 0.06f;
    const float MaxProtection = 0.65f;
    const float StallDamage = 0.12f;
    const float SmokeCondition = 0.4f;
    const float RepairAmount = 0.45f;

    // Заводские детали уступают место новым в том же гнезде.
    static readonly Dictionary<CarSlot, string[]> StockParts = new Dictionary<CarSlot, string[]>
    {
        { CarSlot.Front, new[] { "BullBarPost", "BullBarSide", "BullBarLow", "BullBarTop", "BullBarMid", "TowHook", "SpotLamp", "SpotGlass" } },
        { CarSlot.Roof, new[] { "RackRail", "RackLeg", "RackBar", "Tarp", "TarpStrap", "RoofCan", "RoofCanHandle", "CanStrap", "ShovelHandle", "ShovelBlade" } },
        { CarSlot.Rear, new[] { "SpareMount" } }
    };

    static readonly Dictionary<CarSlot, Vector3> SlotPoints = new Dictionary<CarSlot, Vector3>
    {
        { CarSlot.Front, new Vector3(0f, -0.05f, 2.3f) },
        { CarSlot.Roof, new Vector3(0f, 1f, -0.1f) },
        { CarSlot.Sides, new Vector3(1f, -0.08f, -0.1f) },
        { CarSlot.Rear, new Vector3(0f, -0.15f, -2.22f) },
        { CarSlot.Wheels, new Vector3(0.95f, -0.35f, 1.4f) },
        { CarSlot.Engine, new Vector3(0f, 0.25f, 1.5f) },
        { CarSlot.Tank, new Vector3(0.3f, -0.56f, -0.9f) }
    };

    public static CarUpgrades Instance { get; private set; }

    public IReadOnlyDictionary<CarSlot, CarPartKind> Mounted => mounted;
    public float ArmorTotal { get; private set; }

    readonly Dictionary<CarSlot, CarPartKind> mounted = new Dictionary<CarSlot, CarPartKind>();
    readonly Dictionary<CarSlot, GameObject> models = new Dictionary<CarSlot, GameObject>();
    readonly List<GameObject> wheelLugs = new List<GameObject>();
    VehicleController car;
    Rigidbody body;
    ParticleSystem smoke;

    // Удар о твёрдое: скорость выше порога калечит машину, а броня забирает часть удара на себя.
    void OnCollisionEnter(Collision collision)
    {
        if (body.isKinematic || collision.collider.CompareTag(OptimizedForestGenerator.TerrainTag))
            return;
        float speed = collision.relativeVelocity.magnitude;
        if (speed < DamageSpeed || (collision.rigidbody != null && collision.rigidbody.mass < 100f && !collision.rigidbody.isKinematic))
            return;

        float protection = Mathf.Min(ArmorTotal * ArmorProtection, MaxProtection);
        float damage = (speed - DamageSpeed) * DamagePerSpeed * (1f - protection);
        car.Condition = Mathf.Max(0f, car.Condition - damage);
        if (damage > StallDamage && car.EngineOn)
        {
            car.EngineOn = false;
            ForestAudio.Play(ForestSound.Stall, 0.7f);
        }
    }

    public void Repair()
    {
        car.Condition = Mathf.Min(1f, car.Condition + RepairAmount);
    }

    void Update()
    {
        // Битый мотор парит из-под капота, пока работает.
        bool wanted = car.Condition < SmokeCondition && car.EngineRunning;
        if (wanted && smoke == null)
            smoke = BuildSmoke();
        if (smoke != null)
        {
            ParticleSystem.EmissionModule emission = smoke.emission;
            emission.rateOverTime = wanted ? Mathf.Lerp(16f, 5f, car.Condition / SmokeCondition) : 0f;
        }
    }

    ParticleSystem BuildSmoke()
    {
        var smokeObject = new GameObject("EngineSmoke");
        smokeObject.transform.SetParent(transform, false);
        smokeObject.transform.SetLocalPositionAndRotation(new Vector3(0f, 0.25f, 1.6f), Quaternion.Euler(-80f, 0f, 0f));
        var system = smokeObject.AddComponent<ParticleSystem>();
        system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        ParticleSystem.MainModule main = system.main;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startLifetime = 2.4f;
        main.startSpeed = 0.9f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.25f, 0.6f);
        main.startColor = new Color(0.55f, 0.57f, 0.58f, 0.16f);
        main.maxParticles = 60;
        ParticleSystem.ShapeModule shape = system.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(1.1f, 0.6f, 0.05f);
        smokeObject.GetComponent<ParticleSystemRenderer>().sharedMaterial = ForestAtmosphere.CreateParticleMaterial(0.4f);
        system.Play();
        return system;
    }

    void Awake()
    {
        Instance = this;
        car = GetComponent<VehicleController>();
        body = GetComponent<Rigidbody>();

        // У седана другие размеры, а детали крепятся к его кузову.
        if (SedanSkin.Available)
        {
            SlotPoints[CarSlot.Front] = new Vector3(0f, -0.2f, 2.5f);
            SlotPoints[CarSlot.Roof] = new Vector3(0f, 0.6f, -0.4f);
            SlotPoints[CarSlot.Sides] = new Vector3(0.95f, -0.25f, -0.1f);
            SlotPoints[CarSlot.Rear] = new Vector3(0f, -0.25f, -2.72f);
            SlotPoints[CarSlot.Wheels] = new Vector3(SedanSkin.Track, -0.35f, 1.4f);
            SlotPoints[CarSlot.Engine] = new Vector3(0f, 0.1f, 1.7f);
        }
    }

    public static Vector3 SlotPoint(CarSlot slot)
    {
        return SlotPoints[slot];
    }

    // Возвращает деталь, которую сняли, чтобы положить её рядом.
    public bool Mount(CarPartKind kind, out CarPartKind replaced)
    {
        CarSlot slot = CarParts.Info[kind].Slot;
        bool had = mounted.TryGetValue(slot, out replaced);
        mounted[slot] = kind;
        RebuildSlot(slot);
        Recalculate();
        return had;
    }

    public void SetFromNetwork(CarSlot slot, bool present, CarPartKind kind)
    {
        if (present)
            mounted[slot] = kind;
        else
            mounted.Remove(slot);
        RebuildSlot(slot);
        Recalculate();
    }

    public bool Unmount(CarSlot slot, out CarPartKind kind)
    {
        if (!mounted.TryGetValue(slot, out kind))
            return false;
        mounted.Remove(slot);
        RebuildSlot(slot);
        Recalculate();
        return true;
    }

    public void ClearAll()
    {
        var slots = new List<CarSlot>(mounted.Keys);
        mounted.Clear();
        foreach (CarSlot slot in slots)
            RebuildSlot(slot);
        Recalculate();
    }

    // Ближайшая к точке прицела установленная деталь.
    public bool FindNearest(Vector3 worldPoint, out CarSlot slot)
    {
        Vector3 local = transform.InverseTransformPoint(worldPoint);
        slot = default;
        float best = UnmountReach * UnmountReach;
        bool found = false;
        foreach (CarSlot candidate in mounted.Keys)
        {
            Vector3 point = SlotPoints[candidate];
            if (candidate == CarSlot.Sides || candidate == CarSlot.Wheels)
                point.x = Mathf.Sign(local.x) * Mathf.Abs(point.x);
            if (candidate == CarSlot.Wheels)
                point.z = Mathf.Sign(local.z) * Mathf.Abs(point.z);
            float distance = (point - local).sqrMagnitude;
            if (distance < best)
            {
                best = distance;
                slot = candidate;
                found = true;
            }
        }
        return found;
    }

    void RebuildSlot(CarSlot slot)
    {
        if (models.TryGetValue(slot, out GameObject old) && old != null)
            Destroy(old);
        models.Remove(slot);

        bool present = mounted.TryGetValue(slot, out CarPartKind kind);
        if (StockParts.TryGetValue(slot, out string[] names))
            SetStockVisible(names, !present);

        if (slot == CarSlot.Wheels)
        {
            ApplyWheels(present, kind);
            return;
        }
        if (!present || slot == CarSlot.Engine)
            return;

        var root = new GameObject("Mounted_" + slot).transform;
        root.SetParent(transform, false);
        models[slot] = root.gameObject;

        if (slot == CarSlot.Sides)
        {
            foreach (float side in new[] { -1f, 1f })
            {
                var plate = new GameObject("SidePlate").transform;
                plate.SetParent(root, false);
                plate.SetLocalPositionAndRotation(new Vector3(side * SlotPoints[slot].x, SlotPoints[slot].y, SlotPoints[slot].z), Quaternion.Euler(0f, side > 0f ? 0f : 180f, 0f));
                CarPartModels.Build(plate, kind, true);
            }
            return;
        }

        root.localPosition = SlotPoints[slot];
        CarPartModels.Build(root, kind, true);
        if (kind == CarPartKind.RoofLights)
            AddRoofLight(root);
    }

    void AddRoofLight(Transform root)
    {
        var lampObject = new GameObject("RoofBeam");
        lampObject.transform.SetParent(root, false);
        lampObject.transform.localPosition = new Vector3(0f, 0.17f, 0.2f);
        lampObject.transform.localRotation = Quaternion.Euler(8f, 0f, 0f);
        Light lamp = lampObject.AddComponent<Light>();
        lamp.type = LightType.Spot;
        lamp.range = 40f;
        lamp.spotAngle = 80f;
        lamp.intensity = 2.2f;
        lamp.color = new Color(1f, 0.9f, 0.72f);
        lamp.renderMode = LightRenderMode.ForcePixel;
        car.AddLight(lamp);
    }

    void SetStockVisible(string[] names, bool visible)
    {
        foreach (Renderer part in GetComponentsInChildren<Renderer>(true))
        {
            if (System.Array.IndexOf(names, part.name) >= 0)
                part.enabled = visible;
        }
    }

    void ApplyWheels(bool present, CarPartKind kind)
    {
        foreach (GameObject lug in wheelLugs)
            Destroy(lug);
        wheelLugs.Clear();

        float radius = BaseRadius + (present ? CarParts.Info[kind].WheelRadius : 0f);
        foreach (VehicleController.Wheel wheel in car.Wheels)
        {
            wheel.Collider.radius = radius;
            wheel.Visual.localScale = Vector3.one * (radius / BaseRadius);
            if (present && kind == CarPartKind.MudTyres)
            {
                var holder = new GameObject("Lugs");
                holder.transform.SetParent(wheel.Visual, false);
                CarPartModels.AddLugs(holder.transform, radius, 0.3f, ProceduralModels.Lit(new Color(0.02f, 0.02f, 0.02f), 0.3f, 0f, 0.1f));
                wheelLugs.Add(holder);
            }
        }
    }

    void Recalculate()
    {
        float mass = BaseMass, torque = 1f, speed = 1f, grip = 0f, tank = 0f, burn = 1f, armor = 0f;
        foreach (CarPartKind kind in mounted.Values)
        {
            CarPartInfo info = CarParts.Info[kind];
            mass += info.Mass;
            torque += info.Torque;
            speed += info.Speed;
            grip += info.MudGrip;
            tank += info.TankLiters;
            burn += info.Burn;
            armor += info.Armor;
        }
        GetComponent<CarLoad>().SetBaseMass(mass);
        ArmorTotal = armor;
        car.ApplyUpgrades(torque, speed, grip, tank, burn);
    }

}
