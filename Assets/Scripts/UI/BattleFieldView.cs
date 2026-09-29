using UnityEngine;
using UnityEngine.UI;

public class BattleFieldView : MonoBehaviour
{
    [Header("Layout")]
    [Tooltip("Максимальный размер клетки. Реальный " +
             "размер считается от свободного места.")]
    [Min(4f)]
    public float maxCellSize = 28f;

    [Min(4f)]
    public float minCellSize = 8f;

    [Min(0f)]
    public float cellSpacing = 2f;

    [Header("Colors")]
    public Color emptyColor = new Color(0.16f, 0.28f, 0.45f, 1f);
    public Color missColor = new Color(0.35f, 0.42f, 0.55f, 1f);
    public Color shipColor = new Color(0.20f, 0.60f, 0.35f, 1f);
    public Color hitColor = new Color(0.85f, 0.25f, 0.20f, 1f);
    public Color sunkColor = new Color(0.45f, 0.10f, 0.10f, 1f);
    public Color pendingColor = new Color(0.85f, 0.75f, 0.25f, 1f);
    public Color unknownColor = new Color(0.1f, 0.1f, 0.1f, 1f);

    private static Font cachedFont;

    private BattleClient client;
    private bool ownField;

    private int width;
    private int height;

    private Button[] cells;
    private Image[] images;
    private CellState[] cachedStates;
    private GridLayoutGroup gridLayout;

    private bool? lastInteractable;

    private Vector2 lastAvailableSize;

    private void LateUpdate()
    {
        if (gridLayout == null ||
            width <= 0 ||
            height <= 0)
        {
            return;
        }

        Rect available =
            GetAvailableRect((RectTransform)transform);

        if (Mathf.Approximately(
                available.width,
                lastAvailableSize.x) &&
            Mathf.Approximately(
                available.height,
                lastAvailableSize.y))
        {
            return;
        }

        lastAvailableSize = available.size;

        RecalculateCellSize();
    }

    public void Bind(
        BattleClient client,
        bool ownField)
    {
        this.client = client;
        this.ownField = ownField;
    }

