public class BattlePlayer
{
    public int Id { get; }

    public INetworkTransport Transport { get; }

    public BattleBoard Board { get; }

    public BattlePlayer(
        int id,
        INetworkTransport transport,
        int boardWidth,
        int boardHeight)
    {
        Id = id;
        Transport = transport;

        Board =
            new BattleBoard(
                boardWidth,
                boardHeight);
    }
}