using System;

public interface INetworkTransport
{
    bool IsConnected { get; }

    event Action<byte[]> MessageReceived;
    event Action Disconnected;

    void Send(byte[] data);

    void Disconnect();
}