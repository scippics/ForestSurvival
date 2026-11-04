using UnityEngine;
using UnityEngine.UI;

// Вешается на камеру: точка прицела, а под взглядом на предмет вместо неё значок клавиши, одно слово о действии
// и кайма на предмете. С предметом в руках кайма заранее показывает, куда его нести.
public class InteractionUI : MonoBehaviour
{
    public const string KeyGrab = "ЛКМ";
    public const string KeyInteract = "E";
    public const string KeyPush = "F";
    public const string KeyWrench = "G";
    public const string KeyRecover = "R";
    const string DoorPartName = "Door";
    const char KeySeparator = ' ';
    const string HoodPartName = "Hood";

    const int ReferenceHeight = 1080;
    const int DotTextureSize = 64;
    const float DotSize = 5f;
    const float DotAlpha = 0.55f;

    const int CapTextureSize = 32;
    const int CapCorner = 10;
    const float CapHeight = 34f;
    const float CapWidth = 36f;
    const float CapWideWidth = 60f;
    const float CapGap = 8f;
    const int CapFontSize = 19;
    const int ActionFontSize = 17;
    const float ActionOffset = 34f;
    const float HintRadius = 8f;
    const float HintPeriod = 0.4f;
    static readonly Color CapColor = new Color(0.04f, 0.04f, 0.04f, 0.62f);
    static readonly Color CapTextColor = new Color(0.94f, 0.92f, 0.84f);

    // Один значок или два через пробел: "E" или "E G".
    public string CurrentKey { get; private set; } = "";
    public string CurrentAction { get; private set; } = "";
    public Transform CurrentHint { get; private set; }
    public bool DotVisible => dot.enabled;

    PlayerHands hands;
    PlayerSeat seat;
    PlayerWrench wrench;
    ObjectOutline outline;
    Image dot;
    readonly Image[] caps = new Image[2];
    readonly Text[] capTexts = new Text[2];
    Text actionText;
    float nextHint;
    string hintPart;
    string shownKey = "";
    string shownAction = "";

    string action = "";
    Transform outlined;
    string outlinedPart;

    void Awake()
    {
        hands = GetComponentInParent<PlayerHands>();
        seat = GetComponentInParent<PlayerSeat>();
        wrench = GetComponentInParent<PlayerWrench>();
        outline = gameObject.AddComponent<ObjectOutline>();
        CreateCanvas();
    }

