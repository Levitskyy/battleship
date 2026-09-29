using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum TransportPacketType : byte
{
    Data = 1,
    Ack = 2
}

public class ReliableOrderedTransport : INetworkTransport
{
    private readonly MonoBehaviour coroutineHost;
    private readonly INetworkTransport innerTransport;

    private readonly float retryTimeout;
    private readonly int maxRetries;

    public bool VerboseLogging;

    private int nextSendSequence = 1;
    private int expectedReceiveSequence = 1;

    private readonly Dictionary<int, PendingPacket> pendingPackets =
        new Dictionary<int, PendingPacket>();

    private readonly Dictionary<int, byte[]> receiveBuffer =
        new Dictionary<int, byte[]>();

    private bool disposed;
    private Coroutine retryRoutine;

    public bool IsConnected =>
        !disposed && innerTransport.IsConnected;

    public event Action<byte[]> MessageReceived;
    public event Action Disconnected;
    public event Action Reconnected;

    private class PendingPacket
    {
        public byte[] Data;
        public float LastSendTime;
        public int RetryCount;
    }

    public ReliableOrderedTransport(
        MonoBehaviour coroutineHost,
        INetworkTransport innerTransport,
        float retryTimeout = 0.5f,
        int maxRetries = 10)
    {
        this.coroutineHost = coroutineHost;
        this.innerTransport = innerTransport;
        this.retryTimeout = retryTimeout;
        this.maxRetries = maxRetries;

        innerTransport.MessageReceived += OnRawMessageReceived;
        innerTransport.Disconnected += OnInnerDisconnected;
        innerTransport.Reconnected += OnInnerReconnected;

        StartRetryLoop();
    }

    public void Send(byte[] data)
    {
        if (!IsConnected)
        {
            Debug.LogWarning(
                "[ReliableTransport] Can't send. Disconnected.");

            return;
        }

        int sequence = nextSendSequence++;

        byte[] packet =
            CreateDataPacket(
                sequence,
                data);

        pendingPackets[sequence] =
            new PendingPacket
            {
                Data = packet,
                LastSendTime = Time.time,
                RetryCount = 0
            };

        if (VerboseLogging)
        {
            Debug.Log(
                $"[ReliableTransport] Send DATA #{sequence}");
        }

        innerTransport.Send(packet);
    }

    public void Reconnect()
    {
        innerTransport.Reconnect();
    }

    private void OnInnerReconnected()
    {
        disposed = false;
        nextSendSequence = 1;
        expectedReceiveSequence = 1;

        pendingPackets.Clear();
        receiveBuffer.Clear();

        StartRetryLoop();

        Reconnected?.Invoke();

        Debug.Log(
            "[ReliableTransport] Sequence state reset.");
    }

    private void StartRetryLoop()
    {
        if (coroutineHost == null)
            return;

        if (retryRoutine != null)
        {
            coroutineHost.StopCoroutine(retryRoutine);
            retryRoutine = null;
        }

        retryRoutine =
            coroutineHost.StartCoroutine(RetryLoop());
    }

    private byte[] CreateDataPacket(
        int sequence,
        byte[] payload)
    {
        byte[] result =
            new byte[1 + 4 + payload.Length];

        result[0] =
            (byte)TransportPacketType.Data;

        Buffer.BlockCopy(
            BitConverter.GetBytes(sequence),
            0,
            result,
            1,
            4);

        Buffer.BlockCopy(
            payload,
            0,
            result,
            5,
            payload.Length);

        return result;
    }

    private byte[] CreateAckPacket(int sequence)
    {
        byte[] result =
            new byte[1 + 4];

        result[0] =
            (byte)TransportPacketType.Ack;

        Buffer.BlockCopy(
            BitConverter.GetBytes(sequence),
            0,
            result,
            1,
            4);

        return result;
    }

    private void OnRawMessageReceived(byte[] data)
    {
        if (data == null || data.Length < 5)
        {
            Debug.LogWarning(
                "[ReliableTransport] Invalid packet.");

            return;
        }

        TransportPacketType packetType =
            (TransportPacketType)data[0];

        int sequence =
            BitConverter.ToInt32(data, 1);

        switch (packetType)
        {
            case TransportPacketType.Data:
                HandleDataPacket(data, sequence);
                break;

            case TransportPacketType.Ack:
                HandleAckPacket(sequence);
                break;

            default:
                Debug.LogWarning(
                    $"[ReliableTransport] Unknown packet type: {packetType}");

                break;
        }
    }

    private void HandleDataPacket(
        byte[] data,
        int sequence)
    {
        if (VerboseLogging)
        {
            Debug.Log(
                $"[ReliableTransport] Receive DATA #{sequence}");
        }

        byte[] ack =
            CreateAckPacket(sequence);

        innerTransport.Send(ack);

        if (sequence < expectedReceiveSequence)
        {
            if (VerboseLogging)
            {
                Debug.Log(
                    $"[ReliableTransport] Duplicate DATA #{sequence}");
            }

            return;
        }

        byte[] payload =
            new byte[data.Length - 5];

        Buffer.BlockCopy(
            data,
            5,
            payload,
            0,
            payload.Length);

        if (!receiveBuffer.ContainsKey(sequence))
        {
            receiveBuffer.Add(
                sequence,
                payload);
        }

        DeliverOrderedPackets();
    }

    private void HandleAckPacket(int sequence)
    {
        if (VerboseLogging)
        {
            Debug.Log(
                $"[ReliableTransport] Receive ACK #{sequence}");
        }

        pendingPackets.Remove(sequence);
    }

    private void DeliverOrderedPackets()
    {
        while (
            receiveBuffer.TryGetValue(
                expectedReceiveSequence,
                out byte[] payload))
        {
            receiveBuffer.Remove(
                expectedReceiveSequence);

            expectedReceiveSequence++;

            MessageReceived?.Invoke(payload);
        }
    }

    private IEnumerator RetryLoop()
    {
        while (!disposed)
        {
            yield return new WaitForSeconds(0.05f);

            if (!IsConnected)
                yield break;

            float now = Time.time;

            List<int> sequencesToRetry =
                new List<int>();

            foreach (KeyValuePair<int, PendingPacket> pair
                     in pendingPackets)
            {
                PendingPacket packet = pair.Value;

                if (now - packet.LastSendTime <
                    retryTimeout)
                {
                    continue;
                }

                if (packet.RetryCount >= maxRetries)
                {
                    Debug.LogError(
                        $"[ReliableTransport] " +
                        $"Packet #{pair.Key} failed " +
                        $"after {maxRetries} retries.");

                    Disconnect();

                    yield break;
                }

                sequencesToRetry.Add(pair.Key);
            }

            foreach (int sequence in sequencesToRetry)
            {
                if (!pendingPackets.TryGetValue(
                    sequence,
                    out PendingPacket packet))
                {
                    continue;
                }

                packet.RetryCount++;
                packet.LastSendTime = Time.time;

                if (VerboseLogging)
                {
                    Debug.Log(
                        $"[ReliableTransport] " +
                        $"Retry DATA #{sequence}, " +
                        $"attempt {packet.RetryCount}");
                }

                innerTransport.Send(packet.Data);
            }
        }
    }

    private void OnInnerDisconnected()
    {
        if (disposed)
            return;

        disposed = true;

        pendingPackets.Clear();
        receiveBuffer.Clear();

        Disconnected?.Invoke();
    }

    public void Disconnect()
    {
        if (disposed)
            return;

        disposed = true;

        pendingPackets.Clear();
        receiveBuffer.Clear();

        innerTransport.Disconnect();

        Disconnected?.Invoke();
    }
}
