using System.Collections.Generic;
using UnityEngine;

public class BattleClient
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

    // мои выстрелы по полю противника
    // true = я уже стрелял сюда
    private bool[,] myShots;

    // выстрелы противника по моему полю
    // true = противник уже стрелял сюда
    private bool[,] opponentShots;

    public int PlayerId { get; private set; }

    public bool IsMyTurn =>
        gameStarted &&
        !gameOver &&
        currentPlayerId == PlayerId;

    public BattleClient(
        string clientName,
        INetworkTransport transport)
    {
        this.clientName = clientName;
        this.transport = transport;

        transport.MessageReceived +=
            OnMessageReceived;

        transport.Disconnected +=
            OnDisconnected;
    }

    public void SetPlayerId(int playerId)
    {
        PlayerId = playerId;
    }

    public void Fire(int x, int y)
    {
        if (!transport.IsConnected)
        {
            Debug.LogWarning(
                $"[{clientName}] " +
                $"Can't fire. Disconnected.");

            return;
        }

        if (!gameStarted)
        {
            Debug.LogWarning(
                $"[{clientName}] " +
                $"Can't fire. Game hasn't started.");

            return;
        }

        if (gameOver)
        {
            Debug.LogWarning(
                $"[{clientName}] " +
                $"Can't fire. Game is over.");

            return;
        }

        if (!IsMyTurn)
        {
            Debug.LogWarning(
                $"[{clientName}] " +
                $"Can't fire. It's not my turn.");

            return;
        }

        if (!IsInsideBoard(x, y))
        {
            Debug.LogWarning(
                $"[{clientName}] " +
                $"Can't fire outside board: " +
                $"({x}, {y})");

            return;
        }

        if (myShots[x, y])
        {
            Debug.LogWarning(
                $"[{clientName}] " +
                $"Already fired at ({x}, {y}).");

            return;
        }

        myShots[x, y] = true;

        FireMessage message =
            new FireMessage(x, y);

        byte[] data =
            MessageSerializer.Serialize(
                MessageType.Fire,
                message);

        Debug.Log(
            $"[{clientName}] " +
            $"Fire ({x}, {y})");

        transport.Send(data);
    }

    public void Ping()
    {
        if (!transport.IsConnected)
        {
            Debug.LogWarning(
                $"[{clientName}] " +
                $"Can't ping. Disconnected.");

            return;
        }

        int pingId =
            nextPingId++;

        PingMessage message =
            new PingMessage(pingId);

        pendingPings[pingId] =
            Time.realtimeSinceStartup;

        byte[] data =
            MessageSerializer.Serialize(
                MessageType.Ping,
                message);

        Debug.Log(
            $"[{clientName}] Ping #{pingId}");

        transport.Send(data);
    }

    private void OnMessageReceived(
        byte[] data)
    {
        MessageType type =
            MessageSerializer.GetMessageType(data);

        switch (type)
        {
            case MessageType.GameStarted:
                HandleGameStarted(data);
                break;

            case MessageType.FireResult:
                HandleFireResult(data);
                break;

            case MessageType.OpponentFire:
                HandleOpponentFire(data);
                break;

            case MessageType.TurnChanged:
                HandleTurnChanged(data);
                break;

            case MessageType.GameOver:
                HandleGameOver(data);
                break;

            case MessageType.Pong:
                HandlePong(data);
                break;

            default:
                Debug.LogWarning(
                    $"[{clientName}] " +
                    $"Unknown message type: {type}");

                break;
        }
    }

    private void HandleGameStarted(
        byte[] data)
    {
        GameStartedMessage message =
            MessageSerializer.Deserialize<GameStartedMessage>(
                data);

        boardWidth =
            message.BoardWidth;

        boardHeight =
            message.BoardHeight;

        myShots =
            new bool[
                boardWidth,
                boardHeight];

        opponentShots =
            new bool[
                boardWidth,
                boardHeight];

        gameStarted = true;
        gameOver = false;

        Debug.Log(
            $"[{clientName}] " +
            $"Game started. Board: " +
            $"{boardWidth}x{boardHeight}");
    }

    private void HandleFireResult(
        byte[] data)
    {
        FireResultMessage result =
            MessageSerializer.Deserialize<FireResultMessage>(
                data);

        if (result.AlreadyShot)
        {
            Debug.Log(
                $"[{clientName}] " +
                $"Server rejected duplicate shot " +
                $"at ({result.X}, {result.Y})");

            return;
        }

        if (!result.Hit)
        {
            Debug.Log(
                $"[{clientName}] " +
                $"MY SHOT ({result.X}, {result.Y}) = MISS");

            return;
        }

        if (result.Sunk)
        {
            Debug.Log(
                $"[{clientName}] " +
                $"MY SHOT ({result.X}, {result.Y}) = " +
                $"HIT + SUNK");
        }
        else
        {
            Debug.Log(
                $"[{clientName}] " +
                $"MY SHOT ({result.X}, {result.Y}) = HIT");
        }
    }

    private void HandleOpponentFire(
        byte[] data)
    {
        OpponentFireMessage message =
            MessageSerializer.Deserialize<OpponentFireMessage>(
                data);

        if (!IsInsideBoard(
            message.X,
            message.Y))
        {
            Debug.LogWarning(
                $"[{clientName}] " +
                $"Received opponent shot " +
                $"outside board: " +
                $"({message.X}, {message.Y})");

            return;
        }

        opponentShots[
            message.X,
            message.Y] = true;

        if (!message.Hit)
        {
            Debug.Log(
                $"[{clientName}] " +
                $"OPPONENT SHOT " +
                $"({message.X}, {message.Y}) = MISS");

            return;
        }

        if (message.Sunk)
        {
            Debug.Log(
                $"[{clientName}] " +
                $"OPPONENT SHOT " +
                $"({message.X}, {message.Y}) = " +
                $"HIT + SUNK");
        }
        else
        {
            Debug.Log(
                $"[{clientName}] " +
                $"OPPONENT SHOT " +
                $"({message.X}, {message.Y}) = HIT");
        }
    }

    private void HandleTurnChanged(
        byte[] data)
    {
        TurnChangedMessage message =
            MessageSerializer.Deserialize<TurnChangedMessage>(
                data);

        currentPlayerId =
            message.PlayerId;

        if (currentPlayerId == PlayerId)
        {
            Debug.Log(
                $"[{clientName}] " +
                $"MY TURN");
        }
        else
        {
            Debug.Log(
                $"[{clientName}] " +
                $"OPPONENT'S TURN");
        }
    }

    private void HandleGameOver(
        byte[] data)
    {
        GameOverMessage message =
            MessageSerializer.Deserialize<GameOverMessage>(
                data);

        gameOver = true;

        if (message.WinnerPlayerId == PlayerId)
        {
            Debug.Log(
                $"[{clientName}] " +
                $"GAME OVER — I WIN!");
        }
        else
        {
            Debug.Log(
                $"[{clientName}] " +
                $"GAME OVER — I LOSE.");
        }
    }

    private void HandlePong(
        byte[] data)
    {
        PongMessage pong =
            MessageSerializer.Deserialize<PongMessage>(
                data);

        if (!pendingPings.TryGetValue(
            pong.Id,
            out float startTime))
        {
            Debug.LogWarning(
                $"[{clientName}] " +
                $"Unknown pong #{pong.Id}");

            return;
        }

        pendingPings.Remove(pong.Id);

        float rtt =
            (Time.realtimeSinceStartup - startTime) *
            1000f;

        Debug.Log(
            $"[{clientName}] " +
            $"Pong #{pong.Id}, " +
            $"RTT = {rtt:F1} ms");
    }

    private void OnDisconnected()
    {
        Debug.Log(
            $"[{clientName}] Disconnected.");

        pendingPings.Clear();
    }

    private bool IsInsideBoard(
        int x,
        int y)
    {
        return
            x >= 0 &&
            x < boardWidth &&
            y >= 0 &&
            y < boardHeight;
    }

    public bool WasMyShot(
        int x,
        int y)
    {
        if (!IsInsideBoard(x, y))
            return false;

        return myShots[x, y];
    }

    public bool WasOpponentShot(
        int x,
        int y)
    {
        if (!IsInsideBoard(x, y))
            return false;

        return opponentShots[x, y];
    }

    public void Disconnect()
    {
        transport.Disconnect();
    }
}