using UnityEngine;

public class BattleServer
{
    private readonly BattleshipConfig config;

    private readonly BattlePlayer player1;
    private readonly BattlePlayer player2;

    private bool gameStarted;
    private bool gameOver;
    private int winnerPlayerId = -1;

    private bool IsPaused =>
        gameStarted &&
        !gameOver &&
        (!player1.IsConnected || !player2.IsConnected);

    private int currentPlayerId = 1;

    public BattleServer(
        INetworkTransport player1Transport,
        INetworkTransport player2Transport,
        BattleshipConfig config)
    {
        if (config == null)
        {
            throw new System.ArgumentNullException(
                nameof(config));
        }

        if (player1Transport == null)
        {
            throw new System.ArgumentNullException(
                nameof(player1Transport));
        }

        if (player2Transport == null)
        {
            throw new System.ArgumentNullException(
                nameof(player2Transport));
        }

        this.config = config;

        player1 =
            new BattlePlayer(
                1,
                player1Transport,
                config.boardWidth,
                config.boardHeight);

        player2 =
            new BattlePlayer(
                2,
                player2Transport,
                config.boardWidth,
                config.boardHeight);

        BattleShipPlacer.PlaceShips(
            player1.Board,
            config);

        BattleShipPlacer.PlaceShips(
            player2.Board,
            config);

        Subscribe(player1);
        Subscribe(player2);

        StartGame();
    }

    public void Tick(float deltaTime)
    {
        player1.Tick(deltaTime);
        player2.Tick(deltaTime);

        CheckReconnectTimeout(player1);
        CheckReconnectTimeout(player2);
    }

    private void Subscribe(
        BattlePlayer player)
    {
        player.Transport.MessageReceived +=
            data => OnMessageReceived(
                player,
                data);

        player.Transport.Disconnected +=
            () => OnPlayerDisconnected(player);
    }

    private void StartGame()
    {
        gameStarted = true;
        gameOver = false;
        currentPlayerId = 1;

        Debug.Log(
            "[BattleServer] Game started.");

        SendGameStarted(player1);
        SendGameStarted(player2);

        SendShipPlacement(player1);
        SendShipPlacement(player2);

        SendTurnChanged();
    }

    private void OnMessageReceived(
        BattlePlayer sender,
        byte[] data)
    {
        if (data == null || data.Length == 0)
            return;

        MessageType type =
            MessageSerializer.GetMessageType(data);

        if (type == MessageType.ReconnectRequest)
        {
            HandleReconnectRequest(sender, data);
            return;
        }

        if (!gameStarted || gameOver)
            return;

        switch (type)
        {
            case MessageType.Fire:
                HandleFire(sender, data);
                break;

            case MessageType.Ping:
                HandlePing(sender, data);
                break;

            default:
                Debug.LogWarning(
                    $"[BattleServer] " +
                    $"Unexpected message from " +
                    $"Player {sender.Id}: {type}");

                break;
        }
    }

    private void RejectFire(
        BattlePlayer attacker,
        FireMessage fire,
        string reason)
    {
        SendNotice(attacker, reason);

        FireResultMessage rejection =
            new FireResultMessage(
                fire.X,
                fire.Y,
                false,
                false,
                false,
                true);

        Send(
            attacker,
            MessageType.FireResult,
            rejection);
    }

