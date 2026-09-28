using System.Collections.Generic;
using UnityEngine;

public class BattleShip
{
    public int Id { get; }

    public List<Vector2Int> Cells { get; } =
        new List<Vector2Int>();

    public HashSet<Vector2Int> HitCells { get; } =
        new HashSet<Vector2Int>();

    public bool IsSunk =>
        HitCells.Count >= Cells.Count;

    public int RemainingCells =>
        Cells.Count - HitCells.Count;

    public BattleShip(int id)
    {
        Id = id;
    }
}