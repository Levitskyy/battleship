using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Вместо события используется счётчик StateVersion:
/// любой, кто изменил данные, вызывает MarkDirty(), а UI в
/// Update() сравнивает текущий StateVersion с запомненным и,
/// если он разошёлся, перерисовывает поля
/// </summary>
public partial class BattleClient
{
    private readonly string clientName;
    private readonly INetworkTransport transport;

    private int nextPingId = 1;

    private readonly Dictionary<int, float> pendingPings =
        new Dictionary<int, float>();

    private int boardWidth;
    private int boardHeight;

    private bool gameStarted;
    private bool gameOver;
    private int currentPlayerId;
    private int winnerPlayerId = -1;

    private bool[,] myShots;
    private bool[,] myShotHit;
    private bool[,] myShotSunk;

    private bool[,] opponentShots;

    private int[,] myShipGrid;
    private bool[,] myShipHit;
    private bool[] shipSunk;

    private readonly HashSet<Vector2Int> pendingShots =
        new HashSet<Vector2Int>();

    [Tooltip("Сколько ждать ответа сервера на выстрел, " +
             "прежде чем снять жёлтую подсветку.")]
    [Min(0.5f)]
    public float shotResponseTimeout = 8f;

    private readonly Dictionary<Vector2Int, float> pendingSince =
        new Dictionary<Vector2Int, float>();

    private readonly List<ShipData> myShips =
        new List<ShipData>();

    public int PlayerId { get; private set; }

    public int StateVersion { get; private set; }

    public int BoardWidth => boardWidth;
    public int BoardHeight => boardHeight;

    public bool GameStarted => gameStarted;
    public bool GameOver => gameOver;
    public bool IsConnected => transport.IsConnected;
    public bool IsPaused { get; private set; }

    public bool ManuallyDisconnected { get; private set; }

    public int WinnerPlayerId => winnerPlayerId;

    public string LastNotice { get; private set; } = "";

    public IReadOnlyList<ShipData> MyShips => myShips;

    public bool IsMyTurn =>
        gameStarted &&
        !gameOver &&
        currentPlayerId == PlayerId;

    public bool IsBoardReady =>
        boardWidth > 0 &&
        boardHeight > 0;

    public int AliveShipCount
    {
        get
        {
            int result = 0;

            for (int i = 0; i < myShips.Count; i++)
            {
                if (!shipSunk[i])
                    result++;
            }

            return result;
        }
    }

    public BattleClient(
        string clientName,
        INetworkTransport transport)
    {
        this.clientName =
            clientName;

        if (transport == null)
        {
            throw new ArgumentNullException(
                nameof(transport));
        }

        this.transport = transport;

        transport.MessageReceived +=
            OnMessageReceived;

        transport.Disconnected +=
            OnDisconnected;
    }

    public void Update(float deltaTime)
    {
        ExpireStaleShots(deltaTime);
    }

    public bool IsInsideBoard(
        int x,
        int y)
    {
        return
            x >= 0 &&
            x < boardWidth &&
            y >= 0 &&
            y < boardHeight;
    }

    public void ClearNotice()
    {
        if (string.IsNullOrEmpty(LastNotice))
            return;

        LastNotice = "";

        MarkDirty();
    }

    /// <summary>
    /// Помечает состояние как изменившееся: инкрементит
    /// StateVersion, и UI в следующем Update() перерисует поля.
    /// Вызывать нужно после любого изменения данных, которое
    /// видно игроку.
    /// </summary>
    private void MarkDirty()
    {
        StateVersion++;
    }

    private void OnDisconnected()
    {
        pendingPings.Clear();
        pendingShots.Clear();
        pendingSince.Clear();

        MarkDirty();

        Log("Disconnected.");
    }

    private void Log(
        string message,
        bool warning = false)
    {
        string line =
            $"[{clientName}] {message}";

        if (warning)
        {
            Debug.LogWarning(line);
        }
        else
        {
            Debug.Log(line);
        }
    }
}
