using System;
using System.Collections;
using UnityEngine;

public class LocalTransport : MonoBehaviour, INetworkTransport
{
    [Header("Network Simulation")]
    [Min(0f)]
    public float LatencyMs = 100f;

    [Range(0f, 1f)]
    public float PacketLoss = 0f;

    private LocalTransport remote;
    private bool connected;

    public bool IsConnected => connected;

        public event Action<byte[]> MessageReceived;
        public event Action Disconnected;
        public event Action Reconnected;

    public void Initialize()
    {
        connected = true;
    }

    public void SetRemote(LocalTransport other)
    {
        remote = other;
    }

        public void Reconnect()
        {
            SetConnected(true);

            Reconnected?.Invoke();

            if (remote != null)
            {
                remote.SetConnected(true);
                remote.Reconnected?.Invoke();
            }

            Debug.Log("[LocalTransport] Link restored.");
        }

    public void Send(byte[] data)
    {
        if (!connected)
            return;

        if (remote == null || !remote.connected)
            return;

        if (data == null || data.Length == 0)
            return;

        if (UnityEngine.Random.value < PacketLoss)
        {
            Debug.Log(
                $"[LocalTransport] Packet lost. Size: {data.Length}");

            return;
        }

        StartCoroutine(Deliver(data));
    }

    private IEnumerator Deliver(byte[] data)
    {
        if (LatencyMs > 0f)
        {
            yield return new WaitForSeconds(
                LatencyMs / 1000f);
        }

        if (!connected)
            yield break;

        if (remote == null || !remote.connected)
            yield break;

        remote.Receive(data);
    }

    private void Receive(byte[] data)
    {
        if (!connected)
            return;

        MessageReceived?.Invoke(data);
    }

    public void Disconnect()
    {
        if (!connected)
            return;

        connected = false;

        StopAllCoroutines();

        Disconnected?.Invoke();

        if (remote != null)
        {
            remote.RemoteDisconnected();
        }
    }

    private void RemoteDisconnected()
    {
        if (!connected)
            return;

        connected = false;

        StopAllCoroutines();

        Disconnected?.Invoke();
    }

    private void SetConnected(bool value)
    {
        connected = value;
    }
}
