using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

// Главное меню и пауза. Всё создаётся кодом, а фон меню — настоящий лес: камера медленно
// летит над просекой, поэтому туман, ветер и время суток видны уже на старте.
public class MainMenu : MonoBehaviour
{
    const float ReferenceWidth = 1920f;
    const float ReferenceHeight = 1080f;
    const float FadeSeconds = 0.7f;
    const float MenuFlySpeed = 1.5f;
    const float MenuFlyHeight = 2.3f;
    const float MenuFlyStartZ = 60f;
    const float MenuLookAhead = 14f;
    const int DustCount = 46;
    const int LeftGradientWidth = 256;
    const float SavedMessageSeconds = 2.5f;

    static readonly Color TitleColor = new Color(0.86f, 0.88f, 0.84f);
    static readonly Color SubtitleColor = new Color(0.86f, 0.62f, 0.32f);
    static readonly Color ButtonColor = new Color(0.82f, 0.84f, 0.8f);
    static readonly Color ButtonHoverColor = new Color(1f, 0.74f, 0.38f);
    static readonly Color ButtonDisabledColor = new Color(0.42f, 0.44f, 0.43f);
    static readonly Color HintColor = new Color(0.6f, 0.62f, 0.6f);

    class Dust
    {
        public RectTransform Rect;
        public Image Image;
        public Vector2 Velocity;
        public float Phase;
        public float Speed;
        public float Brightness;
    }

    readonly List<Dust> dust = new List<Dust>();

    Font font;
    Sprite softCircle;
    Canvas canvas;
    GameObject menuPanel;
    GameObject pausePanel;
    GameObject settingsPanel;
    GameObject friendsPanel;
    Text friendsError;
    InputField friendAddress;
    Image fade;
    Text title;
    Text savedMessage;
    float fadeTarget;
    float savedMessageUntil;
    float flyZ = MenuFlyStartZ;
    System.Action afterFade;

    PlayerCamera view;
    Rigidbody playerBody;
    OptimizedForestGenerator forest;
    StoryDirector story;
    PlayerSeat seat;

    public MenuButton StartButton { get; private set; }
    public MenuButton LoadButton { get; private set; }
    public MenuButton HostButton { get; private set; }
    public MenuButton ExitButton { get; private set; }
    public MenuButton ResumeButton { get; private set; }
    public MenuButton SaveButton { get; private set; }

    void Awake()
    {
        seat = FindAnyObjectByType<PlayerSeat>();
        view = seat.GetComponentInChildren<PlayerCamera>();
        playerBody = seat.GetComponent<Rigidbody>();
        forest = FindAnyObjectByType<OptimizedForestGenerator>();
        story = gameObject.AddComponent<StoryDirector>();

        font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        softCircle = CreateSoftCircle();

        CreateCanvas();
        CreateEventSystem();
        CreateMenuPanel();
        CreatePausePanel();
        CreateSettingsPanel();
        CreateFriendsPanel();

        fade = CreateImage(canvas.transform, "Fade", null, Color.black);
        Stretch(fade.rectTransform);
        fade.raycastTarget = false;

        // Пока идёт меню, тело не должно падать: камера улетает, и земли под ним не будет.
        playerBody.isKinematic = true;
        view.enabled = false;

        GameFlow.StateChanged += OnStateChanged;
        GameFlow.SetState(GameState.Menu);
    }

    void OnDestroy()
    {
        GameFlow.StateChanged -= OnStateChanged;
    }

    void CreateCanvas()
    {
        var canvasObject = new GameObject("MenuCanvas");
        canvasObject.transform.SetParent(transform, false);
        canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 10;

        var scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(ReferenceWidth, ReferenceHeight);
        scaler.matchWidthOrHeight = 1f;

        canvasObject.AddComponent<GraphicRaycaster>();
    }

    void CreateEventSystem()
    {
        var system = new GameObject("EventSystem");
        system.transform.SetParent(transform, false);
        system.AddComponent<EventSystem>();
        system.AddComponent<InputSystemUIInputModule>().AssignDefaultActions();
    }

