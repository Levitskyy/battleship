using UnityEngine;
using System.Collections.Generic;

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

    [Header("Network")]
    [Tooltip("Сколько ждать переподключения игрока " +
             "перед тем, как засчитать ему поражение.")]
    [Min(0f)]
    public float reconnectGraceSeconds = 20f;

    /// <summary>
    /// Возвращает копию конфига с гарантированно валидными
    /// значениями. Исходный ассет не меняется.
    /// </summary>
    public BattleshipConfig GetValidated()
    {
        BattleshipConfig result =
            CreateInstance<BattleshipConfig>();

        result.reconnectGraceSeconds =
            Mathf.Max(0f, reconnectGraceSeconds);

        if (boardWidth < 1)
        {
            Debug.LogWarning(
                $"[BattleshipConfig] boardWidth " +
                $"{boardWidth} < 1, using 10.");

            result.boardWidth = 10;
        }
        else
        {
            result.boardWidth = boardWidth;
        }

        if (boardHeight < 1)
        {
            Debug.LogWarning(
                $"[BattleshipConfig] boardHeight " +
                $"{boardHeight} < 1, using 10.");

            result.boardHeight = 10;
        }
        else
        {
            result.boardHeight = boardHeight;
        }

        int[] sizes = SanitizeShips(result);

        result.shipSizes = sizes;
        result.shipCount = sizes.Length;

        return result;
    }

    private int[] SanitizeShips(BattleshipConfig result)
    {
        int[] source = shipSizes;

        if (source == null || source.Length == 0)
        {
            Debug.LogWarning(
                "[BattleshipConfig] shipSizes is empty, " +
                "using 4/3/3/2/2.");

            return new[] { 4, 3, 3, 2, 2 };
        }

        List<int> valid = new List<int>();

        foreach (int size in source)
        {
            if (size < 1)
            {
                Debug.LogWarning(
                    $"[BattleshipConfig] Ship size {size} " +
                    "is invalid, skipping.");

                continue;
            }

            if (size > result.boardWidth &&
                size > result.boardHeight)
            {
                Debug.LogWarning(
                    $"[BattleshipConfig] Ship of size " +
                    $"{size} does not fit on a " +
                    $"{result.boardWidth}x{result.boardHeight} " +
                    "board, skipping.");

                continue;
            }

            valid.Add(size);
        }

        if (valid.Count == 0)
        {
            Debug.LogWarning(
                "[BattleshipConfig] No valid ships left, " +
                "using 4/3/3/2/2.");

            return new[] { 4, 3, 3, 2, 2 };
        }

        return valid.ToArray();
    }
}
