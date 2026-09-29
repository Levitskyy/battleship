using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Исходящие запросы клиента: выстрел, пинг, переподключение.
/// </summary>
public partial class BattleClient
{
    public void Fire(int x, int y)
    {
        if (!transport.IsConnected)
        {
            Log(
                "Can't fire. Disconnected.",
                true);

            return;
        }

        if (!gameStarted)
        {
            Log(
                "Can't fire. Game hasn't started.",
                true);

            return;
        }

        if (gameOver)
        {
            Log(
                "Can't fire. Game is over.",
                true);

            return;
        }

        if (!IsMyTurn)
        {
            Log(
                "Can't fire. It's not my turn.",
                true);

            return;
        }

        if (!IsInsideBoard(x, y))
        {
            Log(
                $"Can't fire outside board: ({x}, {y})",
                true);

            return;
        }

        if (myShots[x, y])
        {
            Log(
                $"Already fired at ({x}, {y}).",
                true);

            return;
        }

        Vector2Int cell = new Vector2Int(x, y);

        if (pendingShots.Contains(cell))
        {
            Log(
                $"Shot at ({x}, {y}) is still in flight.",
                true);

            return;
        }

        pendingShots.Add(cell);
        pendingSince[cell] = Time.time;

        FireMessage message =
            new FireMessage(x, y);

        byte[] data =
            MessageSerializer.Serialize(
                MessageType.Fire,
                message);

        transport.Send(data);

        MarkDirty();
    }

    public void Ping()
    {
        if (!transport.IsConnected)
        {
            Log(
                "Can't ping. Disconnected.",
                true);

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

        transport.Send(data);
    }

    public void Reconnect()
    {
        ManuallyDisconnected = false;

        transport.Reconnect();

        Log("Reconnected to the link.");

        SendReconnectRequest();
    }

    public void SendReconnectRequest()
    {
        if (!transport.IsConnected)
        {
            Log(
                "Can't request a snapshot. Disconnected.",
                true);

            return;
        }

        if (PlayerId == 0)
        {
            Log(
                "Can't request a snapshot. " +
                "Player id is unknown.",
                true);

            return;
        }

        ReconnectRequestMessage message =
            new ReconnectRequestMessage(PlayerId);

        byte[] data =
            MessageSerializer.Serialize(
                MessageType.ReconnectRequest,
                message);

        transport.Send(data);
    }

    public void Disconnect()
    {
        ManuallyDisconnected = true;

        transport.Disconnect();
    }

    private void ExpireStaleShots(float deltaTime)
    {
        if (pendingSince.Count == 0)
            return;

        List<Vector2Int> expired = null;

        foreach (KeyValuePair<Vector2Int, float> pair
            in pendingSince)
        {
            if (Time.time - pair.Value < shotResponseTimeout)
                continue;

            expired ??= new List<Vector2Int>();
            expired.Add(pair.Key);
        }

        if (expired == null)
            return;

        foreach (Vector2Int cell in expired)
        {
            pendingSince.Remove(cell);

            if (!pendingShots.Remove(cell))
                continue;

            Log(
                $"No response for the shot at " +
                $"({cell.x}, {cell.y}), clearing.");
        }

        if (expired.Count > 0)
            MarkDirty();
    }
}
