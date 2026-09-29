using UnityEngine;
using UnityEngine.UI;

[System.Serializable]
public class PlayerSlot
{
    public RectTransform OwnFieldParent;
    public RectTransform EnemyFieldParent;
    public Text Status;
    public Button ReconnectButton;
    public Button DisconnectButton;
}

public class BattleGameUI : MonoBehaviour
{
    [Header("Players")]
    public PlayerSlot player1;
    public PlayerSlot player2;

    [Header("Behaviour")]
    public bool autoReconnect = true;
    public float reconnectDelay = 1.5f;

    [Header("Scene")]
    public BattleSceneSetup setup;
    public Button restartButton;

    private BattleClient client1;
    private BattleClient client2;

    private BattleFieldView p1Own;
    private BattleFieldView p1Enemy;
    private BattleFieldView p2Own;
    private BattleFieldView p2Enemy;

    private int lastVersion1 = -1;
    private int lastVersion2 = -1;

    private string lastStatus1;
    private string lastStatus2;

    private float reconnectTimer;

    private bool initialized;

    public void Initialize(
        BattleClient client1,
        BattleClient client2)
    {
        if (client1 == null)
        {
            Debug.LogError(
                "[BattleGameUI] Client 1 is null.");

            return;
        }

        if (client2 == null)
        {
            Debug.LogError(
                "[BattleGameUI] Client 2 is null.");

            return;
        }

        this.client1 = client1;
        this.client2 = client2;

        p1Own = CreateField(
            player1?.OwnFieldParent,
            "P1_OwnField",
            client1,
            true);

        p1Enemy = CreateField(
            player1?.EnemyFieldParent,
            "P1_EnemyField",
            client1,
            false);

        p2Own = CreateField(
            player2?.OwnFieldParent,
            "P2_OwnField",
            client2,
            true);

        p2Enemy = CreateField(
            player2?.EnemyFieldParent,
            "P2_EnemyField",
            client2,
            false);

        BindButton(
            player1?.ReconnectButton,
            client1);

        BindButton(
            player1?.DisconnectButton,
            client1,
            true);

        BindButton(
            player2?.ReconnectButton,
            client2);

        BindButton(
            player2?.DisconnectButton,
            client2,
            true);

        if (restartButton != null && setup != null)
        {
            restartButton.onClick.AddListener(
                () => setup.Restart());
        }

        initialized = true;
    }

    private void Update()
    {
        if (!initialized)
            return;

        if (client1 != null &&
            client1.StateVersion != lastVersion1)
        {
            lastVersion1 = client1.StateVersion;

            p1Own?.Refresh();
            p1Enemy?.Refresh();
        }

        if (client2 != null &&
            client2.StateVersion != lastVersion2)
        {
            lastVersion2 = client2.StateVersion;

            p2Own?.Refresh();
            p2Enemy?.Refresh();
        }

        UpdateStatus();

        HandleReconnect();
    }

    private void HandleReconnect()
    {
        if (!autoReconnect)
            return;

        bool anyDown =
            (client1 != null &&
             !client1.IsConnected &&
             !client1.ManuallyDisconnected) ||
            (client2 != null &&
             !client2.IsConnected &&
             !client2.ManuallyDisconnected);

        if (!anyDown)
        {
            reconnectTimer = 0f;
            return;
        }

        reconnectTimer += Time.deltaTime;

        if (reconnectTimer < reconnectDelay)
            return;

        reconnectTimer = 0f;

        if (client1 != null &&
            !client1.IsConnected &&
            !client1.ManuallyDisconnected)
        {
            Debug.Log(
                "[BattleGameUI] Reconnecting Player 1.");

            client1.Reconnect();
        }

        if (client2 != null &&
            !client2.IsConnected &&
            !client2.ManuallyDisconnected)
        {
            Debug.Log(
                "[BattleGameUI] Reconnecting Player 2.");

            client2.Reconnect();
        }
    }

    private void UpdateStatus()
    {
        RefreshStatus(
            client1,
            player1,
            "P1",
            ref lastStatus1);

        RefreshStatus(
            client2,
            player2,
            "P2",
            ref lastStatus2);
    }

    private void RefreshStatus(
        BattleClient client,
        PlayerSlot slot,
        string label,
        ref string cache)
    {
        if (slot?.Status == null)
            return;

        UpdateButtons(client, slot);

        string text = BuildStatus(client, label);

        if (text == cache)
            return;

        cache = text;
        slot.Status.text = text;
    }

    private void UpdateButtons(
        BattleClient client,
        PlayerSlot slot)
    {
        bool connected =
            client != null && client.IsConnected;

        if (slot.ReconnectButton != null)
            slot.ReconnectButton.interactable = !connected;

        if (slot.DisconnectButton != null)
            slot.DisconnectButton.interactable = connected;
    }

    private string BuildStatus(
        BattleClient client,
        string label)
    {
        if (client == null)
            return $"{label}: —";

        if (!client.IsConnected)
        {
            return client.ManuallyDisconnected
                ? $"{label}: DISCONNECTED\npress Reconnect"
                : $"{label}: DISCONNECTED\nreconnecting...";
        }

        if (!client.GameStarted)
        {
            return $"{label}: waiting for the server...";
        }

        if (client.GameOver)
        {
            string result =
                client.WinnerPlayerId == client.PlayerId
                    ? "VICTORY"
                    : "DEFEAT";

            return $"{label}: GAME OVER - {result}";
        }

        string turn =
            client.IsPaused
                ? "PAUSED"
                : client.IsMyTurn
                    ? "YOUR TURN"
                    : "OPPONENT'S TURN";

        string notice =
            string.IsNullOrEmpty(client.LastNotice)
                ? ""
                : $"\n{client.LastNotice}";

        return
            $"{label}: {turn}\n" +
            $"ships alive: " +
            $"{client.AliveShipCount}/{client.MyShips.Count}" +
            notice;
    }

    private BattleFieldView CreateField(
        RectTransform parent,
        string name,
        BattleClient client,
        bool ownField)
    {
        if (parent == null)
        {
            Debug.LogWarning(
                $"[BattleGameUI] No parent for {name}.");

            return null;
        }

        GameObject fieldObject =
            new GameObject(
                name,
                typeof(RectTransform),
                typeof(Image));

        fieldObject.transform.SetParent(
            parent,
            false);

        Image background =
            fieldObject.GetComponent<Image>();

        background.color =
            ownField
                ? new Color(0.09f, 0.13f, 0.20f, 1f)
                : new Color(0.12f, 0.09f, 0.12f, 1f);

        RectTransform fieldRect =
            fieldObject.GetComponent<RectTransform>();

        fieldRect.anchorMin = Vector2.zero;
        fieldRect.anchorMax = Vector2.one;
        fieldRect.offsetMin = Vector2.zero;
        fieldRect.offsetMax = Vector2.zero;

        BattleFieldView view =
            fieldObject.AddComponent<BattleFieldView>();

        view.Bind(client, ownField);

        return view;
    }

    private void BindButton(
        Button button,
        BattleClient client)
    {
        BindButton(button, client, false);
    }

    private void BindButton(
        Button button,
        BattleClient client,
        bool disconnect)
    {
        if (button == null)
            return;

        button.onClick.RemoveAllListeners();

        if (client == null)
            return;

        if (disconnect)
        {
            button.onClick.AddListener(
                () => client.Disconnect());
        }
        else
        {
            button.onClick.AddListener(
                () => client.Reconnect());
        }
    }
}
