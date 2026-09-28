using System;
using System.Collections;
using UnityEngine;

public class LocalTransport : MonoBehaviour, INetworkTransport
{
    [Header("Network Simulation")]
    [Min(0f)]
    public float LatencyMs = 100f;

    [Range(0f, 1f)]
    public float PacketLoss = 0.1f;

    private LocalTransport remote;
    private bool connected;

    public bool IsConnected => connected;

    public event Action<byte[]> MessageReceived;
    public event Action Disconnected;

    public void Initialize()
    {
        connected = true;
    }

    public void SetRemote(LocalTransport other)
    {
        remote = other;
    }

    public void Send(byte[] data)
    {
        if (!connected)
            return;

        if (remote == null || !remote.IsConnected)
            return;

        // Имитация потери пакета.
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

        if (remote == null || !remote.IsConnected)
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
}