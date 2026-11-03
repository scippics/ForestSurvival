using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

// Всё, что пролог показывает поверх мира: чернота с титрами, субтитры, записка и блокнот.
public class StoryUI : MonoBehaviour
{
    const float ReferenceHeight = 1080f;
    const float SubtitleFade = 0.5f;
    const float SubtitleSecondsPerChar = 0.065f;
    const float SubtitleMinSeconds = 3f;

    static readonly Vector2 DialogueSize = new Vector2(1200f, 370f);

    static readonly Color CardColor = new Color(0.82f, 0.82f, 0.78f);
    static readonly Color TitleColor = new Color(0.9f, 0.88f, 0.82f);
    static readonly Color AccentColor = new Color(0.86f, 0.62f, 0.32f);
    static readonly Color PaperColor = new Color(0.78f, 0.74f, 0.62f);
    static readonly Color InkColor = new Color(0.1f, 0.09f, 0.14f);
    static readonly Color CoverColor = new Color(0.1f, 0.14f, 0.1f);

    public static StoryUI Instance { get; private set; }

    public bool NoteOpen => paper.activeSelf;
    public bool NotebookOpen => notebook.activeSelf;
    public bool DialogueOpen => dialogue.activeSelf;
    public string DialogueLine => dialogueText.text;
    public string Goal { get; private set; } = "";
    public string CurrentSubtitle => subtitle.text;

    readonly Queue<string> subtitles = new Queue<string>();

    Font font;
    Image black;
    Text card;
    Text title;
    Text titleLine;
    Text subtitle;
    Text skipHint;
    Text noteText;
    Text notebookText;
    Text dialogueText;
    GameObject paper;
    GameObject notebook;
    GameObject dialogue;
    float subtitleUntil;
    float subtitleStart;

    void Awake()
    {
        Instance = this;
        font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        var canvasObject = new GameObject("StoryCanvas");
        canvasObject.transform.SetParent(transform, false);
        var canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 5;
        var scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, ReferenceHeight);
        scaler.matchWidthOrHeight = 1f;

        Transform root = canvasObject.transform;