    void CreateMenuPanel()
    {
        menuPanel = CreatePanel("MenuPanel");

        Image shade = CreateImage(menuPanel.transform, "LeftShade", CreateLeftGradient(), Color.white);
        Stretch(shade.rectTransform);
        shade.raycastTarget = false;

        for (int i = 0; i < DustCount; i++)
            dust.Add(CreateDust(menuPanel.transform));

        title = CreateText(menuPanel.transform, "Title", "FOREST", 118, TitleColor, FontStyle.Bold);
        Place(title.rectTransform, 140f, 780f, 900f, 150f);
        Text subtitle = CreateText(menuPanel.transform, "Subtitle", "S U R V I V A L", 40, SubtitleColor, FontStyle.Normal);
        Place(subtitle.rectTransform, 146f, 716f, 900f, 60f);

        StartButton = CreateButton(menuPanel.transform, "НОВАЯ ИГРА", 560f, BeginNewGame);

        bool hasSave = SaveSystem.HasSave;
        LoadButton = CreateButton(menuPanel.transform, "ЗАГРУЗИТЬ", 490f, BeginLoadedGame);
        LoadButton.SetInteractable(hasSave);
        LoadButton.SetHint(hasSave ? SaveSystem.SaveDescription : "нет сохранений");

        HostButton = CreateButton(menuPanel.transform, "ИГРА С ДРУЗЬЯМИ", 420f, () => ShowSubPanel(friendsPanel));
        MenuButton settings = CreateButton(menuPanel.transform, "НАСТРОЙКИ", 350f, () => ShowSubPanel(settingsPanel));
        ExitButton = CreateButton(menuPanel.transform, "ВЫХОД", 280f, Application.Quit);

        Text version = CreateText(menuPanel.transform, "Version", "Прототип, версия " + Application.version, 20, HintColor, FontStyle.Normal);
        Place(version.rectTransform, 146f, 70f, 600f, 34f);

        Navigate(StartButton, LoadButton, HostButton, settings, ExitButton);

        // У клиента мир уже идёт на чужом компьютере: начинать и загружать тут нечего.
        if (!Unity.Netcode.NetworkManager.Singleton.IsServer)
        {
            StartButton.GetComponentInChildren<Text>().text = "В ИГРУ";
            LoadButton.SetInteractable(false);
            LoadButton.SetHint("игру ведёт хост");
        }
    }

    void CreatePausePanel()
    {
        pausePanel = CreatePanel("PausePanel");

        Image dim = CreateImage(pausePanel.transform, "Dim", null, new Color(0.02f, 0.03f, 0.04f, 0.72f));
        Stretch(dim.rectTransform);

        Text pauseTitle = CreateText(pausePanel.transform, "PauseTitle", "ПАУЗА", 72, TitleColor, FontStyle.Bold);
        Place(pauseTitle.rectTransform, 140f, 700f, 800f, 100f);

        ResumeButton = CreateButton(pausePanel.transform, "ПРОДОЛЖИТЬ", 560f, () => GameFlow.SetState(GameState.Playing));
        SaveButton = CreateButton(pausePanel.transform, "СОХРАНИТЬ", 490f, SaveGame);
        MenuButton pauseSettings = CreateButton(pausePanel.transform, "НАСТРОЙКИ", 420f, () => ShowSubPanel(settingsPanel));
        MenuButton quit = CreateButton(pausePanel.transform, "ВЫЙТИ ИЗ ИГРЫ", 350f, Application.Quit);

        savedMessage = CreateText(pausePanel.transform, "SavedMessage", "", 24, SubtitleColor, FontStyle.Normal);
        Place(savedMessage.rectTransform, 146f, 280f, 800f, 40f);

        Text controls = CreateText(pausePanel.transform, "Controls",
            "WASD - идти или ехать     Пробел - прыжок, ручник     ЛКМ / ПКМ - взять, бросить     E - машина, ключ зажигания, залить бензин     F - толкать\nG - ключ     T - фонарик     L - фары     V - дворники     R - поставить на колёса     Tab - блокнот",
            22, HintColor, FontStyle.Normal);
        Place(controls.rectTransform, 146f, 70f, 1500f, 80f);

        Navigate(ResumeButton, SaveButton, pauseSettings, quit);
        if (!Unity.Netcode.NetworkManager.Singleton.IsServer)
        {
            SaveButton.SetInteractable(false);
            SaveButton.SetHint("сохраняет хост");
        }
        pausePanel.SetActive(false);
    }

