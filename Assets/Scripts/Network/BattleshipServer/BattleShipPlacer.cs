using System;
using System.Collections.Generic;
using UnityEngine;

public static class BattleShipPlacer
{
    private const int MaxPlacementAttempts = 5000;

    public static void PlaceShips(
        BattleBoard board,
        BattleshipConfig config)
    {
        if (board == null)
            throw new ArgumentNullException(nameof(board));

        if (config == null)
            throw new ArgumentNullException(nameof(config));

        ValidateConfig(config);

        for (int i = 0; i < config.shipCount; i++)
        {
            int shipSize =
                config.shipSizes[i];

            BattleShip ship =
                CreateShip(
                    board,
                    shipSize,
                    i);

            board.AddShip(ship);
        }

        Debug.Log(
            $"[BattleShipPlacer] " +
            $"Placed {config.shipCount} ships.");
    }

    private static BattleShip CreateShip(
        BattleBoard board,
        int size,
        int shipId)
    {
        for (int attempt = 0;
             attempt < MaxPlacementAttempts;
             attempt++)
        {
            bool horizontal =
                UnityEngine.Random.value > 0.5f;

            int startX =
                UnityEngine.Random.Range(
                    0,
                    board.Width);

            int startY =
                UnityEngine.Random.Range(
                    0,
                    board.Height);

            List<Vector2Int> cells =
                CreateCells(
                    board,
                    startX,
                    startY,
                    size,
                    horizontal);

            if (cells == null)
                continue;

            if (!CanPlaceShip(
                board,
                cells))
            {
                continue;
            }

            BattleShip ship =
                new BattleShip(shipId);

            foreach (Vector2Int cell in cells)
            {
                ship.Cells.Add(cell);
            }

            return ship;
        }

        throw new InvalidOperationException(
            $"Unable to place ship " +
            $"with size {size}. " +
            $"The board may be too small for " +
            $"the configured ships.");
    }

    private static List<Vector2Int> CreateCells(
        BattleBoard board,
        int startX,
        int startY,
        int size,
        bool horizontal)
    {
        List<Vector2Int> cells =
            new List<Vector2Int>(size);

        for (int i = 0; i < size; i++)
        {
            int x =
                horizontal
                    ? startX + i
                    : startX;

            int y =
                horizontal
                    ? startY
                    : startY + i;

            if (!board.IsInside(x, y))
                return null;

            cells.Add(
                new Vector2Int(x, y));
        }

        return cells;
    }

    /// <summary>
    /// Проверяет:
    ///
    /// 1. Все клетки корабля находятся внутри поля.
    /// 2. Клетки корабля свободны.
    /// 3. Ни одна клетка корабля не касается
    ///    другого корабля даже по диагонали.
    ///
    /// Таким образом между кораблями всегда
    /// минимум одна свободная клетка.
    /// </summary>
    private static bool CanPlaceShip(
        BattleBoard board,
        List<Vector2Int> cells)
    {
        foreach (Vector2Int cell in cells)
        {
            if (!board.IsInside(
                cell.x,
                cell.y))
            {
                return false;
            }

            if (board.HasShip(
                cell.x,
                cell.y))
            {
                return false;
            }

            for (int dx = -1; dx <= 1; dx++)
            {
                for (int dy = -1; dy <= 1; dy++)
                {
                    if (dx == 0 && dy == 0)
                        continue;

                    int neighbourX =
                        cell.x + dx;

                    int neighbourY =
                        cell.y + dy;

                    if (!board.IsInside(
                        neighbourX,
                        neighbourY))
                    {
                        continue;
                    }

                    if (board.HasShip(
                        neighbourX,
                        neighbourY))
                    {
                        return false;
                    }
                }
            }
        }

        return true;
    }

    private static void ValidateConfig(
        BattleshipConfig config)
    {
        if (config.boardWidth <= 0)
        {
            throw new InvalidOperationException(
                "Board width must be greater than zero.");
        }

        if (config.boardHeight <= 0)
        {
            throw new InvalidOperationException(
                "Board height must be greater than zero.");
        }

        if (config.shipCount <= 0)
        {
            throw new InvalidOperationException(
                "Ship count must be greater than zero.");
        }

        if (config.shipSizes == null)
        {
            throw new InvalidOperationException(
                "Ship sizes are not configured.");
        }

        if (config.shipSizes.Length <
            config.shipCount)
        {
            throw new InvalidOperationException(
                "shipSizes must contain at least " +
                "shipCount elements.");
        }

        int maxShipSize =
            Mathf.Max(
                config.boardWidth,
                config.boardHeight);

        for (int i = 0;
             i < config.shipCount;
             i++)
        {
            int size =
                config.shipSizes[i];

            if (size <= 0)
            {
                throw new InvalidOperationException(
                    $"Ship size at index {i} " +
                    $"must be greater than zero.");
            }

            if (size > maxShipSize)
            {
                throw new InvalidOperationException(
                    $"Ship size {size} is too large " +
                    $"for board " +
                    $"{config.boardWidth}x" +
                    $"{config.boardHeight}.");
            }
        }
    }
}