        subtitle = AddText(root, "Subtitle", 30, CardColor, FontStyle.Normal);
        Place(subtitle.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 110f), new Vector2(1300f, 120f));

        paper = AddPanel(root, "Paper", PaperColor, new Vector2(820f, 800f));
        noteText = AddText(paper.transform, "NoteText", 30, InkColor, FontStyle.Italic);
        noteText.alignment = TextAnchor.UpperLeft;
        noteText.GetComponent<Shadow>().enabled = false;
        Place(noteText.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(720f, 700f));
        paper.SetActive(false);

        notebook = AddPanel(root, "Notebook", CoverColor, new Vector2(860f, 640f));
        Image page = AddImage(notebook.transform, "Page", PaperColor);
        Place(page.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(14f, 0f), new Vector2(800f, 600f));
        notebookText = AddText(notebook.transform, "NotebookText", 28, InkColor, FontStyle.Normal);
        notebookText.alignment = TextAnchor.UpperLeft;
        notebookText.supportRichText = true;
        notebookText.GetComponent<Shadow>().enabled = false;
        Place(notebookText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(14f, 0f), new Vector2(720f, 540f));
        notebook.SetActive(false);

        dialogue = AddPanel(root, "Dialogue", new Color(0f, 0f, 0f, 0.72f), DialogueSize);
        Place(dialogue.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0f, DialogueSize.y * 0.5f + 40f), DialogueSize);
        dialogueText = AddText(dialogue.transform, "DialogueText", 27, CardColor, FontStyle.Normal);
        dialogueText.alignment = TextAnchor.UpperLeft;
        dialogueText.supportRichText = true;
        Place(dialogueText.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, DialogueSize - new Vector2(60f, 40f));
        dialogue.SetActive(false);

        black = AddImage(root, "Black", Color.black);
        Stretch(black.rectTransform);
        black.color = Color.clear;

        card = AddText(root, "Card", 38, CardColor, FontStyle.Normal);
        Place(card.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1300f, 300f));

        title = AddText(root, "Title", 96, TitleColor, FontStyle.Bold);
        Place(title.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 60f), new Vector2(1400f, 160f));

        titleLine = AddText(root, "TitleLine", 32, AccentColor, FontStyle.Normal);
        Place(titleLine.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, -30f), new Vector2(1400f, 60f));

        skipHint = AddText(root, "SkipHint", 20, new Color(0.55f, 0.55f, 0.52f), FontStyle.Normal);
        skipHint.text = "Пробел - пропустить";
        skipHint.alignment = TextAnchor.LowerRight;
        Place(skipHint.rectTransform, new Vector2(1f, 0f), new Vector2(-180f, 40f), new Vector2(320f, 40f));
        skipHint.enabled = false;

        SetCard("", 0f);
        SetTitle("", "", 0f);
        subtitle.text = "";
    }

    void Update()
    {
        float now = Time.unscaledTime;
        if (now >= subtitleUntil)
        {
            if (subtitles.Count > 0)
            {
                subtitle.text = subtitles.Dequeue();
                subtitleStart = now;
                subtitleUntil = now + Mathf.Max(SubtitleMinSeconds, subtitle.text.Length * SubtitleSecondsPerChar);
            }
            else
            {
                subtitle.text = "";
            }
        }

        float alpha = Mathf.Min(Mathf.Clamp01((now - subtitleStart) / SubtitleFade), Mathf.Clamp01((subtitleUntil - now) / SubtitleFade));
        Color color = subtitle.color;
        color.a = alpha;
        subtitle.color = color;
    }

    public void SetBlack(float alpha)
    {
        black.color = new Color(0f, 0f, 0f, alpha);
    }

    public void SetSkipHint(bool visible)
    {
        skipHint.enabled = visible;
    }

    public void SetCard(string text, float alpha)
    {
        card.text = text;
        card.color = new Color(CardColor.r, CardColor.g, CardColor.b, alpha);
    }

    public void SetTitle(string text, string line, float alpha)
    {
        title.text = text;
        titleLine.text = line;
        title.color = new Color(TitleColor.r, TitleColor.g, TitleColor.b, alpha);
        titleLine.color = new Color(AccentColor.r, AccentColor.g, AccentColor.b, alpha);
    }

    public void Say(string line)
    {
        subtitles.Enqueue(line);
    }

    public void ClearSubtitles()
    {
        subtitles.Clear();
        subtitleUntil = 0f;
    }

    public void SetGoal(string goal)
    {
        Goal = goal;
    }

    public void ShowNote(string text)
    {
        noteText.text = text;
        notebook.SetActive(false);
        paper.SetActive(true);
    }

    public void CloseNote()
    {
        paper.SetActive(false);
    }

    public void ShowDialogue(string speaker, string line, IReadOnlyList<string> options)
    {
        var text = new StringBuilder();
        text.Append("<color=#DB9E52><b>").Append(speaker).Append("</b></color>\n").Append(line).Append("\n\n");
        for (int i = 0; i < options.Count; i++)
            text.Append("<b>").Append(i + 1).Append("</b>  ").Append(options[i]).Append('\n');

        dialogueText.text = text.ToString();
        dialogue.SetActive(true);
    }

    public void HideDialogue()
    {
        dialogue.SetActive(false);
    }

    public void ToggleNotebook(IReadOnlyList<string> recipes)
    {
        if (notebook.activeSelf)
        {
            notebook.SetActive(false);
            return;
        }

        var text = new StringBuilder();
        text.Append("<b>Задача</b>\n");
        text.Append(string.IsNullOrEmpty(Goal) ? StoryText.NoGoal : Goal);
        text.Append("\n\n<b>").Append(StoryText.RecipesHeader).Append("</b>\n");
        if (recipes.Count == 0)
            text.Append(StoryText.RecipesEmpty);
        foreach (string recipe in recipes)
            text.Append("- ").Append(StoryText.RecipeName(recipe)).Append('\n');

        notebookText.text = text.ToString();
        paper.SetActive(false);
        notebook.SetActive(true);
    }

    GameObject AddPanel(Transform parent, string objectName, Color color, Vector2 size)
    {
        Image image = AddImage(parent, objectName, color);
        Place(image.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, size);
        return image.gameObject;
    }

    static Image AddImage(Transform parent, string objectName, Color color)
    {
        var imageObject = new GameObject(objectName, typeof(RectTransform));
        imageObject.transform.SetParent(parent, false);
        var image = imageObject.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    Text AddText(Transform parent, string objectName, int size, Color color, FontStyle style)
    {
        var textObject = new GameObject(objectName, typeof(RectTransform));
        textObject.transform.SetParent(parent, false);
        var text = textObject.AddComponent<Text>();
        text.font = font;
        text.fontSize = size;
        text.fontStyle = style;
        text.color = color;
        text.alignment = TextAnchor.MiddleCenter;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.raycastTarget = false;
        var shadow = textObject.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.85f);
        shadow.effectDistance = new Vector2(2f, -2f);
        return text;
    }

    static void Place(RectTransform rect, Vector2 anchor, Vector2 position, Vector2 size)
    {
        rect.anchorMin = rect.anchorMax = anchor;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }
}