    void CreateSettingsPanel()
    {
        settingsPanel = CreatePanel("SettingsPanel");
        Image dim = CreateImage(settingsPanel.transform, "Dim", null, new Color(0.02f, 0.03f, 0.04f, 0.85f));
        Stretch(dim.rectTransform);

        Text header = CreateText(settingsPanel.transform, "SettingsTitle", "НАСТРОЙКИ", 72, TitleColor, FontStyle.Bold);
        Place(header.rectTransform, 140f, 760f, 900f, 100f);

        CreateSlider(settingsPanel.transform, "Чувствительность мыши", 620f, GameSettings.MinSensitivity, GameSettings.MaxSensitivity, GameSettings.Sensitivity, value => GameSettings.Sensitivity = value);
        CreateSlider(settingsPanel.transform, "Громкость", 550f, 0f, 1f, GameSettings.Volume, value => GameSettings.Volume = value);

        MenuButton quality = null;
        quality = CreateButton(settingsPanel.transform, "", 470f, () =>
        {
            GameSettings.Quality = (GameSettings.Quality + 1) % GameSettings.QualityNames.Length;
            SetQualityLabel(quality);
        });
        SetQualityLabel(quality);

        MenuButton screen = null;
        screen = CreateButton(settingsPanel.transform, "", 400f, () =>
        {
            Screen.fullScreen = !Screen.fullScreen;
            SetScreenLabel(screen, !Screen.fullScreen);
        });
        SetScreenLabel(screen, Screen.fullScreen);

        MenuButton limit = null;
        limit = CreateButton(settingsPanel.transform, "", 330f, () =>
        {
            GameSettings.FrameLimit = (GameSettings.FrameLimit + 1) % GameSettings.FrameLimitNames.Length;
            SetLimitLabel(limit);
        });
        SetLimitLabel(limit);

        MenuButton stream = null;
        stream = CreateButton(settingsPanel.transform, "", 260f, () =>
        {
            GameSettings.AllowExternalAudioStreaming = !GameSettings.AllowExternalAudioStreaming;
            SetStreamLabel(stream);
        });
        SetStreamLabel(stream);
        stream.SetInteractable(RadioCapture.IsSupported);
        if (!RadioCapture.IsSupported)
            stream.SetHint("только на Windows");

        MenuButton back = CreateButton(settingsPanel.transform, "НАЗАД", 160f, CloseSubPanel);
        Navigate(quality, screen, limit, stream, back);
        settingsPanel.SetActive(false);
    }

    static void SetQualityLabel(MenuButton button)
    {
        button.GetComponentInChildren<Text>().text = "КАЧЕСТВО: " + GameSettings.QualityNames[GameSettings.Quality];
    }

    static void SetStreamLabel(MenuButton button)
    {
        button.GetComponentInChildren<Text>().text = "ЗВУК ПК В МАГНИТОЛУ: " + (GameSettings.AllowExternalAudioStreaming ? "РАЗРЕШЁН" : "ЗАПРЕЩЁН");
    }

    static void SetLimitLabel(MenuButton button)
    {
        button.GetComponentInChildren<Text>().text = "ПРЕДЕЛ FPS: " + GameSettings.FrameLimitNames[GameSettings.FrameLimit];
    }

    static void SetScreenLabel(MenuButton button, bool fullScreen)
    {
        button.GetComponentInChildren<Text>().text = fullScreen ? "ЭКРАН: ВО ВЕСЬ" : "ЭКРАН: В ОКНЕ";
    }

