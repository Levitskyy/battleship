using UnityEngine;

[CreateAssetMenu(
    fileName = "BattleshipConfig",
    menuName = "Battleship/Game Config")]
public class BattleshipConfig : ScriptableObject
{
    [Header("Board")]
    [Min(1)]
    public int boardWidth = 10;

    [Min(1)]
    public int boardHeight = 10;

    [Header("Ships")]
    [Min(1)]
    public int shipCount = 5;

    [Tooltip("Длины кораблей.")]
    public int[] shipSizes = { 4, 3, 3, 2, 2 };
}