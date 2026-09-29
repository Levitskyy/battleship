using UnityEngine;

/// <summary>
/// Отображение состояния в клетки: что показывать на своём
/// поле и на поле противника.
/// </summary>
public partial class BattleClient
{
    public CellState GetOwnFieldState(
        int x,
        int y)
    {
        if (!IsInsideBoard(x, y))
            return CellState.Unknown;

        if (myShipGrid == null)
            return CellState.Empty;

        int shipIndex = myShipGrid[x, y];

        if (!opponentShots[x, y])
        {
            return shipIndex >= 0
                ? CellState.Ship
                : CellState.Empty;
        }

        if (shipIndex < 0)
            return CellState.Miss;

        if (!myShipHit[x, y])
            return CellState.Ship;

        if (IsShipSunk(shipIndex))
            return CellState.Sunk;

        return CellState.Hit;
    }

    public CellState GetEnemyFieldState(
        int x,
        int y)
    {
        if (!IsInsideBoard(x, y))
            return CellState.Unknown;

        if (pendingShots.Contains(new Vector2Int(x, y)))
            return CellState.Pending;

        if (myShots == null || !myShots[x, y])
            return CellState.Empty;

        if (myShotSunk != null && myShotSunk[x, y])
            return CellState.Sunk;

        if (myShotHit != null && myShotHit[x, y])
            return CellState.Hit;

        return CellState.Miss;
    }

    private bool MarkShipHit(int x, int y)
    {
        if (myShipGrid == null)
            return false;

        int index = myShipGrid[x, y];

        if (index < 0 || index >= myShips.Count)
            return false;

        if (myShipHit[x, y])
            return true;

        myShipHit[x, y] = true;

        ShipData ship = myShips[index];
        ship.HitCount++;

        if (index < shipSunk.Length)
        {
            shipSunk[index] = ship.IsSunk;
        }

        return true;
    }

    private bool IsShipSunk(int index)
    {
        if (shipSunk == null)
            return false;

        if (index < 0 || index >= shipSunk.Length)
            return false;

        return shipSunk[index];
    }
}