    void CreateFriendsPanel()
    {
        friendsPanel = CreatePanel("FriendsPanel");
        Image dim = CreateImage(friendsPanel.transform, "Dim", null, new Color(0.02f, 0.03f, 0.04f, 0.88f));
        Stretch(dim.rectTransform);

        Text header = CreateText(friendsPanel.transform, "FriendsTitle", "ИГРА С ДРУЗЬЯМИ", 72, TitleColor, FontStyle.Bold);
        Place(header.rectTransform, 140f, 800f, 1200f, 100f);

        string addresses = string.Join(", ", NetworkBootstrap.LocalAddresses());
        Text host = CreateText(friendsPanel.transform, "HostInfo",
            "Твой компьютер уже сервер. Адрес в домашней сети: " + addresses + ", порт " + NetworkBootstrap.HostPort + ".\n" +
            "Друг вводит этот адрес у себя. Windows может спросить разрешение для игры в брандмауэре: нужно разрешить.\n" +
            "Через интернет: проброс UDP-порта " + NetworkBootstrap.HostPort + " на роутере или общая сеть вроде Radmin VPN.",
            26, ButtonColor, FontStyle.Normal);
        Place(host.rectTransform, 146f, 640f, 1640f, 130f);

        Text label = CreateText(friendsPanel.transform, "JoinLabel", "Подключиться к другу:", 30, SubtitleColor, FontStyle.Normal);
        Place(label.rectTransform, 146f, 540f, 600f, 50f);
        friendAddress = CreateInput(friendsPanel.transform, 146f, 475f);

        MenuButton connect = CreateButton(friendsPanel.transform, "ПОДКЛЮЧИТЬСЯ", 390f, ConnectToFriend);
        friendsError = CreateText(friendsPanel.transform, "JoinError", "", 24, SubtitleColor, FontStyle.Normal);
        Place(friendsError.rectTransform, 146f, 345f, 1200f, 40f);

        MenuButton back = CreateButton(friendsPanel.transform, "НАЗАД", 270f, CloseSubPanel);

        Text limit = CreateText(friendsPanel.transform, "NetLimit",
            "По сети общие игроки, машина, предметы в руках, время суток, дождь и задача. Толчки предметов и дверь пока у каждого свои.",
            22, HintColor, FontStyle.Normal);
        Place(limit.rectTransform, 146f, 70f, 1500f, 40f);

        Navigate(connect, back);
        friendsPanel.SetActive(false);
    }

    void ConnectToFriend()
    {
        if (!NetworkBootstrap.IsValidAddress(friendAddress.text))
        {
            friendsError.text = "Введи адрес, например 192.168.1.20";
            return;
        }
        friendsError.text = "Перезапуск игры...";
        NetworkBootstrap.RelaunchAsClient(friendAddress.text);
    }

    void ShowSubPanel(GameObject panel)
    {
        menuPanel.SetActive(false);
        pausePanel.SetActive(false);
        panel.SetActive(true);
        EventSystem.current.SetSelectedGameObject(null);
    }

    void CloseSubPanel()
    {
        settingsPanel.SetActive(false);
        friendsPanel.SetActive(false);
        bool paused = GameFlow.State == GameState.Paused;
        pausePanel.SetActive(paused);
        menuPanel.SetActive(!paused);
        EventSystem.current.SetSelectedGameObject(paused ? ResumeButton.gameObject : StartButton.gameObject);
    }

    void CreateSlider(Transform parent, string label, float y, float min, float max, float value, UnityEngine.Events.UnityAction<float> changed)
    {
        Text caption = CreateText(parent, label, label, 30, ButtonColor, FontStyle.Normal);
        Place(caption.rectTransform, 146f, y, 560f, 50f);

        var root = new GameObject("Slider", typeof(RectTransform));
        root.transform.SetParent(parent, false);
        Place(root.GetComponent<RectTransform>(), 720f, y + 14f, 520f, 22f);

        Image track = CreateImage(root.transform, "Track", null, new Color(0.2f, 0.21f, 0.2f));
        Stretch(track.rectTransform);

        Image fill = CreateImage(root.transform, "Fill", null, ButtonHoverColor);
        fill.raycastTarget = false;
        fill.rectTransform.sizeDelta = Vector2.zero;

        var handleArea = new GameObject("HandleArea", typeof(RectTransform));
        handleArea.transform.SetParent(root.transform, false);
        Stretch(handleArea.GetComponent<RectTransform>());
        Image handle = CreateImage(handleArea.transform, "Handle", null, TitleColor);
        handle.rectTransform.sizeDelta = new Vector2(16f, 16f);

        var slider = root.AddComponent<Slider>();
        slider.fillRect = fill.rectTransform;
        slider.handleRect = handle.rectTransform;
        slider.targetGraphic = handle;
        slider.direction = Slider.Direction.LeftToRight;
        slider.minValue = min;
        slider.maxValue = max;
        slider.value = value;
        slider.onValueChanged.AddListener(changed);
    }

