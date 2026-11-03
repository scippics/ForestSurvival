using UnityEngine;
using UnityEngine.UI;

// Вешается на камеру: точка прицела и контекстная подсказка под ней.
public class InteractionUI : MonoBehaviour
{
    const string PromptTake = "ЛКМ - Взять";
    const string PromptPush = "F - Толкать машину";
    const string PromptTransfer = "Зажать ЛКМ - Подсосать бензин";
    const string PromptHolding = "ЛКМ - Отпустить, ПКМ - Бросить";
    const string PromptRead = "ЛКМ - Читать";
    const string PromptDoorOpen = "ЛКМ - Открыть дверь";
    const string PromptDoorClose = "ЛКМ - Закрыть дверь";
    const string PromptTalk = "ЛКМ - Поговорить";
    const string PromptCloseNote = "ЛКМ - Убрать записку";
    const string PromptPour = "Зажать E - Залить в бак: ";
    const string PromptNoFuel = "Бак пуст: мотор не заведётся\n";
    const string PromptDrag = "ЛКМ - Оттащить с дороги";
    const string PromptCells = "ЛКМ - Взять батарейки";
    const string PromptInsert = "ЛКМ - Вставить шланг в бак";
    const string PromptNeedHose = "Горловина бака: нужен шланг";
    const string PromptTooFar = "Шланг не достаёт до канистры";
    const string PromptFiller = "Подойди к горловине бака: сзади слева";
    const string PromptEnter = "E - Сесть за руль";
    const string PromptDriving = "E - Выйти, L - Фары, V - Дворники: ";
    const string PromptHandbrake = ", Пробел - Ручник: ";
    const string PromptOverturned = "Машина перевернулась! R - Поставить на колёса\n";

    static readonly string[] WiperNames = { "выкл", "редко", "часто" };

    const int ReferenceHeight = 1080;
    const float PromptOffset = 24f;
    const float PromptSize = 24f;
    const float DrivingPromptSize = 16f;
    const float DrivingPromptMargin = 20f;

    const int DotTextureSize = 64;
    const float DotIdleSize = 5f;
    const float DotActiveSize = 22f;
    const float DotIdleAlpha = 0.55f;
    const float DotActiveAlpha = 0.3f;
    const float DotResponse = 16f;

    public string CurrentPrompt { get; private set; } = "";
    public bool IsAimingAtInteractable { get; private set; }
    public float DotPixelSize => dot.rectTransform.sizeDelta.x;
    public float DotAlpha => dot.color.a;
    public bool DotVisible => dot.enabled;

    PlayerHands hands;
    PlayerSeat seat;
    PlayerWrench wrench;
    GUIStyle promptStyle;
    Image dot;
    float dotOpenness;

    void Awake()
    {
        hands = GetComponentInParent<PlayerHands>();
        seat = GetComponentInParent<PlayerSeat>();
        wrench = GetComponentInParent<PlayerWrench>();
        CreateDot();
    }

