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
    OpponentFire = 7,
    ShipPlacement = 8,
    GameStateSnapshot = 9,
    ReconnectRequest = 10,
    Notice = 11,
    PauseChanged = 12
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

    public bool Rejected;

    public FireResultMessage(
        int x,
        int y,
        bool hit,
        bool alreadyShot,
        bool sunk,
        bool rejected = false)
    {
        X = x;
        Y = y;
        Hit = hit;
        AlreadyShot = alreadyShot;
        Sunk = sunk;
        Rejected = rejected;
    }
}

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
    public int PlayerId;
    public int BoardWidth;
    public int BoardHeight;

    public GameStartedMessage(
        int playerId,
        int boardWidth,
        int boardHeight)
    {
        PlayerId = playerId;
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

[Serializable]
public struct ShipPlacementMessage
{
    public int ShipCount;
    public int[] ShipSizes;
    public int[] ShipStartX;
    public int[] ShipStartY;
    public bool[] ShipHorizontal;
    public bool[] ShipHitCells;

    public ShipPlacementMessage(
        int shipCount,
        int[] shipSizes,
        int[] shipStartX,
        int[] shipStartY,
        bool[] shipHorizontal,
        bool[] shipHitCells)
    {
        ShipCount = shipCount;
        ShipSizes = shipSizes;
        ShipStartX = shipStartX;
        ShipStartY = shipStartY;
        ShipHorizontal = shipHorizontal;
        ShipHitCells = shipHitCells;
    }
}

[Serializable]
public struct NoticeMessage
{
    public string Text;

    public NoticeMessage(string text)
    {
        Text = text;
    }
}

[Serializable]
public struct PauseChangedMessage
{
    public bool Paused;

    public PauseChangedMessage(bool paused)
    {
        Paused = paused;
    }
}

[Serializable]
public struct GameStateSnapshotMessage
{
    public int PlayerId;
    public int BoardWidth;
    public int BoardHeight;
    public int CurrentPlayerId;
    public bool GameOver;
    public bool Paused;
    public int WinnerPlayerId;

    public int ShipCount;
    public int[] ShipSizes;
    public int[] ShipStartX;
    public int[] ShipStartY;
    public bool[] ShipHorizontal;
    public bool[] ShipHitCells;

    public bool[] MyShotsFlat;
    public bool[] OpponentShotsFlat;
    public bool[] MyShotHitFlat;
    public bool[] MyShotSunkFlat;

    public GameStateSnapshotMessage(
        int playerId,
        int boardWidth,
        int boardHeight,
        int currentPlayerId,
        bool gameOver,
        bool paused,
        int winnerPlayerId,
        int shipCount,
        int[] shipSizes,
        int[] shipStartX,
        int[] shipStartY,
        bool[] shipHorizontal,
        bool[] shipHitCells,
        bool[,] myShots,
        bool[,] opponentShots,
        bool[,] myShotHit,
        bool[,] myShotSunk)
    {
        PlayerId = playerId;
        BoardWidth = boardWidth;
        BoardHeight = boardHeight;
        CurrentPlayerId = currentPlayerId;
        GameOver = gameOver;
        Paused = paused;
        WinnerPlayerId = winnerPlayerId;
        ShipCount = shipCount;
        ShipSizes = shipSizes;
        ShipStartX = shipStartX;
        ShipStartY = shipStartY;
        ShipHorizontal = shipHorizontal;
        ShipHitCells = shipHitCells;

        int cellCount = boardWidth * boardHeight;

        MyShotsFlat = new bool[cellCount];
        OpponentShotsFlat = new bool[cellCount];
        MyShotHitFlat = new bool[cellCount];
        MyShotSunkFlat = new bool[cellCount];

        for (int x = 0; x < boardWidth; x++)
        {
            for (int y = 0; y < boardHeight; y++)
            {
                int index = FlatIndex(x, y, boardHeight);

                MyShotsFlat[index] = myShots[x, y];
                OpponentShotsFlat[index] = opponentShots[x, y];
                MyShotHitFlat[index] = myShotHit[x, y];
                MyShotSunkFlat[index] = myShotSunk[x, y];
            }
        }
    }

    public static int FlatIndex(
        int x,
        int y,
        int height)
    {
        return x * height + y;
    }

    public bool GetMyShot(int x, int y)
    {
        if (MyShotsFlat == null)
            return false;

        int index = FlatIndex(x, y, BoardHeight);

        if (index < 0 || index >= MyShotsFlat.Length)
            return false;

        return MyShotsFlat[index];
    }

    public bool GetOpponentShot(int x, int y)
    {
        if (OpponentShotsFlat == null)
            return false;

        int index = FlatIndex(x, y, BoardHeight);

        if (index < 0 || index >= OpponentShotsFlat.Length)
            return false;

        return OpponentShotsFlat[index];
    }

    public bool GetMyHit(int x, int y)
    {
        if (MyShotHitFlat == null)
            return false;

        int index = FlatIndex(x, y, BoardHeight);

        if (index < 0 || index >= MyShotHitFlat.Length)
            return false;

        return MyShotHitFlat[index];
    }

    public bool GetMySunk(int x, int y)
    {
        if (MyShotSunkFlat == null)
            return false;

        int index = FlatIndex(x, y, BoardHeight);

        if (index < 0 || index >= MyShotSunkFlat.Length)
            return false;

        return MyShotSunkFlat[index];
    }

    public bool[,] ToMyShots()
    {
        return ToGrid(MyShotsFlat);
    }

    public bool[,] ToOpponentShots()
    {
        return ToGrid(OpponentShotsFlat);
    }

    private bool[,] ToGrid(bool[] flat)
    {
        bool[,] result =
            new bool[BoardWidth, BoardHeight];

        if (flat == null)
            return result;

        for (int x = 0; x < BoardWidth; x++)
        {
            for (int y = 0; y < BoardHeight; y++)
            {
                result[x, y] =
                    Get(
                        flat,
                        x,
                        y);
            }
        }

        return result;
    }

    private bool Get(
        bool[] flat,
        int x,
        int y)
    {
        int index = FlatIndex(x, y, BoardHeight);

        if (index < 0 || index >= flat.Length)
            return false;

        return flat[index];
    }
}

[Serializable]
public struct ReconnectRequestMessage
{
    public int PlayerId;

    public ReconnectRequestMessage(int playerId)
    {
        PlayerId = playerId;
    }
}