    public void Refresh()
    {
        if (client == null)
            return;

        if (!client.IsBoardReady)
            return;

        if (cells == null ||
            client.BoardWidth != width ||
            client.BoardHeight != height)
        {
            Build(
                client.BoardWidth,
                client.BoardHeight);
        }

        if (cells == null)
            return;

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                CellState state =
                    ownField
                        ? client.GetOwnFieldState(x, y)
                        : client.GetEnemyFieldState(x, y);

                Paint(x, y, state);
            }
        }

        UpdateInteractable();
    }

    public void Clear()
    {
        if (images == null)
            return;

        for (int i = 0; i < images.Length; i++)
        {
            if (images[i] == null)
                continue;

            images[i].color = unknownColor;
            cachedStates[i] = CellState.Unknown;
        }
    }

    private void Build(
        int newWidth,
        int newHeight)
    {
        if (newWidth <= 0 || newHeight <= 0)
            return;

        width = newWidth;
        height = newHeight;

        gridLayout =
            GetComponent<GridLayoutGroup>();

        if (gridLayout == null)
        {
            gridLayout =
                gameObject.AddComponent<GridLayoutGroup>();
        }

        gridLayout.spacing =
            new Vector2(cellSpacing, cellSpacing);

        gridLayout.constraint =
            GridLayoutGroup.Constraint.FixedColumnCount;

        gridLayout.constraintCount = width;

        gridLayout.childAlignment = TextAnchor.MiddleCenter;

        RecalculateCellSize();

        ClearChildren();

        int count = width * height;

        cells = new Button[count];
        images = new Image[count];
        cachedStates = new CellState[count];

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int index = y * width + x;

                GameObject cellObject =
                    new GameObject(
                        $"Cell_{x}_{y}",
                        typeof(RectTransform));

                cellObject.transform.SetParent(
                    transform,
                    false);

                Image image =
                    cellObject.AddComponent<Image>();

                image.color = emptyColor;
                image.raycastTarget = true;

                Button button =
                    cellObject.AddComponent<Button>();

                button.transition =
                    Selectable.Transition.None;

                button.targetGraphic = image;

                int capturedX = x;
                int capturedY = y;

                button.onClick.AddListener(
                    () => OnCellClicked(
                        capturedX,
                        capturedY));

                cells[index] = button;
                images[index] = image;
                cachedStates[index] = CellState.Unknown;
            }
        }

        LayoutRebuilder.ForceRebuildLayoutImmediate(
            (RectTransform)transform);

        lastAvailableSize = GetAvailableRect(
            (RectTransform)transform).size;

        RecalculateCellSize();
    }

    private void RecalculateCellSize()
    {
        if (gridLayout == null ||
            width <= 0 ||
            height <= 0)
        {
            return;
        }

        RectTransform rect =
            (RectTransform)transform;

        Rect available = GetAvailableRect(rect);

        if (available.width <= 1f ||
            available.height <= 1f)
        {
            return;
        }

        float totalSpacingX =
            cellSpacing * (width - 1);

        float totalSpacingY =
            cellSpacing * (height - 1);

        float byWidth =
            (available.width - totalSpacingX) / width;

        float byHeight =
            (available.height - totalSpacingY) / height;

        float size =
            Mathf.Min(byWidth, byHeight);

        size = Mathf.Clamp(
            size,
            minCellSize,
            maxCellSize);

        Vector2 target =
            new Vector2(size, size);

        if (gridLayout.cellSize != target)
        {
            gridLayout.cellSize = target;

            LayoutRebuilder.ForceRebuildLayoutImmediate(rect);
        }
    }

    private Rect GetAvailableRect(RectTransform field)
    {
        RectTransform holder =
            field.parent as RectTransform;

        if (holder == null)
            return field.rect;

        Rect rect = holder.rect;

        // Subtract the label sitting above the field.
        float labelHeight = 0f;

        for (int i = 0; i < holder.childCount; i++)
        {
            RectTransform child =
                holder.GetChild(i) as RectTransform;

            if (child != null && child != field)
                labelHeight += child.rect.height;
        }

        float spacing = 0f;

        VerticalLayoutGroup layout =
            holder.GetComponent<VerticalLayoutGroup>();

        if (layout != null)
            spacing = layout.spacing;

        rect.height -= labelHeight + spacing;

        if (rect.width <= 0f)
            rect.width = field.rect.width;

        if (rect.height <= 0f)
            rect.height = field.rect.height;

        return rect;
    }

    private void Paint(
        int x,
        int y,
        CellState state)
    {
        int index = y * width + x;

        if (index < 0 || index >= cachedStates.Length)
            return;

        if (cachedStates[index] == state &&
            images[index] != null)
        {
            return;
        }

        cachedStates[index] = state;

        if (images[index] != null)
        {
            images[index].color = GetColor(state);
        }
    }

    private void UpdateInteractable()
    {
        bool interactable =
            !ownField &&
            client != null &&
            client.IsMyTurn &&
            !client.GameOver &&
            !client.IsPaused;

        if (lastInteractable == interactable)
            return;

        lastInteractable = interactable;

        if (cells == null)
            return;

        for (int i = 0; i < cells.Length; i++)
        {
            if (cells[i] == null)
                continue;

            cells[i].interactable = interactable;
        }
    }

    private Color GetColor(CellState state)
    {
        switch (state)
        {
            case CellState.Empty:
                return emptyColor;

            case CellState.Miss:
                return missColor;

            case CellState.Ship:
                return shipColor;

            case CellState.Hit:
                return hitColor;

            case CellState.Sunk:
                return sunkColor;

            case CellState.Pending:
                return pendingColor;

            default:
                return unknownColor;
        }
    }

    private void OnCellClicked(int x, int y)
    {
        if (client == null)
            return;

        if (ownField)
            return;

        if (!client.IsMyTurn || client.GameOver || client.IsPaused)
            return;

        client.Fire(x, y);
    }

    private void ClearChildren()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            GameObject child =
                transform.GetChild(i).gameObject;

            if (Application.isPlaying)
            {
                Destroy(child);
            }
            else
            {
                DestroyImmediate(child);
            }
        }
    }

    public static Font GetDefaultFont()
    {
        if (cachedFont == null)
        {
            cachedFont =
                Resources.GetBuiltinResource<Font>(
                    "LegacyRuntime.ttf");
        }

        return cachedFont;
    }
}