    void CreateCanvas()
    {
        var canvasObject = new GameObject("CrosshairCanvas");
        canvasObject.AddComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;

        var dotObject = new GameObject("CrosshairDot");
        dotObject.transform.SetParent(canvasObject.transform, false);
        dot = dotObject.AddComponent<Image>();
        dot.sprite = CreateCircleSprite();
        dot.raycastTarget = false;
        dot.color = new Color(1f, 1f, 1f, DotAlpha);
        Center(dot.rectTransform);

        Sprite capSprite = CreateCapSprite();
        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        for (int i = 0; i < caps.Length; i++)
        {
            var capObject = new GameObject("KeyCap");
            capObject.transform.SetParent(canvasObject.transform, false);
            caps[i] = capObject.AddComponent<Image>();
            caps[i].sprite = capSprite;
            caps[i].type = Image.Type.Sliced;
            caps[i].color = CapColor;
            caps[i].raycastTarget = false;
            Center(caps[i].rectTransform);

            var textObject = new GameObject("KeyName");
            textObject.transform.SetParent(capObject.transform, false);
            capTexts[i] = textObject.AddComponent<Text>();
            capTexts[i].font = font;
            capTexts[i].fontStyle = FontStyle.Bold;
            capTexts[i].color = CapTextColor;
            capTexts[i].alignment = TextAnchor.MiddleCenter;
            capTexts[i].horizontalOverflow = HorizontalWrapMode.Overflow;
            capTexts[i].verticalOverflow = VerticalWrapMode.Overflow;
            capTexts[i].raycastTarget = false;
            RectTransform rect = capTexts[i].rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            capObject.SetActive(false);
        }

        var actionObject = new GameObject("KeyAction");
        actionObject.transform.SetParent(canvasObject.transform, false);
        actionText = actionObject.AddComponent<Text>();
        actionText.font = font;
        actionText.color = CapTextColor;
        actionText.alignment = TextAnchor.MiddleCenter;
        actionText.horizontalOverflow = HorizontalWrapMode.Overflow;
        actionText.verticalOverflow = VerticalWrapMode.Overflow;
        actionText.raycastTarget = false;
        var shadow = actionObject.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.85f);
        shadow.effectDistance = new Vector2(1f, -1f);
        Center(actionText.rectTransform);
    }

    static void Center(RectTransform rect)
    {
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
    }

    static Sprite CreateCircleSprite()
    {
        var texture = new Texture2D(DotTextureSize, DotTextureSize, TextureFormat.RGBA32, false);
        float center = (DotTextureSize - 1) * 0.5f;
        for (int y = 0; y < DotTextureSize; y++)
        {
            for (int x = 0; x < DotTextureSize; x++)
            {
                float distance = Mathf.Sqrt((x - center) * (x - center) + (y - center) * (y - center));
                texture.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(center - distance)));
            }
        }
        texture.Apply();
        return Sprite.Create(texture, new Rect(0f, 0f, DotTextureSize, DotTextureSize), Vector2.one * 0.5f);
    }

    // Скруглённая плашка: углы растягиваются отдельно от середины, поэтому широкая клавиша выглядит так же.
    static Sprite CreateCapSprite()
    {
        var texture = new Texture2D(CapTextureSize, CapTextureSize, TextureFormat.RGBA32, false);
        float half = (CapTextureSize - 1) * 0.5f;
        float inner = half - CapCorner;
        for (int y = 0; y < CapTextureSize; y++)
        {
            for (int x = 0; x < CapTextureSize; x++)
            {
                float dx = Mathf.Max(Mathf.Abs(x - half) - inner, 0f);
                float dy = Mathf.Max(Mathf.Abs(y - half) - inner, 0f);
                float edge = CapCorner - Mathf.Sqrt(dx * dx + dy * dy);
                texture.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(edge)));
            }
        }
        texture.Apply();
        return Sprite.Create(texture, new Rect(0f, 0f, CapTextureSize, CapTextureSize), Vector2.one * 0.5f, 100f, 0, SpriteMeshType.FullRect, Vector4.one * (CapCorner + 1));
    }

    void Update()
    {
        if (GameFlow.State != GameState.Playing)
        {
            CurrentKey = "";
            CurrentAction = "";
            outline.Show(null);
            dot.enabled = false;
            DrawKeys();
            return;
        }

        outlined = null;
        outlinedPart = null;
        bool aimed = InteractionRay.Cast(transform, out RaycastHit hit);
        action = "";
        CurrentKey = ResolveKey(aimed, hit);
        CurrentAction = action.Length > 0 ? action : HeldReadout();

        // Нет своей цели под прицелом: кайма показывает, куда нести то, что в руках.
        if (outlined == null)
        {
            UpdateHint();
            outlined = CurrentHint;
            outlinedPart = hintPart;
        }
        outline.Show(outlined, outlinedPart);

        // За рулём точка не нужна: центр экрана остаётся свободным для дороги.
        dot.enabled = !seat.IsSeated && CurrentKey.Length == 0;
        dot.rectTransform.sizeDelta = Vector2.one * (DotSize * Screen.height / ReferenceHeight);
        DrawKeys();
    }

    // Сколько бензина под рукой: видно всё время, пока канистра или вставленный шланг в руках.
    string HeldReadout()
    {
        if (hands.Held == null)
            return "";
        if (hands.Held.TryGetComponent(out FuelCan can))
            return "Канистра: " + can.Percent + "%";
        if (hands.HeldSiphon != null && hands.HeldSiphon.Source != null)
            return "В баке: " + Mathf.RoundToInt(hands.HeldSiphon.Source.Liters) + " л";
        return "";
    }

    void UpdateHint()
    {
        if (Time.time < nextHint)
            return;
        nextHint = Time.time + HintPeriod;
        CurrentHint = null;
        hintPart = null;

        Rigidbody held = hands.Held;
        if (seat.IsSeated || held == null || hands.IsDragging)
            return;

        Vector3 here = transform.position;
        if (hands.HeldSiphon != null)
        {
            if (hands.HeldSiphon.Source != null)
                CurrentHint = NearestCan(here);
            else
                CurrentHint = NearestFiller(here);
            return;
        }

        VehicleController car = FindAnyObjectByType<VehicleController>();
        if (car == null || Vector3.Distance(here, car.transform.position) > HintRadius)
            return;

        if (held.TryGetComponent(out FuelCan can))
        {
            if (can.Liters > 0f && car.FuelLiters < car.TankSize)
                CurrentHint = car.FillerCap;
        }
        else if (held.TryGetComponent(out CarPartItem _) || held.TryGetComponent(out CarBatteryItem _) || held.TryGetComponent(out RepairKit _))
        {
            CurrentHint = car.transform;
            hintPart = HoodPartName;
        }
    }

    Transform NearestFiller(Vector3 here)
    {
        Transform best = null;
        float bestDistance = HintRadius;
        foreach (AbandonedCar wreck in FindObjectsByType<AbandonedCar>(FindObjectsSortMode.None))
        {
            float distance = Vector3.Distance(here, wreck.FillerPoint);
            if (!wreck.IsDry && distance < bestDistance)
            {
                best = wreck.Filler;
                bestDistance = distance;
            }
        }
        return best;
    }

    Transform NearestCan(Vector3 here)
    {
        Transform best = null;
        float bestDistance = HintRadius;
        foreach (FuelCan can in FindObjectsByType<FuelCan>(FindObjectsSortMode.None))
        {
            float distance = Vector3.Distance(here, can.transform.position);
            if (!can.IsFull && hands.HeldSiphon.CanReach(can) && distance < bestDistance)
            {
                best = can.transform;
                bestDistance = distance;
            }
        }
        return best;
    }

    string ResolveKey(bool aimed, RaycastHit hit)
    {
        string basic = ResolveBasic(aimed, hit);
        if (!wrench.HasJob)
            return basic;

        if (wrench.JobTarget != null)
            outlined = wrench.JobTarget;

        // С деталью в руках дело одно: ключ. С пустыми руками у машины можно и сесть, и снять деталь.
        if (hands.Held != null || basic.Length == 0)
        {
            action = wrench.JobName;
            return KeyWrench;
        }
        action += " / " + wrench.JobName;
        return basic + KeySeparator + KeyWrench;
    }

    string ResolveBasic(bool aimed, RaycastHit hit)
    {
        if (seat.IsSeated)
        {
            if (seat.AimingAtKey)
            {
                outlined = seat.Vehicle.IgnitionKey;
                action = seat.Vehicle.EngineOn ? "Заглушить" : "Держать: завести";
                return KeyInteract;
            }
            if (!seat.Vehicle.IsOverturned)
                return "";
            action = "Поставить на колёса";
            return KeyRecover;
        }

        // Записка закрывается тем же кликом, которым открылась, а варианты ответа уже на экране.
        if (StoryUI.Instance.NoteOpen || NpcTalk.Current != null)
            return "";

        if (hands.HeldSiphon != null && hands.HeldSiphon.IsPumping)
            return "";

        // У машины можно и толкать, и заливать: с канистрой в руках важнее бак.
        if (aimed && hands.Held != null && hands.Held.TryGetComponent(out FuelCan heldCan) && heldCan.Liters > 0f
            && InteractionRay.IsFreeVehicle(hit, out VehicleController car) && car.FuelLiters < car.TankSize)
        {
            outlined = car.FillerCap;
            if (Vector3.Distance(transform.position, car.FillerPoint) > PlayerSeat.PourReach)
                return "";
            action = "Держать: залить (" + heldCan.Percent + "%)";
            return KeyInteract;
        }

        // Луч из зоны толкания не может попасть в её триггер: начало луча внутри него.
        // Поэтому значок опирается на сам факт нахождения в зоне, где F и работает.
        if (seat.PushZone != null)
        {
            action = "Толкать";
            return KeyPush;
        }

        if (!aimed)
            return "";

        if (InteractionRay.IsFiller(hit, out AbandonedCar tank) && hands.HeldSiphon != null && hands.HeldSiphon.Source == null)
        {
            outlined = hit.collider.transform;
            if (tank.CapIsOpen && tank.IsDry)
            {
                action = "Бак сухой";
                return "";
            }
            action = "Вставить шланг";
            return KeyGrab;
        }
        if (hands.Held == null && (InteractionRay.IsPickupable(hit) || InteractionRay.IsNpc(hit, out _) || InteractionRay.IsDoor(hit, out _)))
        {
            outlined = hit.rigidbody.transform;
            action = GrabWord(hit);
            return KeyGrab;
        }
        if (InteractionRay.IsFuelTransfer(hands.HeldSiphon, hit))
        {
            outlined = hit.rigidbody.transform;
            action = "Держать: подсос (" + hit.rigidbody.GetComponent<FuelCan>().Percent + "%)";
            return KeyGrab;
        }
        if (InteractionRay.IsCar(hit, out VehicleController boarding) && seat.ChooseSeat(boarding) != PlayerSeat.NoSeat)
        {
            outlined = boarding.transform;
            outlinedPart = DoorPartName;
            action = "Сесть";
            return KeyInteract;
        }
        return "";
    }

    static string GrabWord(RaycastHit hit)
    {
        if (InteractionRay.IsNpc(hit, out _))
            return "Поговорить";
        if (InteractionRay.IsDoor(hit, out HouseDoor door))
            return door.IsOpen ? "Закрыть" : "Открыть";

        Rigidbody body = hit.rigidbody;
        if (body.TryGetComponent(out NoteItem _))
            return "Читать";
        if (body.TryGetComponent(out RoadBlock _))
            return "Оттащить";
        if (body.TryGetComponent(out FuelCan can))
            return "Взять (" + can.Percent + "%)";
        return "Взять";
    }

    void DrawKeys()
    {
        if (CurrentKey == shownKey && CurrentAction == shownAction && Time.frameCount % 30 != 0)
            return;
        shownKey = CurrentKey;
        shownAction = CurrentAction;

        string[] keys = CurrentKey.Length == 0 ? System.Array.Empty<string>() : CurrentKey.Split(KeySeparator);
        float scale = Screen.height / (float)ReferenceHeight;

        actionText.text = CurrentAction;
        actionText.fontSize = Mathf.RoundToInt(ActionFontSize * scale);
        actionText.rectTransform.anchoredPosition = new Vector2(0f, -ActionOffset * scale);

        float total = -CapGap;
        foreach (string key in keys)
            total += WidthOf(key) + CapGap;

        float left = -total * 0.5f;
        for (int i = 0; i < caps.Length; i++)
        {
            bool used = i < keys.Length;
            caps[i].gameObject.SetActive(used);
            if (!used)
                continue;

            float width = WidthOf(keys[i]);
            caps[i].rectTransform.sizeDelta = new Vector2(width, CapHeight) * scale;
            caps[i].rectTransform.anchoredPosition = new Vector2((left + width * 0.5f) * scale, 0f);
            capTexts[i].text = keys[i];
            capTexts[i].fontSize = Mathf.RoundToInt(CapFontSize * scale);
            left += width + CapGap;
        }
    }

    static float WidthOf(string key)
    {
        return key.Length > 1 ? CapWideWidth : CapWidth;
    }
}
