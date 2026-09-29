using UnityEngine;

/// <summary>
/// Обработка входящих сообщений сервера.
/// </summary>
public partial class BattleClient
{
    private void OnMessageReceived(byte[] data)
    {
        if (data == null || data.Length == 0)
            return;

        MessageType type = MessageSerializer.GetMessageType(data);

        switch (type)
        {
            case MessageType.GameStarted:
                HandleGameStarted(data);
                break;

            case MessageType.ShipPlacement:
                HandleShipPlacement(data);
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

            case MessageType.GameStateSnapshot:
                HandleGameStateSnapshot(data);
                break;

            case MessageType.Pong:
                HandlePong(data);
                break;

            case MessageType.Notice:
                HandleNotice(data);
                break;

            case MessageType.PauseChanged:
                HandlePauseChanged(data);
                break;

            default:
                Log(
                    $"Unknown message type: {type}",
                    true);

                break;
        }
    }

    private void HandleGameStarted(byte[] data)
    {
        GameStartedMessage message =
            MessageSerializer.Deserialize<GameStartedMessage>(
                data);

        PlayerId = message.PlayerId;

        gameStarted = true;
        gameOver = false;
        winnerPlayerId = -1;

        if (boardWidth != message.BoardWidth ||
            boardHeight != message.BoardHeight)
        {
            boardWidth = message.BoardWidth;
            boardHeight = message.BoardHeight;

            AllocateBoard();
        }

        Log(
            $"Game started as Player {PlayerId}. " +
            $"Board: {boardWidth}x{boardHeight}");

        MarkDirty();
    }

    private void HandleShipPlacement(byte[] data)
    {
        ShipPlacementMessage message =
            MessageSerializer.Deserialize<ShipPlacementMessage>(
                data);

        RebuildShips(
            message.ShipCount,
            message.ShipSizes,
            message.ShipStartX,
            message.ShipStartY,
            message.ShipHorizontal,
            message.ShipHitCells);

        Log(
            $"Received ship placement: " +
            $"{myShips.Count} ships.");

        MarkDirty();
    }

    private void HandleFireResult(byte[] data)
    {
        FireResultMessage result =
            MessageSerializer.Deserialize<FireResultMessage>(
                data);

        if (!IsInsideBoard(result.X, result.Y))
        {
            Log(
                $"Server returned a result outside " +
                $"the board: ({result.X}, {result.Y})",
                true);

            return;
        }

        Vector2Int cell = new Vector2Int(result.X, result.Y);
        bool wasPending = pendingShots.Remove(cell);

        pendingSince.Remove(cell);

        if (result.Rejected)
        {
            Log(
                $"Shot at ({result.X}, {result.Y}) " +
                $"was rejected by the server.");

            MarkDirty();

            return;
        }

        if (result.AlreadyShot)
        {
            myShots[result.X, result.Y] = true;

            Log(
                $"Server rejected a duplicate shot " +
                $"at ({result.X}, {result.Y})");

            MarkDirty();

            return;
        }

        myShots[result.X, result.Y] = true;
        myShotHit[result.X, result.Y] = result.Hit;
        myShotSunk[result.X, result.Y] = result.Sunk;

        if (result.Sunk)
        {
            Log(
                $"MY SHOT ({result.X}, {result.Y}) = " +
                $"HIT + SUNK");
        }
        else if (result.Hit)
        {
            Log(
                $"MY SHOT ({result.X}, {result.Y}) = HIT");
        }
        else
        {
            Log(
                $"MY SHOT ({result.X}, {result.Y}) = MISS");
        }

        if (!wasPending)
        {
            Log(
                $"Unexpected FireResult for a shot " +
                $"that was never sent: " +
                $"({result.X}, {result.Y})",
                true);
        }

        MarkDirty();
    }

    private void HandleOpponentFire(byte[] data)
    {
        OpponentFireMessage message =
            MessageSerializer.Deserialize<OpponentFireMessage>(
                data);

        if (!IsInsideBoard(message.X, message.Y))
        {
            Log(
                $"Received opponent shot " +
                $"outside board: ({message.X}, {message.Y})",
                true);

            return;
        }

        opponentShots[message.X, message.Y] = true;

        if (!message.Hit)
        {
            Log(
                $"OPPONENT SHOT " +
                $"({message.X}, {message.Y}) = MISS");

            MarkDirty();

            return;
        }

        bool onShip =
            MarkShipHit(message.X, message.Y);

        if (!onShip)
        {
            Log(
                $"Server reported a hit at " +
                $"({message.X}, {message.Y}) but there is " +
                $"no ship there.",
                true);
        }

        if (message.Sunk)
        {
            Log(
                $"OPPONENT SHOT " +
                $"({message.X}, {message.Y}) = " +
                $"HIT + SUNK");
        }
        else
        {
            Log(
                $"OPPONENT SHOT " +
                $"({message.X}, {message.Y}) = HIT");
        }

        MarkDirty();
    }

    private void HandleTurnChanged(byte[] data)
    {
        TurnChangedMessage message =
            MessageSerializer.Deserialize<TurnChangedMessage>(
                data);

        currentPlayerId = message.PlayerId;

        Log(
            currentPlayerId == PlayerId
                ? "MY TURN"
                : "OPPONENT'S TURN");

        MarkDirty();
    }

    private void HandleGameOver(byte[] data)
    {
        GameOverMessage message =
            MessageSerializer.Deserialize<GameOverMessage>(
                data);

        gameOver = true;
        winnerPlayerId = message.WinnerPlayerId;

        Log(
            winnerPlayerId == PlayerId
                ? "GAME OVER - I WIN!"
                : "GAME OVER - I LOSE.");

        MarkDirty();
    }

    private void HandlePauseChanged(byte[] data)
    {
        PauseChangedMessage message =
            MessageSerializer.Deserialize<PauseChangedMessage>(
                data);

        IsPaused = message.Paused;

        if (!IsPaused)
            ClearNotice();

        MarkDirty();
    }

    private void HandleGameStateSnapshot(byte[] data)
    {
        GameStateSnapshotMessage snapshot =
            MessageSerializer.Deserialize<GameStateSnapshotMessage>(
                data);

        ApplySnapshot(snapshot);

        Log(
            $"Snapshot applied. " +
            $"Ships: {myShips.Count}, " +
            $"Alive: {AliveShipCount}");

        MarkDirty();
    }

    private void HandleNotice(byte[] data)
    {
        NoticeMessage message =
            MessageSerializer.Deserialize<NoticeMessage>(
                data);

        LastNotice = message.Text ?? "";

        Log($"NOTICE: {LastNotice}");

        MarkDirty();
    }

    private void HandlePong(byte[] data)
    {
        PongMessage pong =
            MessageSerializer.Deserialize<PongMessage>(
                data);

        if (!pendingPings.TryGetValue(
                pong.Id,
                out float startTime))
        {
            Log(
                $"Unknown pong #{pong.Id}",
                true);

            return;
        }

        pendingPings.Remove(pong.Id);

        float rtt =
            (Time.realtimeSinceStartup - startTime) *
            1000f;

        Log(
            $"Pong #{pong.Id}, RTT = {rtt:F1} ms");
    }
}
