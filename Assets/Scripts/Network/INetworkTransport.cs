using System;

public interface INetworkTransport
{
    bool IsConnected { get; }

    event Action<byte[]> MessageReceived;
    event Action Disconnected;
    event Action Reconnected;

    void Send(byte[] data);

    void Reconnect();

    void Disconnect();
}