    private void HandleFire(
        BattlePlayer attacker,
        byte[] data)
    {
        FireMessage fire =
            MessageSerializer.Deserialize<FireMessage>(
                data);

        if (attacker.Id != currentPlayerId)
        {
            Debug.LogWarning(
                $"[BattleServer] " +
                $"Player {attacker.Id} tried to fire " +
                $"out of turn.");

            RejectFire(attacker, fire, "Not your turn.");

            return;
        }

        if (!gameStarted)
        {
            RejectFire(
                attacker,
                fire,
                "The game has not started yet.");

            return;
        }

        if (gameOver)
        {
            RejectFire(attacker, fire, "The game is over.");

            return;
        }

        if (IsPaused)
        {
            RejectFire(
                attacker,
                fire,
                "Game paused: waiting for opponent.");

            return;
        }

        BattlePlayer defender =
            GetOpponent(attacker);

        if (!defender.Board.IsInside(
                fire.X,
                fire.Y))
        {
            Debug.LogWarning(
                $"[BattleServer] " +
                $"Player {attacker.Id} fired " +
                $"outside the board: " +
                $"({fire.X}, {fire.Y})");

            RejectFire(
                attacker,
                fire,
                "Shot outside the board.");

            return;
        }

        FireResultMessage result =
            defender.Board.ReceiveShot(
                fire.X,
                fire.Y);

        Send(
            attacker,
            MessageType.FireResult,
            result);

        if (result.AlreadyShot)
        {
            Debug.Log(
                $"[BattleServer] " +
                $"Player {attacker.Id} already shot " +
                $"at ({fire.X}, {fire.Y})");

            return;
        }

        OpponentFireMessage opponentFire =
            new OpponentFireMessage(
                fire.X,
                fire.Y,
                result.Hit,
                result.Sunk);

        Send(
            defender,
            MessageType.OpponentFire,
            opponentFire);

        if (defender.Board.RemainingShipCells <= 0)
        {
            EndGame(attacker);
            return;
        }

        // if (!result.Hit)
        // {
            currentPlayerId = defender.Id;
        // }

        SendTurnChanged();
    }

    private void HandlePing(
        BattlePlayer player,
        byte[] data)
    {
        PingMessage ping =
            MessageSerializer.Deserialize<PingMessage>(
                data);

        PongMessage pong =
            new PongMessage(ping.Id);

        Send(
            player,
            MessageType.Pong,
            pong);
    }

    private void HandleReconnectRequest(
        BattlePlayer player,
        byte[] data)
    {
        ReconnectRequestMessage request =
            MessageSerializer.Deserialize<ReconnectRequestMessage>(
                data);

        if (request.PlayerId != player.Id)
        {
            Debug.LogWarning(
                $"[BattleServer] " +
                $"Reconnect PlayerId mismatch: " +
                $"requested {request.PlayerId}, " +
                $"expected {player.Id}");

            SendNotice(
                player,
                "Reconnect rejected: wrong player id.");

            return;
        }

        player.MarkReconnected();

        GameStateSnapshotMessage snapshot =
            CreateGameStateSnapshot(player);

        Send(
            player,
            MessageType.GameStateSnapshot,
            snapshot);

        Debug.Log(
            $"[BattleServer] " +
            $"Player {player.Id} reconnected. " +
            $"Snapshot sent " +
            $"({snapshot.ShipCount} ships).");

        SendNotice(
            player,
            "Reconnected. State restored.");

        Send(
            GetOpponent(player),
            MessageType.PauseChanged,
            new PauseChangedMessage(IsPaused));

        Send(
            GetOpponent(player),
            MessageType.Notice,
            new NoticeMessage(
                $"Player {player.Id} reconnected. " +
                $"Game resumed."));
    }

    private GameStateSnapshotMessage CreateGameStateSnapshot(
        BattlePlayer player)
    {
        BattlePlayer opponent =
            GetOpponent(player);

        player.Board.GetShipPlacementData(
            out int shipCount,
            out int[] shipSizes,
            out int[] shipStartX,
            out int[] shipStartY,
            out bool[] shipHorizontal,
            out bool[] shipHitCells);

        int boardWidth = player.Board.Width;
        int boardHeight = player.Board.Height;

        bool[,] myShots = new bool[boardWidth, boardHeight];
        bool[,] opponentShots = new bool[boardWidth, boardHeight];
        bool[,] myShotHit = new bool[boardWidth, boardHeight];
        bool[,] myShotSunk = new bool[boardWidth, boardHeight];

        for (int x = 0; x < boardWidth; x++)
        {
            for (int y = 0; y < boardHeight; y++)
            {
                myShots[x, y] =
                    opponent.Board.WasShot(x, y);

                opponentShots[x, y] =
                    player.Board.WasShot(x, y);

                opponent.Board.GetShotOutcome(
                    x,
                    y,
                    out bool hit,
                    out bool sunk);

                myShotHit[x, y] = hit;
                myShotSunk[x, y] = sunk;
            }
        }

        return new GameStateSnapshotMessage(
            player.Id,
            boardWidth,
            boardHeight,
            currentPlayerId,
            gameOver,
            IsPaused,
            winnerPlayerId,
            shipCount,
            shipSizes,
            shipStartX,
            shipStartY,
            shipHorizontal,
            shipHitCells,
            myShots,
            opponentShots,
            myShotHit,
            myShotSunk);
    }

