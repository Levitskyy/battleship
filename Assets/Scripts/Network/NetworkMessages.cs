using System;

public enum MessageType : byte
{
    Fire = 0,
    FireResult = 1,
    Ping = 2,
    Pong = 3,
    GameStarted = 4,
    TurnChanged = 5,
    GameOver = 6,
    OpponentFire = 7
}

[Serializable]
public struct FireMessage
{
    public int X;
    public int Y;

    public FireMessage(int x, int y)
    {
        X = x;
        Y = y;
    }
}

[Serializable]
public struct FireResultMessage
{
    public int X;
    public int Y;

    public bool Hit;
    public bool AlreadyShot;
    public bool Sunk;

    public FireResultMessage(
        int x,
        int y,
        bool hit,
        bool alreadyShot,
        bool sunk)
    {
        X = x;
        Y = y;
        Hit = hit;
        AlreadyShot = alreadyShot;
        Sunk = sunk;
    }
}

/// <summary>
/// Сообщение владельцу поля
/// </summary>
[Serializable]
public struct OpponentFireMessage
{
    public int X;
    public int Y;

    public bool Hit;
    public bool Sunk;

    public OpponentFireMessage(
        int x,
        int y,
        bool hit,
        bool sunk)
    {
        X = x;
        Y = y;
        Hit = hit;
        Sunk = sunk;
    }
}

[Serializable]
public struct PingMessage
{
    public int Id;

    public PingMessage(int id)
    {
        Id = id;
    }
}

[Serializable]
public struct PongMessage
{
    public int Id;

    public PongMessage(int id)
    {
        Id = id;
    }
}

[Serializable]
public struct GameStartedMessage
{
    public int BoardWidth;
    public int BoardHeight;

    public GameStartedMessage(
        int boardWidth,
        int boardHeight)
    {
        BoardWidth = boardWidth;
        BoardHeight = boardHeight;
    }
}

[Serializable]
public struct TurnChangedMessage
{
    public int PlayerId;

    public TurnChangedMessage(int playerId)
    {
        PlayerId = playerId;
    }
}

[Serializable]
public struct GameOverMessage
{
    public int WinnerPlayerId;

    public GameOverMessage(int winnerPlayerId)
    {
        WinnerPlayerId = winnerPlayerId;
    }
}