    InputField CreateInput(Transform parent, float x, float y)
    {
        var root = new GameObject("AddressField", typeof(RectTransform));
        root.transform.SetParent(parent, false);
        Place(root.GetComponent<RectTransform>(), x, y, 560f, 56f);
        Image background = root.AddComponent<Image>();
        background.color = new Color(0.12f, 0.13f, 0.13f);

        Text placeholder = CreateText(root.transform, "Placeholder", "192.168.1.20", 30, HintColor, FontStyle.Normal);
        Place(placeholder.rectTransform, 14f, 0f, 530f, 56f);
        Text typed = CreateText(root.transform, "Typed", "", 30, TitleColor, FontStyle.Normal);
        Place(typed.rectTransform, 14f, 0f, 530f, 56f);
        typed.supportRichText = false;
        typed.horizontalOverflow = HorizontalWrapMode.Wrap;

        var field = root.AddComponent<InputField>();
        field.textComponent = typed;
        field.placeholder = placeholder;
        field.characterLimit = 64;
        return field;
    }

    static void Navigate(params MenuButton[] buttons)
    {
        for (int i = 0; i < buttons.Length; i++)
        {
            var navigation = new Navigation
            {
                mode = Navigation.Mode.Explicit,
                selectOnUp = buttons[(i + buttons.Length - 1) % buttons.Length].Button,
                selectOnDown = buttons[(i + 1) % buttons.Length].Button
            };
            buttons[i].Button.navigation = navigation;
        }
    }

    public void BeginNewGame()
    {
        FadeThen(() => EnterGame(false));
    }

    public void BeginLoadedGame()
    {
        FadeThen(() => EnterGame(true));
    }

    void FadeThen(System.Action action)
    {
        afterFade = action;
        fadeTarget = 1f;
        menuPanel.GetComponent<CanvasGroup>().interactable = false;
    }

    // Из меню в гараж, а если сессия уже идёт, то сразу к остальным в тайгу.
    void EnterGame(bool load)
    {
        menuPanel.SetActive(false);
        view.ResetToBody();

        if (WorldSync.Instance != null && WorldSync.Instance.SessionStarted)
        {
            BeginTaiga(load);
            return;
        }

        GarageLobby.Instance.Enter(load);
        playerBody.isKinematic = false;
        view.enabled = true;
        GameFlow.SetState(GameState.Playing);
    }

    // Гараж позади: у хоста здесь начинается пролог или загрузка, клиент просто оказывается в идущем мире.
    public void BeginTaiga(bool load)
    {
        if (load)
            SaveSystem.Load();
        else
            story.Begin();

        // Сначала земля вокруг новой позиции, и только потом оживает физика.
        forest.RefreshNow();
        if (!seat.IsSeated)
            playerBody.isKinematic = false;

        // Пролог сам возвращает камеру и управление, когда закончится.
        if (story.InCutscene)
            return;

        view.enabled = true;
        GameFlow.SetState(GameState.Playing);
    }

    void SaveGame()
    {
        bool saved = SaveSystem.Save();
        savedMessage.text = saved ? "Игра сохранена" : "Не удалось сохранить";
        savedMessageUntil = Time.unscaledTime + SavedMessageSeconds;
    }

    void OnStateChanged(GameState state)
    {
        pausePanel.SetActive(state == GameState.Paused);
        if (settingsPanel != null)
            settingsPanel.SetActive(false);

        if (state == GameState.Paused)
        {
            savedMessage.text = "";
            SaveButton.SetInteractable(Unity.Netcode.NetworkManager.Singleton.IsServer && !GarageLobby.InLobby);
            EventSystem.current.SetSelectedGameObject(ResumeButton.gameObject);
        }
        else if (state == GameState.Menu)
        {
            EventSystem.current.SetSelectedGameObject(StartButton.gameObject);
        }
        else
        {
            EventSystem.current.SetSelectedGameObject(null);
        }
    }

