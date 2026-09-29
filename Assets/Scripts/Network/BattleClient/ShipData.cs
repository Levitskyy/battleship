public class ShipData
{
    public int Size;
    public int StartX;
    public int StartY;
    public bool Horizontal;
    public int HitCount;

    public bool IsSunk =>
        HitCount >= Size;

    public void GetCell(
        int index,
        out int x,
        out int y)
    {
        x = Horizontal ? StartX + index : StartX;
        y = Horizontal ? StartY : StartY + index;
    }
}
