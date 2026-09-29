using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class BattleSceneSetup : MonoBehaviour
{
    private static bool started;

    [Header("Config")]
    [Tooltip("Загружается из Assets/Resources, если поле пустое.")]
    public string configResourceName = "BattleshipConfig";

    public BattleshipConfig config;

    [Header("Network Simulation")]
    [Min(0f)]
    public float latencyMs = 120f;

    [Range(0f, 1f)]
    public float packetLoss = 0f;

    [Header("Reliable Layer")]
    public bool verboseTransportLogging = false;
    public float retryTimeout = 0.5f;
    public int maxRetries = 10;

    private Canvas canvas;

    private GameObject canvasObject;
    private GameObject networkObject;
    private GameObject eventSystemObject;

    private bool restarting;

    private LocalTransport clientTransport1;
    private LocalTransport clientTransport2;
    private LocalTransport serverTransport1;
    private LocalTransport serverTransport2;

    private ReliableOrderedTransport clientLink1;
    private ReliableOrderedTransport clientLink2;
    private ReliableOrderedTransport serverLink1;
    private ReliableOrderedTransport serverLink2;

    private BattleClient client1;
    private BattleClient client2;
    private BattleServer server;
    private BattleGameUI gameUI;
    private Button restartButton;

    private void Awake()
    {
        if (started)
        {
            Destroy(gameObject);
            return;
        }

        started = true;

        DontDestroyOnLoad(gameObject);

        SetupScene();
    }

    private void Update()
    {
        server?.Tick(Time.deltaTime);

        client1?.Update(Time.deltaTime);
        client2?.Update(Time.deltaTime);
    }

    private void OnDestroy()
    {
        clientLink1?.Disconnect();
        clientLink2?.Disconnect();
        serverLink1?.Disconnect();
        serverLink2?.Disconnect();
    }

    private void SetupScene()
    {
        config = ResolveConfig();

        CreateCanvas();
        CreateNetwork();
        CreateServer();
        CreateUI();
    }

    private BattleshipConfig ResolveConfig()
    {
        if (config != null)
        {
            Debug.Log(
                $"[BattleSceneSetup] Using assigned config: " +
                $"{config.name}");

            return config.GetValidated();
        }

        if (!string.IsNullOrEmpty(configResourceName))
        {
            BattleshipConfig loaded =
                Resources.Load<BattleshipConfig>(
                    configResourceName);

            if (loaded != null)
            {
                config = loaded;

                Debug.Log(
                    $"[BattleSceneSetup] Loaded config from " +
                    $"Resources/{configResourceName}");

                return config.GetValidated();
            }

            Debug.LogWarning(
                $"[BattleSceneSetup] No config at " +
                $"Resources/{configResourceName}. " +
                $"Create it via Assets > Create > " +
                $"Battleship > Game Config and move it " +
                $"to Assets/Resources/.");
        }

        Debug.LogWarning(
            "[BattleSceneSetup] No config found, " +
            "using built-in defaults.");

        config =
            ScriptableObject.CreateInstance<BattleshipConfig>();

        return config.GetValidated();
    }

    private void CreateCanvas()
    {
        GameObject canvasObject =
            new GameObject("BattleCanvas");

        this.canvasObject = canvasObject;

        canvas =
            canvasObject.AddComponent<Canvas>();

        canvas.renderMode =
            RenderMode.ScreenSpaceOverlay;

        CanvasScaler scaler =
            canvasObject.AddComponent<CanvasScaler>();

        scaler.uiScaleMode =
            CanvasScaler.ScaleMode.ScaleWithScreenSize;

        scaler.referenceResolution =
            new Vector2(1920, 1080);

        canvasObject.AddComponent<GraphicRaycaster>();

        CreateEventSystem();

        DontDestroyOnLoad(canvasObject);
    }

    public static void AllowBootstrap()
    {
        started = false;
    }

    public void Restart()
    {
        if (restarting)
            return;

        restarting = true;

        BattleRestartHost.Begin(
            this,
            networkObject,
            eventSystemObject,
            canvasObject);
    }

    public void ShutdownForRestart()
    {
        clientLink1?.Disconnect();
        clientLink2?.Disconnect();
        serverLink1?.Disconnect();
        serverLink2?.Disconnect();

        Destroy(gameObject);
    }

    private void CreateEventSystem()
    {
        EventSystem existing =
            EventSystem.current;

        if (existing != null)
            return;

        GameObject eventSystemObject =
            new GameObject("EventSystem");

        this.eventSystemObject = eventSystemObject;

#if ENABLE_INPUT_SYSTEM
        eventSystemObject.AddComponent<EventSystem>();

        UnityEngine.InputSystem.UI.InputSystemUIInputModule module =
            eventSystemObject.AddComponent<
                UnityEngine.InputSystem.UI.InputSystemUIInputModule>();

        if (module.actionsAsset == null)
            module.AssignDefaultActions();
#else
        eventSystemObject.AddComponent<EventSystem>();
        eventSystemObject.AddComponent<StandaloneInputModule>();
#endif

        eventSystemObject.transform.SetParent(
            canvas.transform,
            false);

        DontDestroyOnLoad(eventSystemObject);
    }

    private void CreateNetwork()
    {
        GameObject networkObject =
            new GameObject("NetworkManager");

        this.networkObject = networkObject;

        clientTransport1 =
            networkObject.AddComponent<LocalTransport>();

        serverTransport1 =
            networkObject.AddComponent<LocalTransport>();

        clientTransport2 =
            networkObject.AddComponent<LocalTransport>();

        serverTransport2 =
            networkObject.AddComponent<LocalTransport>();

        Configure(clientTransport1, serverTransport1);
        Configure(clientTransport2, serverTransport2);

        clientLink1 =
            new ReliableOrderedTransport(
                clientTransport1,
                clientTransport1,
                retryTimeout,
                maxRetries)
            {
                VerboseLogging = verboseTransportLogging
            };

        clientLink2 =
            new ReliableOrderedTransport(
                clientTransport2,
                clientTransport2,
                retryTimeout,
                maxRetries)
            {
                VerboseLogging = verboseTransportLogging
            };

        serverLink1 =
            new ReliableOrderedTransport(
                serverTransport1,
                serverTransport1,
                retryTimeout,
                maxRetries)
            {
                VerboseLogging = verboseTransportLogging
            };

        serverLink2 =
            new ReliableOrderedTransport(
                serverTransport2,
                serverTransport2,
                retryTimeout,
                maxRetries)
            {
                VerboseLogging = verboseTransportLogging
            };

        client1 =
            new BattleClient("Player1", clientLink1);

        client2 =
            new BattleClient("Player2", clientLink2);

        DontDestroyOnLoad(networkObject);
    }

    private void Configure(
        LocalTransport clientSide,
        LocalTransport serverSide)
    {
        clientSide.LatencyMs = latencyMs;
        clientSide.PacketLoss = packetLoss;

        serverSide.LatencyMs = latencyMs;
        serverSide.PacketLoss = packetLoss;

        clientSide.Initialize();
        serverSide.Initialize();

        clientSide.SetRemote(serverSide);
        serverSide.SetRemote(clientSide);
    }

    private void CreateServer()
    {
        server =
            new BattleServer(
                serverLink1,
                serverLink2,
                config);
    }

    private void CreateUI()
    {
        GameObject uiRoot =
            new GameObject(
                "BattleGameUI",
                typeof(RectTransform));

        uiRoot.transform.SetParent(
            canvas.transform,
            false);

        RectTransform uiRect =
            uiRoot.GetComponent<RectTransform>();

        uiRect.anchorMin = Vector2.zero;
        uiRect.anchorMax = Vector2.one;
        uiRect.offsetMin = Vector2.zero;
        uiRect.offsetMax = Vector2.zero;

        PlayerSlot slot1 =
            CreatePlayerPanel(
                uiRoot.transform,
                "Player1_Panel",
                "PLAYER 1",
                new Vector2(0f, 0f),
                new Vector2(0.5f, 1f));

        PlayerSlot slot2 =
            CreatePlayerPanel(
                uiRoot.transform,
                "Player2_Panel",
                "PLAYER 2",
                new Vector2(0.5f, 0f),
                new Vector2(1f, 1f));

        CreateLegend(uiRoot.transform);
        restartButton = CreateRestartButton(uiRoot.transform);

        gameUI =
            uiRoot.AddComponent<BattleGameUI>();

        gameUI.player1 = slot1;
        gameUI.player2 = slot2;
        gameUI.autoReconnect = true;
        gameUI.setup = this;
        gameUI.restartButton = restartButton;

        gameUI.Initialize(client1, client2);
    }

    private PlayerSlot CreatePlayerPanel(
        Transform parent,
        string name,
        string title,
        Vector2 anchorMin,
        Vector2 anchorMax)
    {
        GameObject panel =
            new GameObject(
                name,
                typeof(RectTransform),
                typeof(Image));

        panel.transform.SetParent(parent, false);

        RectTransform rect =
            panel.GetComponent<RectTransform>();

        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = new Vector2(10f, 10f);
        rect.offsetMax = new Vector2(-10f, -10f);

        panel.GetComponent<Image>().color =
            new Color(0.07f, 0.08f, 0.12f, 1f);

        VerticalLayoutGroup layout =
            panel.AddComponent<VerticalLayoutGroup>();

        layout.spacing = 8f;
        layout.padding = new RectOffset(12, 12, 12, 12);
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        CreateLabel(
            panel.transform,
            "Title",
            title,
            26,
            FontStyle.Bold,
            new Color(0.85f, 0.90f, 1f, 1f),
            30f);

        Text status =
            CreateLabel(
                panel.transform,
                "Status",
                "...",
                16,
                FontStyle.Normal,
                new Color(0.75f, 0.80f, 0.90f, 1f),
                56f);

        GameObject fieldsRow =
            CreateRow(panel.transform, "FieldsRow");

        PlayerSlot slot = new PlayerSlot
        {
            OwnFieldParent =
                CreateFieldContainer(
                    fieldsRow.transform,
                    "OwnField",
                    "MY FIELD"),
            EnemyFieldParent =
                CreateFieldContainer(
                    fieldsRow.transform,
                    "EnemyField",
                    "ENEMY FIELD"),
            Status = status
        };

        GameObject buttonsRow =
            CreateRow(panel.transform, "ButtonsRow");

        slot.ReconnectButton =
            CreateButton(
                buttonsRow.transform,
                "Reconnect",
                new Color(0.20f, 0.45f, 0.30f, 1f));

        slot.DisconnectButton =
            CreateButton(
                buttonsRow.transform,
                "Disconnect",
                new Color(0.50f, 0.20f, 0.20f, 1f));

        return slot;
    }

    private GameObject CreateRow(
        Transform parent,
        string name)
    {
        GameObject row =
            new GameObject(
                name,
                typeof(RectTransform));

        row.transform.SetParent(parent, false);

        HorizontalLayoutGroup layout =
            row.AddComponent<HorizontalLayoutGroup>();

        layout.spacing = 12f;
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = true;

        return row;
    }

    private RectTransform CreateFieldContainer(
        Transform parent,
        string name,
        string label)
    {
        GameObject container =
            new GameObject(
                name,
                typeof(RectTransform));

        container.transform.SetParent(parent, false);

        VerticalLayoutGroup layout =
            container.AddComponent<VerticalLayoutGroup>();

        layout.spacing = 6f;
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = true;

        CreateLabel(
            container.transform,
            "Label",
            label,
            15,
            FontStyle.Bold,
            new Color(0.70f, 0.78f, 0.95f, 1f),
            22f);

        GameObject holder =
            new GameObject(
                "Holder",
                typeof(RectTransform));

        holder.transform.SetParent(
            container.transform,
            false);

        LayoutElement element =
            holder.AddComponent<LayoutElement>();

        element.flexibleWidth = 1f;
        element.flexibleHeight = 1f;

        return holder.GetComponent<RectTransform>();
    }

    private void CreateLegend(Transform parent)
    {
        GameObject legend =
            new GameObject(
                "Legend",
                typeof(RectTransform),
                typeof(Image));

        legend.transform.SetParent(parent, false);

        RectTransform rect =
            legend.GetComponent<RectTransform>();

        rect.anchorMin = new Vector2(0.5f, 0f);
        rect.anchorMax = new Vector2(0.5f, 0f);
        rect.pivot = new Vector2(0.5f, 0f);
        rect.anchoredPosition = new Vector2(0f, 8f);
        rect.sizeDelta = new Vector2(900f, 34f);

        Image legendBackground =
            legend.GetComponent<Image>();

        legendBackground.color =
            new Color(0.07f, 0.08f, 0.12f, 0.9f);

        legendBackground.raycastTarget = false;

        Text text =
            CreateLabel(
                legend.transform,
                "Text",
                "empty = not fired   |   miss   |   " +
                "ship   |   hit   |   sunk   |   " +
                "pending (shot in flight)",
                15,
                FontStyle.Normal,
                new Color(0.85f, 0.88f, 0.95f, 1f),
                0f);

        LayoutElement element =
            text.gameObject.AddComponent<LayoutElement>();

        element.ignoreLayout = true;

        RectTransform textRect =
            text.rectTransform;

        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;
    }

    private Text CreateLabel(
        Transform parent,
        string name,
        string content,
        int fontSize,
        FontStyle style,
        Color color,
        float height)
    {
        GameObject labelObject =
            new GameObject(
                name,
                typeof(RectTransform));

        labelObject.transform.SetParent(parent, false);

        Text text =
            labelObject.AddComponent<Text>();

        text.text = content;
        text.font = BattleFieldView.GetDefaultFont();
        text.fontSize = fontSize;
        text.fontStyle = style;
        text.color = color;
        text.alignment = TextAnchor.MiddleCenter;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.raycastTarget = false;

        if (height > 0f)
        {
            LayoutElement element =
                labelObject.AddComponent<LayoutElement>();

            element.preferredHeight = height;
            element.minHeight = height;
        }

        return text;
    }

    private Button CreateButton(
        Transform parent,
        string label,
        Color color)
    {
        GameObject buttonObject =
            new GameObject(
                $"{label}Button",
                typeof(RectTransform),
                typeof(Image));

        buttonObject.transform.SetParent(parent, false);

        Image image =
            buttonObject.GetComponent<Image>();

        image.color = color;

        Button button =
            buttonObject.AddComponent<Button>();

        button.transition = Selectable.Transition.None;
        button.targetGraphic = image;

        LayoutElement element =
            buttonObject.AddComponent<LayoutElement>();

        element.preferredHeight = 32f;
        element.minHeight = 32f;
        element.flexibleWidth = 1f;

        Text text =
            CreateLabel(
                buttonObject.transform,
                "Label",
                label,
                15,
                FontStyle.Bold,
                Color.white,
                0f);

        RectTransform textRect = text.rectTransform;

        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        return button;
    }

    private Button CreateRestartButton(Transform parent)
    {
        GameObject root =
            new GameObject(
                "RestartBar",
                typeof(RectTransform));

        root.transform.SetParent(parent, false);

        RectTransform rect =
            root.GetComponent<RectTransform>();

        rect.anchorMin = new Vector2(0.5f, 0f);
        rect.anchorMax = new Vector2(0.5f, 0f);
        rect.pivot = new Vector2(0.5f, 0f);
        rect.anchoredPosition = new Vector2(0f, 48f);
        rect.sizeDelta = new Vector2(220f, 34f);

        Button button =
            CreateButton(
                root.transform,
                "Restart",
                new Color(0.55f, 0.30f, 0.10f, 1f));

        RectTransform buttonRect =
            button.GetComponent<RectTransform>();

        buttonRect.anchorMin = Vector2.zero;
        buttonRect.anchorMax = Vector2.one;
        buttonRect.offsetMin = Vector2.zero;
        buttonRect.offsetMax = Vector2.zero;

        LayoutElement element =
            button.GetComponent<LayoutElement>();

        if (element != null)
            Destroy(element);

        return button;
    }
}
