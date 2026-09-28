using UnityEngine;

public class BattleServer
{
    private readonly BattleshipConfig config;

    private readonly BattlePlayer player1;
    private readonly BattlePlayer player2;

    private bool gameStarted;
    private bool gameOver;

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

        SendTurnChanged();
    }

    private void OnMessageReceived(
        BattlePlayer sender,
        byte[] data)
    {
        if (!gameStarted || gameOver)
            return;

        if (data == null || data.Length == 0)
            return;

        MessageType type =
            MessageSerializer.GetMessageType(data);

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

    private void HandleFire(
        BattlePlayer attacker,
        byte[] data)
    {
        FireMessage fire =
            MessageSerializer.Deserialize<FireMessage>(
                data);

        Debug.Log(
            $"[BattleServer] " +
            $"Player {attacker.Id} fires at " +
            $"({fire.X}, {fire.Y})");

        // -------------------------------------------------
        // Проверяем ход.
        // -------------------------------------------------

        if (attacker.Id != currentPlayerId)
        {
            Debug.LogWarning(
                $"[BattleServer] " +
                $"Player {attacker.Id} tried to fire " +
                $"out of turn.");

            return;
        }

        BattlePlayer defender =
            GetOpponent(attacker);

        // -------------------------------------------------
        // Проверяем координаты.
        // -------------------------------------------------

        if (!defender.Board.IsInside(
                fire.X,
                fire.Y))
        {
            Debug.LogWarning(
                $"[BattleServer] " +
                $"Player {attacker.Id} fired " +
                $"outside the board: " +
                $"({fire.X}, {fire.Y})");

            return;
        }

        // -------------------------------------------------
        // Обрабатываем выстрел на поле защитника.
        // -------------------------------------------------

        FireResultMessage result =
            defender.Board.ReceiveShot(
                fire.X,
                fire.Y);

        // -------------------------------------------------
        // Отправляем результат стрелявшему.
        // -------------------------------------------------

        Send(
            attacker,
            MessageType.FireResult,
            result);

        // -------------------------------------------------
        // Если это был повторный выстрел,
        // ход не меняется.
        // -------------------------------------------------

        if (result.AlreadyShot)
        {
            Debug.Log(
                $"[BattleServer] " +
                $"Player {attacker.Id} already shot " +
                $"at ({fire.X}, {fire.Y})");

            return;
        }

        // -------------------------------------------------
        // Уведомляем владельца поля.
        //
        // Здесь игрок узнаёт, куда именно
        // противник стрелял по его полю.
        // -------------------------------------------------

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

        // -------------------------------------------------
        // Проверяем победу.
        // -------------------------------------------------

        if (defender.Board.RemainingShipCells <= 0)
        {
            EndGame(attacker);
            return;
        }

        // -------------------------------------------------
        // Правило хода:
        //
        // HIT  -> игрок продолжает стрелять.
        // MISS -> ход переходит противнику.
        // -------------------------------------------------

        if (!result.Hit)
        {
            currentPlayerId =
                defender.Id;
        }

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

    private void SendGameStarted(
        BattlePlayer player)
    {
        GameStartedMessage message =
            new GameStartedMessage(
                config.boardWidth,
                config.boardHeight);

        Send(
            player,
            MessageType.GameStarted,
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

    private void EndGame(
        BattlePlayer winner)
    {
        if (gameOver)
            return;

        gameOver = true;

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
        if (gameOver)
            return;

        Debug.Log(
            $"[BattleServer] " +
            $"Player {disconnectedPlayer.Id} " +
            $"disconnected.");

        BattlePlayer winner =
            GetOpponent(disconnectedPlayer);

        EndGame(winner);
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