    void Update()
    {
        float dt = Time.unscaledDeltaTime;

        // Экран открывается из черноты и уходит в неё при старте игры.
        float alpha = Mathf.MoveTowards(fade.color.a, fadeTarget, dt / FadeSeconds);
        fade.color = new Color(0f, 0f, 0f, alpha);
        if (afterFade != null && alpha >= 1f)
        {
            System.Action action = afterFade;
            afterFade = null;
            action();
            fadeTarget = 0f;
        }

        if (savedMessage.text.Length > 0 && Time.unscaledTime > savedMessageUntil)
            savedMessage.text = "";

        if (GameFlow.State != GameState.Menu)
            return;

        FlyCamera(dt);
        AnimateTitle();
        AnimateDust(dt);
    }

    void FlyCamera(float dt)
    {
        flyZ += MenuFlySpeed * dt;
        Vector3 position = RoadPoint(flyZ);
        Vector3 target = RoadPoint(flyZ + MenuLookAhead);
        target.y -= 0.8f;
        Transform camera = view.transform;
        camera.SetPositionAndRotation(position, Quaternion.LookRotation(target - position, Vector3.up));
    }

    static Vector3 RoadPoint(float z)
    {
        float x = OptimizedForestGenerator.RoadCenterX(0, z);
        return new Vector3(x, OptimizedForestGenerator.TerrainHeight(x, z) + MenuFlyHeight, z);
    }

    // Лёгкое мерцание, как у лампы на плохом контакте.
    void AnimateTitle()
    {
        float t = Time.unscaledTime;
        float flicker = Mathf.PerlinNoise(t * 3.1f, 7.3f);
        float alpha = flicker < 0.18f ? 0.72f : Mathf.Lerp(0.9f, 1f, flicker);
        title.color = new Color(TitleColor.r, TitleColor.g, TitleColor.b, alpha);
    }

    void AnimateDust(float dt)
    {
        float t = Time.unscaledTime;
        foreach (Dust mote in dust)
        {
            Vector2 position = mote.Rect.anchoredPosition + mote.Velocity * dt;
            if (position.x > ReferenceWidth) position.x -= ReferenceWidth;
            if (position.x < 0f) position.x += ReferenceWidth;
            if (position.y > ReferenceHeight) position.y -= ReferenceHeight;
            if (position.y < 0f) position.y += ReferenceHeight;
            mote.Rect.anchoredPosition = position;

            float pulse = 0.5f + 0.5f * Mathf.Sin(t * mote.Speed + mote.Phase);
            mote.Image.color = new Color(1f, 0.82f, 0.56f, mote.Brightness * pulse);
        }
    }

    Dust CreateDust(Transform parent)
    {
        Image image = CreateImage(parent, "Dust", softCircle, Color.clear);
        image.raycastTarget = false;
        float size = Random.Range(3f, 11f);
        RectTransform rect = image.rectTransform;
        rect.anchorMin = rect.anchorMax = Vector2.zero;
        rect.sizeDelta = Vector2.one * size;
        rect.anchoredPosition = new Vector2(Random.Range(0f, ReferenceWidth), Random.Range(0f, ReferenceHeight));
        return new Dust
        {
            Rect = rect,
            Image = image,
            Velocity = new Vector2(Random.Range(-14f, 14f), Random.Range(4f, 22f)),
            Phase = Random.Range(0f, Mathf.PI * 2f),
            Speed = Random.Range(0.4f, 1.6f),
            Brightness = Random.Range(0.12f, 0.5f)
        };
    }

    GameObject CreatePanel(string panelName)
    {
        var panel = new GameObject(panelName, typeof(RectTransform), typeof(CanvasGroup));
        panel.transform.SetParent(canvas.transform, false);
        Stretch(panel.GetComponent<RectTransform>());
        return panel;
    }

