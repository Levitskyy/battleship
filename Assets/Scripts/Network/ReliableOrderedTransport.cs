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

    // Следующий sequence для исходящего сообщения.
    private int nextSendSequence = 1;

    // Какой sequence мы ждём от удалённой стороны.
    private int expectedReceiveSequence = 1;

    // Пакеты, которые отправили, но ещё не получили ACK.
    private readonly Dictionary<int, PendingPacket> pendingPackets =
        new Dictionary<int, PendingPacket>();

    // Пакеты, которые пришли раньше времени.
    private readonly Dictionary<int, byte[]> receiveBuffer =
        new Dictionary<int, byte[]>();

    private bool disposed;

    public bool IsConnected =>
        !disposed && innerTransport.IsConnected;

    public event Action<byte[]> MessageReceived;
    public event Action Disconnected;

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

        coroutineHost.StartCoroutine(RetryLoop());
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

        Debug.Log(
            $"[ReliableTransport] Send DATA #{sequence}");

        innerTransport.Send(packet);
    }

    private byte[] CreateDataPacket(
        int sequence,
        byte[] payload)
    {
        // 1 byte type
        // 4 bytes sequence
        // N bytes payload

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
        // 1 byte type
        // 4 bytes sequence

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
        Debug.Log(
            $"[ReliableTransport] Receive DATA #{sequence}");

        // Отправляем ACK сразу.
        // ACK не проходит через reliable layer.
        byte[] ack =
            CreateAckPacket(sequence);

        innerTransport.Send(ack);

        // Старый пакет.
        if (sequence < expectedReceiveSequence)
        {
            Debug.Log(
                $"[ReliableTransport] Duplicate DATA #{sequence}");

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

        // Если пакет уже лежит в buffer,
        // повторно его не сохраняем.
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
        Debug.Log(
            $"[ReliableTransport] Receive ACK #{sequence}");

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

            int deliveredSequence =
                expectedReceiveSequence;

            expectedReceiveSequence++;

            Debug.Log(
                $"[ReliableTransport] Deliver DATA #{deliveredSequence}");

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

                Debug.Log(
                    $"[ReliableTransport] " +
                    $"Retry DATA #{sequence}, " +
                    $"attempt {packet.RetryCount}");

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