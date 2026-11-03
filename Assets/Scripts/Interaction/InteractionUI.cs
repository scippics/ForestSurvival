using UnityEngine;
using UnityEngine.UI;

// Вешается на камеру: точка прицела, а под взглядом на предмет вместо неё значок клавиши и кайма на предмете.
// Никаких текстов: что с предметом можно сделать, игрок узнаёт из самой клавиши.
public class InteractionUI : MonoBehaviour
{
    public const string KeyGrab = "ЛКМ";
    public const string KeyInteract = "E";
    public const string KeyPush = "F";
    public const string KeyWrench = "G";
    public const string KeyRecover = "R";
    const string DoorPartName = "Door";
    const char KeySeparator = ' ';

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
    static readonly Color CapColor = new Color(0.04f, 0.04f, 0.04f, 0.62f);
    static readonly Color CapTextColor = new Color(0.94f, 0.92f, 0.84f);

    // Один значок или два через пробел: "E" или "E G".
    public string CurrentKey { get; private set; } = "";
    public bool DotVisible => dot.enabled;

    PlayerHands hands;
    PlayerSeat seat;
    PlayerWrench wrench;
    ObjectOutline outline;
    Image dot;
    readonly Image[] caps = new Image[2];
    readonly Text[] capTexts = new Text[2];
    string shownKey = "";

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
            outline.Show(null);
            dot.enabled = false;
            DrawKeys();
            return;
        }

        outlined = null;
        outlinedPart = null;
        bool aimed = InteractionRay.Cast(transform, out RaycastHit hit);
        CurrentKey = ResolveKey(aimed, hit);
        outline.Show(outlined, outlinedPart);

        // За рулём точка не нужна: центр экрана остаётся свободным для дороги.
        dot.enabled = !seat.IsSeated && CurrentKey.Length == 0;
        dot.rectTransform.sizeDelta = Vector2.one * (DotSize * Screen.height / ReferenceHeight);
        DrawKeys();
    }

    string ResolveKey(bool aimed, RaycastHit hit)
    {
        string basic = ResolveBasic(aimed, hit);
        if (!wrench.HasJob)
            return basic;

        if (wrench.JobTarget != null)
            outlined = wrench.JobTarget;

        // С деталью в руках дело одно: ключ. С пустыми руками у машины можно и сесть, и снять деталь.
        return hands.Held != null || basic.Length == 0 ? KeyWrench : basic + KeySeparator + KeyWrench;
    }

    string ResolveBasic(bool aimed, RaycastHit hit)
    {
        if (seat.IsSeated)
        {
            if (seat.AimingAtKey)
            {
                outlined = seat.Vehicle.IgnitionKey;
                return KeyInteract;
            }
            return seat.Vehicle.IsOverturned ? KeyRecover : "";
        }

        // Записка закрывается тем же кликом, которым открылась, а варианты ответа уже на экране.
        if (StoryUI.Instance.NoteOpen || NpcTalk.Current != null)
            return "";

        if (hands.HeldSiphon != null && hands.HeldSiphon.IsPumping)
            return "";

        // Сзади машины можно и толкать, и заливать: с канистрой в руках важнее бак.
        if (aimed && hands.Held != null && hands.Held.TryGetComponent(out FuelCan heldCan) && heldCan.Liters > 0f
            && InteractionRay.IsFreeVehicle(hit, out VehicleController car) && car.FuelLiters < car.TankSize)
        {
            outlined = car.FillerCap;
            return Vector3.Distance(transform.position, car.FillerPoint) > PlayerSeat.PourReach ? "" : KeyInteract;
        }

        // Луч из зоны толкания не может попасть в её триггер: начало луча внутри него.
        // Поэтому значок опирается на сам факт нахождения в зоне, где F и работает.
        if (seat.PushZone != null)
            return KeyPush;

        if (!aimed)
            return "";

        if (InteractionRay.IsFiller(hit, out _) && hands.HeldSiphon != null && hands.HeldSiphon.Source == null)
        {
            outlined = hit.collider.transform;
            return KeyGrab;
        }
        if (hands.Held == null && (InteractionRay.IsPickupable(hit) || InteractionRay.IsNpc(hit, out _) || InteractionRay.IsDoor(hit, out _)))
        {
            outlined = hit.rigidbody.transform;
            return KeyGrab;
        }
        if (InteractionRay.IsFuelTransfer(hands.HeldSiphon, hit))
        {
            outlined = hit.rigidbody.transform;
            return KeyGrab;
        }
        if (InteractionRay.IsCar(hit, out VehicleController boarding) && seat.ChooseSeat(boarding) != PlayerSeat.NoSeat)
        {
            outlined = boarding.transform;
            outlinedPart = DoorPartName;
            return KeyInteract;
        }
        return "";
    }

    void DrawKeys()
    {
        if (CurrentKey == shownKey && Time.frameCount % 30 != 0)
            return;
        shownKey = CurrentKey;

        string[] keys = CurrentKey.Length == 0 ? System.Array.Empty<string>() : CurrentKey.Split(KeySeparator);
        float scale = Screen.height / (float)ReferenceHeight;

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
