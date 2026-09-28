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

    public IReadOnlyList<BattleShip> Ships => ships;

    public int Width => width;
    public int Height => height;

    public bool HasShip(int x, int y)
    {
        return shipIds[x, y] != -1;
    }

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

    public bool WasShot(int x, int y)
    {
        return shots[x, y];
    }

    public void AddShip(BattleShip ship)
    {
        foreach (Vector2Int cell in ship.Cells)
        {
            shipIds[cell.x, cell.y] = ship.Id;
        }

        ships.Add(ship);
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

        BattleShip ship =
            ships[shipId];

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
}