    private void CheckReconnectTimeout(
        BattlePlayer player)
    {
        if (gameOver || !gameStarted)
            return;

        if (player.IsConnected)
            return;

        if (config.reconnectGraceSeconds <= 0f)
            return;

        if (player.DisconnectedFor <
            config.reconnectGraceSeconds)
        {
            return;
        }

        Debug.Log(
            $"[BattleServer] " +
            $"Player {player.Id} did not reconnect " +
            $"within {config.reconnectGraceSeconds:F0}s.");

        SendNotice(
            GetOpponent(player),
            "Opponent disconnected. You win by timeout.");

        EndGame(GetOpponent(player));
    }

    private void SendGameStarted(
        BattlePlayer player)
    {
        GameStartedMessage message =
            new GameStartedMessage(
                player.Id,
                config.boardWidth,
                config.boardHeight);

        Send(
            player,
            MessageType.GameStarted,
            message);
    }

    private void SendShipPlacement(
        BattlePlayer player)
    {
        player.Board.GetShipPlacementData(
            out int shipCount,
            out int[] shipSizes,
            out int[] shipStartX,
            out int[] shipStartY,
            out bool[] shipHorizontal,
            out bool[] shipHitCells);

        ShipPlacementMessage message =
            new ShipPlacementMessage(
                shipCount,
                shipSizes,
                shipStartX,
                shipStartY,
                shipHorizontal,
                shipHitCells);

        Send(
            player,
            MessageType.ShipPlacement,
            message);
    }

    private void SendTurnChanged()
    {
        TurnChangedMessage message =
            new TurnChangedMessage(
                currentPlayerId);

        Send(
            player1,
            MessageType.TurnChanged,
            message);

        Send(
            player2,
            MessageType.TurnChanged,
            message);
    }

    private void SendNotice(
        BattlePlayer player,
        string text)
    {
        Send(
            player,
            MessageType.Notice,
            new NoticeMessage(text));
    }

    private void EndGame(
        BattlePlayer winner)
    {
        if (gameOver)
            return;

        gameOver = true;
        winnerPlayerId = winner.Id;

        Debug.Log(
            $"[BattleServer] " +
            $"Game over. Winner: " +
            $"Player {winner.Id}");

        GameOverMessage message =
            new GameOverMessage(
                winner.Id);

        Send(
            player1,
            MessageType.GameOver,
            message);

        Send(
            player2,
            MessageType.GameOver,
            message);
    }

    private void OnPlayerDisconnected(
        BattlePlayer disconnectedPlayer)
    {
        if (gameOver || !gameStarted)
            return;

        disconnectedPlayer.MarkDisconnected();

        Debug.Log(
            $"[BattleServer] " +
            $"Player {disconnectedPlayer.Id} disconnected. " +
            $"Game paused for " +
            $"{config.reconnectGraceSeconds:F0}s " +
            $"waiting for reconnect.");

        SendNotice(
            GetOpponent(disconnectedPlayer),
            $"Player {disconnectedPlayer.Id} " +
            $"disconnected. Waiting for reconnect...");

        Send(
            GetOpponent(disconnectedPlayer),
            MessageType.PauseChanged,
            new PauseChangedMessage(true));
    }

    private BattlePlayer GetOpponent(
        BattlePlayer player)
    {
        return player.Id == 1
            ? player2
            : player1;
    }

    private void Send<T>(
        BattlePlayer player,
        MessageType type,
        T message)
    {
        if (!player.Transport.IsConnected)
            return;

        byte[] data =
            MessageSerializer.Serialize(
                type,
                message);

        player.Transport.Send(data);
    }
}
