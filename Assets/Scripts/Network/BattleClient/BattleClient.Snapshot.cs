using UnityEngine;

/// <summary>
/// Восстановление состояния из снапшота сервера, вызывается
/// при переподключении.
/// </summary>
public partial class BattleClient
{
    private void ApplySnapshot(
        GameStateSnapshotMessage snapshot)
    {
        PlayerId = snapshot.PlayerId;

        bool sizeChanged =
            boardWidth != snapshot.BoardWidth ||
            boardHeight != snapshot.BoardHeight;

        if (sizeChanged)
        {
            boardWidth = snapshot.BoardWidth;
            boardHeight = snapshot.BoardHeight;

            AllocateBoard();
        }

        currentPlayerId = snapshot.CurrentPlayerId;
        gameOver = snapshot.GameOver;
        IsPaused = snapshot.Paused;
        winnerPlayerId = snapshot.WinnerPlayerId;
        gameStarted = true;

        myShots = snapshot.ToMyShots();
        opponentShots = snapshot.ToOpponentShots();

        if (myShotHit == null)
        {
            myShotHit = new bool[boardWidth, boardHeight];
        }

        if (myShotSunk == null)
        {
            myShotSunk = new bool[boardWidth, boardHeight];
        }

        for (int x = 0; x < boardWidth; x++)
        {
            for (int y = 0; y < boardHeight; y++)
            {
                myShotHit[x, y] = snapshot.GetMyHit(x, y);

                myShotSunk[x, y] = snapshot.GetMySunk(x, y);
            }
        }

        pendingShots.Clear();
        pendingSince.Clear();

        RebuildShips(
            snapshot.ShipCount,
            snapshot.ShipSizes,
            snapshot.ShipStartX,
            snapshot.ShipStartY,
            snapshot.ShipHorizontal,
            snapshot.ShipHitCells);
    }

    private void AllocateBoard()
    {
        myShots = new bool[boardWidth, boardHeight];
        myShotHit = new bool[boardWidth, boardHeight];
        myShotSunk = new bool[boardWidth, boardHeight];
        opponentShots = new bool[boardWidth, boardHeight];
        myShipGrid = new int[boardWidth, boardHeight];
        myShipHit = new bool[boardWidth, boardHeight];

        for (int x = 0; x < boardWidth; x++)
        {
            for (int y = 0; y < boardHeight; y++)
            {
                myShipGrid[x, y] = -1;
            }
        }
    }

    private void RebuildShips(
        int count,
        int[] sizes,
        int[] startX,
        int[] startY,
        bool[] horizontal,
        bool[] hitCells)
    {
        myShips.Clear();

        if (myShipGrid == null)
            return;

        for (int x = 0; x < boardWidth; x++)
        {
            for (int y = 0; y < boardHeight; y++)
            {
                myShipGrid[x, y] = -1;
                myShipHit[x, y] = false;
            }
        }

        if (sizes == null)
            return;

        int total =
            Mathf.Min(count, sizes.Length);

        if (startX != null)
            total = Mathf.Min(total, startX.Length);

        if (startY != null)
            total = Mathf.Min(total, startY.Length);

        if (horizontal != null)
            total = Mathf.Min(total, horizontal.Length);

        shipSunk = new bool[total];

        int cellIndex = 0;

        for (int i = 0; i < total; i++)
        {
            ShipData ship = new ShipData
            {
                Size = sizes[i],
                StartX = startX[i],
                StartY = startY[i],
                Horizontal = horizontal[i]
            };

            myShips.Add(ship);

            for (int j = 0; j < ship.Size; j++)
            {
                ship.GetCell(j, out int cx, out int cy);

                bool hit =
                    hitCells != null &&
                    cellIndex < hitCells.Length &&
                    hitCells[cellIndex];

                if (IsInsideBoard(cx, cy))
                {
                    myShipGrid[cx, cy] = i;

                    if (hit)
                    {
                        myShipHit[cx, cy] = true;
                        ship.HitCount++;
                    }
                }

                cellIndex++;
            }

            shipSunk[i] = ship.IsSunk;
        }
    }
}
