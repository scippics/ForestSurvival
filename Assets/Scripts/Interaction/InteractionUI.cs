using UnityEngine;

// Вешается на камеру: прицел и контекстная подсказка под ним.
public class InteractionUI : MonoBehaviour
{
    const string PromptTake = "ЛКМ - Взять";
    const string PromptPush = "F - Толкать машину";
    const string PromptTransfer = "Зажать ЛКМ - Перелить топливо";
    const string PromptHolding = "ЛКМ - Отпустить, ПКМ - Бросить";
    const string PromptPressure = "Отпустить на 75-95. Давление: ";
    const string PromptEnter = "E - Сесть за руль";
    const string PromptDriving = "E - Выйти, G - Пониженная: ";
    const string PromptHeadlights = ", L - Фары";

    const int ReferenceHeight = 1080;
    const float CrosshairLength = 8f;
    const float CrosshairThickness = 2f;
    const float PromptOffset = 24f;
    const float PromptSize = 24f;
    const float DrivingPromptSize = 16f;
    const float DrivingPromptMargin = 20f;

    public string CurrentPrompt { get; private set; } = "";

    PlayerHands hands;
    PlayerSeat seat;
    GUIStyle promptStyle;

    void Awake()
    {
        hands = GetComponentInParent<PlayerHands>();
        seat = GetComponentInParent<PlayerSeat>();
    }

    void Update()
    {
        CurrentPrompt = ResolvePrompt();
    }

    string ResolvePrompt()
    {
        if (seat.IsSeated)
            return PromptDriving + (seat.Vehicle.isLowGear ? "вкл" : "выкл") + PromptHeadlights;

        if (hands.HeldSiphon != null && hands.HeldSiphon.IsPumping)
            return PromptPressure + Mathf.FloorToInt(hands.HeldSiphon.SiphonPressure);

        // Луч из зоны толкания не может попасть в её триггер: начало луча внутри него.
        // Поэтому подсказка опирается на сам факт нахождения в зоне, где F и работает.
        if (seat.PushZone != null)
            return PromptPush;

        if (InteractionRay.Cast(transform, out RaycastHit hit))
        {
            if (hands.Held == null && InteractionRay.IsPickupable(hit))
                return PromptTake;
            if (InteractionRay.IsFuelTransfer(hands.HeldSiphon, hit))
                return PromptTransfer;
            if (InteractionRay.IsFreeVehicle(hit, out _))
                return PromptEnter;
        }
        return hands.Held != null ? PromptHolding : "";
    }

    void OnGUI()
    {
        if (Event.current.type != EventType.Repaint)
            return;

        float scale = Screen.height / (float)ReferenceHeight;
        Vector2 center = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);

        DrawCrosshair(center, scale, 1f, Color.black);
        DrawCrosshair(center, scale, 0f, Color.white);

        if (string.IsNullOrEmpty(CurrentPrompt))
            return;

        if (promptStyle == null)
            promptStyle = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold };

        // За рулём подсказка видна постоянно, поэтому она уходит в угол и не мешает смотреть на дорогу.
        Rect area;
        if (seat.IsSeated)
        {
            float margin = DrivingPromptMargin * scale;
            float height = DrivingPromptSize * 2f * scale;
            promptStyle.alignment = TextAnchor.LowerLeft;
            promptStyle.fontSize = Mathf.RoundToInt(DrivingPromptSize * scale);
            area = new Rect(margin, Screen.height - height - margin, Screen.width - margin * 2f, height);
        }
        else
        {
            promptStyle.alignment = TextAnchor.UpperCenter;
            promptStyle.fontSize = Mathf.RoundToInt(PromptSize * scale);
            area = new Rect(0f, center.y + PromptOffset * scale, Screen.width, PromptSize * 2.5f * scale);
        }

        Rect shadow = area;
        shadow.position += Vector2.one * Mathf.Max(1f, 2f * scale);

        GUI.color = Color.black;
        GUI.Label(shadow, CurrentPrompt, promptStyle);
        GUI.color = Color.white;
        GUI.Label(area, CurrentPrompt, promptStyle);
    }

    static void DrawCrosshair(Vector2 center, float scale, float outline, Color color)
    {
        float length = (CrosshairLength * scale + outline) * 2f;
        float thickness = Mathf.Max(1f, CrosshairThickness * scale) + outline * 2f;

        GUI.color = color;
        GUI.DrawTexture(new Rect(center.x - length * 0.5f, center.y - thickness * 0.5f, length, thickness), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(center.x - thickness * 0.5f, center.y - length * 0.5f, thickness, length), Texture2D.whiteTexture);
    }
}