    void CreateDot()
    {
        var canvasObject = new GameObject("CrosshairCanvas");
        canvasObject.AddComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;

        var dotObject = new GameObject("CrosshairDot");
        dotObject.transform.SetParent(canvasObject.transform, false);
        dot = dotObject.AddComponent<Image>();
        dot.sprite = CreateCircleSprite();
        dot.raycastTarget = false;
        dot.color = new Color(1f, 1f, 1f, DotIdleAlpha);

        RectTransform rect = dot.rectTransform;
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

    void Update()
    {
        if (GameFlow.State != GameState.Playing)
        {
            dot.enabled = false;
            CurrentPrompt = "";
            return;
        }

        bool aimed = InteractionRay.Cast(transform, out RaycastHit hit);
        CurrentPrompt = ResolvePrompt(aimed, hit);

        IsAimingAtInteractable = !seat.IsSeated
            && (seat.PushZone != null || (aimed && InteractionRay.IsInteractable(hit, hands.Held != null, hands.HeldSiphon)));
        UpdateDot();
    }

    void UpdateDot()
    {
        // За рулём точка не нужна: центр экрана остаётся свободным для дороги.
        dot.enabled = !seat.IsSeated;

        dotOpenness = Mathf.MoveTowards(dotOpenness, IsAimingAtInteractable ? 1f : 0f, DotResponse * 0.5f * Time.unscaledDeltaTime);
        float eased = Mathf.SmoothStep(0f, 1f, dotOpenness);

        float size = Mathf.Lerp(DotIdleSize, DotActiveSize, eased) * Screen.height / ReferenceHeight;
        dot.rectTransform.sizeDelta = Vector2.one * size;
        dot.color = new Color(1f, 1f, 1f, Mathf.Lerp(DotIdleAlpha, DotActiveAlpha, eased));
    }

    string ResolvePrompt(bool aimed, RaycastHit hit)
    {
        string basic = ResolveBasic(aimed, hit);
        if (string.IsNullOrEmpty(wrench.Prompt))
            return basic;
        return string.IsNullOrEmpty(basic) ? wrench.Prompt : basic + "\n" + wrench.Prompt;
    }

    string ResolveBasic(bool aimed, RaycastHit hit)
    {
        if (seat.IsSeated)
        {
            string driving = PromptDriving + WiperNames[seat.Vehicle.WiperMode] + PromptHandbrake + (seat.Vehicle.HandbrakeOn ? "вкл" : "выкл");
            if (seat.Vehicle.FuelLiters <= 0f)
                driving = PromptNoFuel + driving;
            return seat.Vehicle.IsOverturned ? PromptOverturned + driving : driving;
        }

        if (StoryUI.Instance.NoteOpen)
            return PromptCloseNote;

        // Варианты ответа уже на экране, а ЛКМ просто заканчивает разговор.
        if (NpcTalk.Current != null)
            return "";

        if (hands.HeldSiphon != null && hands.HeldSiphon.IsPumping)
            return "";

        // Сзади машины можно и толкать, и заливать: с канистрой в руках важнее бак.
        if (aimed && hands.Held != null && hands.Held.TryGetComponent(out FuelCan heldCan) && heldCan.Liters > 0f
            && InteractionRay.IsFreeVehicle(hit, out VehicleController car) && car.FuelLiters < car.TankSize)
        {
            if (Vector3.Distance(transform.position, car.FillerPoint) > PlayerSeat.PourReach)
                return PromptFiller;
            return PromptPour + Mathf.RoundToInt(heldCan.Liters) + " л, в баке " + Mathf.RoundToInt(car.FuelLiters) + " л";
        }

        // Луч из зоны толкания не может попасть в её триггер: начало луча внутри него.
        // Поэтому подсказка опирается на сам факт нахождения в зоне, где F и работает.
        if (seat.PushZone != null)
            return PromptPush;

        if (aimed)
        {
            if (InteractionRay.IsFiller(hit, out _))
            {
                if (hands.HeldSiphon == null)
                    return PromptNeedHose;
                if (hands.HeldSiphon.Source == null)
                    return PromptInsert;
            }
            if (hands.HeldSiphon != null && hit.collider.CompareTag(InteractionRay.JerrycanTag) && hands.HeldSiphon.Source != null
                && hit.rigidbody.TryGetComponent(out FuelCan farCan) && !hands.HeldSiphon.CanReach(farCan))
                return PromptTooFar;
            if (hands.Held == null && InteractionRay.IsPickupable(hit))
            {
                if (hit.rigidbody.TryGetComponent(out NoteItem _))
                    return PromptRead;
                if (hit.rigidbody.TryGetComponent(out CellPack _))
                    return PromptCells;
                if (hit.rigidbody.TryGetComponent(out RoadBlock _))
                    return PromptDrag;
                if (hit.rigidbody.TryGetComponent(out CarPartItem aimedPart))
                    return PromptTake + ": " + aimedPart.Info.Name + " (" + Mathf.RoundToInt(hit.rigidbody.mass) + " кг)";
                return hit.rigidbody.TryGetComponent(out FuelCan aimedCan) ? PromptTake + CanLiters(aimedCan) : PromptTake;
            }
            if (hands.Held == null && InteractionRay.IsNpc(hit, out _))
                return PromptTalk;
            if (hands.Held == null && InteractionRay.IsDoor(hit, out HouseDoor door))
                return door.IsOpen ? PromptDoorClose : PromptDoorOpen;
            if (InteractionRay.IsFuelTransfer(hands.HeldSiphon, hit))
                return PromptTransfer;
            if (InteractionRay.IsFreeVehicle(hit, out _))
                return PromptEnter;
        }
        if (hands.Held == null)
            return "";
        return hands.Held.TryGetComponent(out FuelCan can) ? PromptHolding + CanLiters(can) : PromptHolding;
    }

    static string CanLiters(FuelCan can)
    {
        return " (канистра: " + Mathf.RoundToInt(can.Liters) + " л)";
    }

    void OnGUI()
    {
        if (Event.current.type != EventType.Repaint || string.IsNullOrEmpty(CurrentPrompt))
            return;

        float scale = Screen.height / (float)ReferenceHeight;
        Vector2 center = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);

        if (promptStyle == null)
            promptStyle = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold };

        // За рулём подсказка видна постоянно, поэтому она уходит в угол и не мешает смотреть на дорогу.
        Rect area;
        if (seat.IsSeated)
        {
            float margin = DrivingPromptMargin * scale;
            float height = DrivingPromptSize * 4f * scale;
            promptStyle.alignment = TextAnchor.LowerLeft;
            promptStyle.fontSize = Mathf.RoundToInt(DrivingPromptSize * scale);
            area = new Rect(margin, Screen.height - height - margin, Screen.width - margin * 2f, height);
        }
        else
        {
            promptStyle.alignment = TextAnchor.UpperCenter;
            promptStyle.fontSize = Mathf.RoundToInt(PromptSize * scale);
            // Записка закрывает центр экрана, поэтому подсказка на это время уходит под неё.
            float top = StoryUI.Instance.NoteOpen ? Screen.height * 0.91f : center.y + PromptOffset * scale;
            area = new Rect(0f, top, Screen.width, PromptSize * 2.5f * scale);
        }

        Rect shadow = area;
        shadow.position += Vector2.one * Mathf.Max(1f, 2f * scale);

        GUI.color = Color.black;
        GUI.Label(shadow, CurrentPrompt, promptStyle);
        GUI.color = Color.white;
        GUI.Label(area, CurrentPrompt, promptStyle);
    }
}
