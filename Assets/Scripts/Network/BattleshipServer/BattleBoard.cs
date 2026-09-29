using System;
using System.Collections.Generic;
using UnityEngine;

public class BattleBoard
{
    private readonly int width;
    private readonly int height;

    private readonly int[,] shipIds;
    private readonly bool[,] shots;

    private readonly List<BattleShip> ships =
        new List<BattleShip>();

    private readonly Dictionary<int, BattleShip> shipsById =
        new Dictionary<int, BattleShip>();

    public IReadOnlyList<BattleShip> Ships => ships;

    public int Width => width;
    public int Height => height;

    public BattleBoard(int width, int height)
    {
        this.width = width;
        this.height = height;

        shipIds =
            new int[width, height];

        shots =
            new bool[width, height];

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                shipIds[x, y] = -1;
            }
        }
    }

    public bool IsInside(int x, int y)
    {
        return
            x >= 0 &&
            x < width &&
            y >= 0 &&
            y < height;
    }

    public bool HasShip(int x, int y)
    {
        if (!IsInside(x, y))
            return false;

        return shipIds[x, y] != -1;
    }

    public bool WasShot(int x, int y)
    {
        if (!IsInside(x, y))
            return false;

        return shots[x, y];
    }

    public BattleShip GetShipAt(int x, int y)
    {
        if (!IsInside(x, y))
            return null;

        int id = shipIds[x, y];

        if (id == -1)
            return null;

        if (!shipsById.TryGetValue(id, out BattleShip ship))
            return null;

        return ship;
    }

    public bool IsSunkAt(int x, int y)
    {
        BattleShip ship = GetShipAt(x, y);

        return ship != null && ship.IsSunk;
    }

    /// <summary>
    /// Результат выстрела с точки зрения стрелка.
    ///
    /// Сервер вправе раскрывать это только для
    /// клеток, куда игрок уже стрелял, иначе
    /// мы бы выдали ему позиции кораблей.
    /// </summary>
    public void GetShotOutcome(
        int x,
        int y,
        out bool hit,
        out bool sunk)
    {
        hit = false;
        sunk = false;

        if (!IsInside(x, y))
            return;

        if (!shots[x, y])
            return;

        BattleShip ship = GetShipAt(x, y);

        if (ship == null)
            return;

        hit = true;
        sunk = ship.IsSunk;
    }

    public void AddShip(BattleShip ship)
    {
        if (ship == null)
            throw new ArgumentNullException(nameof(ship));

        if (shipsById.ContainsKey(ship.Id))
        {
            throw new InvalidOperationException(
                $"Duplicate ship id: {ship.Id}");
        }

        foreach (Vector2Int cell in ship.Cells)
        {
            if (!IsInside(cell.x, cell.y))
            {
                throw new InvalidOperationException(
                    $"Ship {ship.Id} has a cell " +
                    $"outside the board: ({cell.x}, {cell.y})");
            }

            if (shipIds[cell.x, cell.y] != -1)
            {
                throw new InvalidOperationException(
                    $"Ship {ship.Id} overlaps an existing " +
                    $"ship at ({cell.x}, {cell.y})");
            }
        }

        foreach (Vector2Int cell in ship.Cells)
        {
            shipIds[cell.x, cell.y] = ship.Id;
        }

        ships.Add(ship);
        shipsById.Add(ship.Id, ship);
    }

    public FireResultMessage ReceiveShot(
        int x,
        int y)
    {
        if (!IsInside(x, y))
        {
            return new FireResultMessage(
                x,
                y,
                false,
                false,
                false);
        }

        if (shots[x, y])
        {
            return new FireResultMessage(
                x,
                y,
                false,
                true,
                false);
        }

        shots[x, y] = true;

        int shipId =
            shipIds[x, y];

        if (shipId == -1)
        {
            return new FireResultMessage(
                x,
                y,
                false,
                false,
                false);
        }

        if (!shipsById.TryGetValue(
                shipId,
                out BattleShip ship))
        {
            Debug.LogError(
                $"[BattleBoard] Ship {shipId} " +
                $"not found at ({x}, {y}).");

            return new FireResultMessage(
                x,
                y,
                false,
                false,
                false);
        }

        Vector2Int cell =
            new Vector2Int(x, y);

        ship.HitCells.Add(cell);

        return new FireResultMessage(
            x,
            y,
            true,
            false,
            ship.IsSunk);
    }

    public int RemainingShipCells
    {
        get
        {
            int result = 0;

            foreach (BattleShip ship in ships)
            {
                result += ship.RemainingCells;
            }

            return result;
        }
    }

    public int SunkShipCount
    {
        get
        {
            int result = 0;

            foreach (BattleShip ship in ships)
            {
                if (ship.IsSunk)
                    result++;
            }

            return result;
        }
    }

    public void GetShipPlacementData(
        out int shipCount,
        out int[] shipSizes,
        out int[] shipStartX,
        out int[] shipStartY,
        out bool[] shipHorizontal,
        out bool[] shipHitCells)
    {
        shipCount = ships.Count;
        shipSizes = new int[shipCount];
        shipStartX = new int[shipCount];
        shipStartY = new int[shipCount];
        shipHorizontal = new bool[shipCount];

        int totalCells = 0;

        foreach (BattleShip ship in ships)
        {
            totalCells += ship.Cells.Count;
        }

        shipHitCells = new bool[totalCells];

        int cellIndex = 0;

        for (int i = 0; i < shipCount; i++)
        {
            BattleShip ship = ships[i];

            shipSizes[i] = ship.Cells.Count;

            Vector2Int firstCell = ship.Cells[0];
            shipStartX[i] = firstCell.x;
            shipStartY[i] = firstCell.y;

            if (ship.Cells.Count > 1)
            {
                Vector2Int secondCell = ship.Cells[1];
                shipHorizontal[i] = secondCell.x != firstCell.x;
            }
            else
            {
                shipHorizontal[i] = true;
            }

            foreach (Vector2Int cell in ship.Cells)
            {
                shipHitCells[cellIndex] =
                    ship.HitCells.Contains(cell);

                cellIndex++;
            }
        }
    }
}