    MenuButton CreateButton(Transform parent, string label, float y, UnityEngine.Events.UnityAction action)
    {
        var root = new GameObject("Button_" + label, typeof(RectTransform));
        root.transform.SetParent(parent, false);
        var rect = root.GetComponent<RectTransform>();
        Place(rect, 140f, y, 720f, 62f);

        // Прозрачная подложка ловит наведение по всей строке, а не только по буквам.
        Image hitArea = root.AddComponent<Image>();
        hitArea.color = Color.clear;

        var button = root.AddComponent<Button>();
        button.transition = Selectable.Transition.None;
        if (action != null)
            button.onClick.AddListener(action);
        button.onClick.AddListener(() => ForestAudio.Play(ForestSound.Click, 0.6f));

        Text text = CreateText(root.transform, "Label", label, 38, ButtonColor, FontStyle.Bold);
        Place(text.rectTransform, 0f, 0f, 600f, 62f);
        text.raycastTarget = false;

        Text hint = CreateText(root.transform, "Hint", "", 22, HintColor, FontStyle.Normal);
        Place(hint.rectTransform, 0f, 0f, 700f, 62f);
        hint.alignment = TextAnchor.MiddleRight;
        hint.raycastTarget = false;

        Image line = CreateImage(root.transform, "Line", null, ButtonHoverColor);
        line.raycastTarget = false;
        RectTransform lineRect = line.rectTransform;
        lineRect.anchorMin = lineRect.anchorMax = lineRect.pivot = Vector2.zero;
        lineRect.anchoredPosition = new Vector2(0f, 2f);
        lineRect.sizeDelta = new Vector2(0f, 3f);

        var menuButton = root.AddComponent<MenuButton>();
        menuButton.Setup(button, text, hint, lineRect, ButtonColor, ButtonHoverColor, ButtonDisabledColor);
        return menuButton;
    }

    Text CreateText(Transform parent, string objectName, string content, int size, Color color, FontStyle style)
    {
        var textObject = new GameObject(objectName, typeof(RectTransform));
        textObject.transform.SetParent(parent, false);
        var text = textObject.AddComponent<Text>();
        text.font = font;
        text.text = content;
        text.fontSize = size;
        text.fontStyle = style;
        text.color = color;
        text.alignment = TextAnchor.MiddleLeft;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        var shadow = textObject.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.8f);
        shadow.effectDistance = new Vector2(2f, -2f);
        return text;
    }

    static Image CreateImage(Transform parent, string objectName, Sprite sprite, Color color)
    {
        var imageObject = new GameObject(objectName, typeof(RectTransform));
        imageObject.transform.SetParent(parent, false);
        var image = imageObject.AddComponent<Image>();
        image.sprite = sprite;
        image.color = color;
        return image;
    }

    static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    static void Place(RectTransform rect, float x, float y, float width, float height)
    {
        rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.zero;
        rect.anchoredPosition = new Vector2(x, y);
        rect.sizeDelta = new Vector2(width, height);
    }

    // Затемнение слева, где стоит текст: справа лес остаётся открытым.
    static Sprite CreateLeftGradient()
    {
        var texture = new Texture2D(LeftGradientWidth, 1, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        for (int x = 0; x < LeftGradientWidth; x++)
        {
            float t = x / (float)(LeftGradientWidth - 1);
            texture.SetPixel(x, 0, new Color(0.01f, 0.015f, 0.02f, Mathf.Lerp(0.88f, 0f, Mathf.SmoothStep(0f, 1f, t * 1.4f))));
        }
        texture.Apply();
        return Sprite.Create(texture, new Rect(0f, 0f, LeftGradientWidth, 1f), Vector2.one * 0.5f);
    }

    static Sprite CreateSoftCircle()
    {
        const int size = 32;
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        float center = (size - 1) * 0.5f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float distance = Mathf.Sqrt((x - center) * (x - center) + (y - center) * (y - center)) / center;
                float alpha = Mathf.Clamp01(1f - distance);
                texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha * alpha));
            }
        }
        texture.Apply();
        return Sprite.Create(texture, new Rect(0f, 0f, size, size), Vector2.one * 0.5f);
    }
}
