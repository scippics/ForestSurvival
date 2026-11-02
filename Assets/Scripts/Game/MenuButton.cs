using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Живая реакция кнопки: при наведении текст отъезжает вправо, теплеет, а снизу растёт линия.
// Время без масштаба: кнопки должны работать и на паузе.
public class MenuButton : MonoBehaviour, IPointerEnterHandler, ISelectHandler, IDeselectHandler
{
    const float Response = 11f;
    const float HoverShift = 26f;
    const float LineWidth = 460f;

    public Button Button { get; private set; }
    public bool IsInteractable => Button.interactable;
    public bool IsActive => IsInteractable && selected;

    Text label;
    Text hint;
    RectTransform line;
    Color normalColor, hoverColor, disabledColor;
    bool selected;
    float amount;

    public void Setup(Button button, Text labelText, Text hintText, RectTransform lineRect, Color normal, Color hover, Color disabled)
    {
        Button = button;
        label = labelText;
        hint = hintText;
        line = lineRect;
        normalColor = normal;
        hoverColor = hover;
        disabledColor = disabled;
    }

    public void SetInteractable(bool interactable)
    {
        Button.interactable = interactable;
        label.color = interactable ? normalColor : disabledColor;
    }

    public void SetHint(string text)
    {
        hint.text = text;
    }

    // Наведение выбирает кнопку: и мышь, и клавиатура двигают одну подсветку, поэтому она всегда одна.
    public void OnPointerEnter(PointerEventData eventData)
    {
        if (IsInteractable)
            EventSystem.current.SetSelectedGameObject(gameObject);
    }

    public void OnSelect(BaseEventData eventData) => selected = true;
    public void OnDeselect(BaseEventData eventData) => selected = false;

    void OnDisable()
    {
        selected = false;
        amount = 0f;
    }

    void Update()
    {
        bool active = IsActive;
        amount = Mathf.MoveTowards(amount, active ? 1f : 0f, Response * Time.unscaledDeltaTime);
        float eased = Mathf.SmoothStep(0f, 1f, amount);

        label.rectTransform.anchoredPosition = new Vector2(HoverShift * eased, 0f);
        line.sizeDelta = new Vector2(LineWidth * eased, line.sizeDelta.y);
        if (IsInteractable)
            label.color = Color.Lerp(normalColor, hoverColor, eased);
    }
}
