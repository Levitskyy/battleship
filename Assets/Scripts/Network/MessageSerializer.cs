using System;
using System.Text;
using UnityEngine;

public static class MessageSerializer
{
    public static byte[] Serialize<T>(
        MessageType type,
        T message)
    {
        string json = JsonUtility.ToJson(message);

        byte[] payload =
            Encoding.UTF8.GetBytes(json);

        byte[] result =
            new byte[1 + payload.Length];

        result[0] = (byte)type;

        Buffer.BlockCopy(
            payload,
            0,
            result,
            1,
            payload.Length);

        return result;
    }

    public static MessageType GetMessageType(byte[] data)
    {
        return (MessageType)data[0];
    }

    public static T Deserialize<T>(byte[] data)
    {
        string json =
            Encoding.UTF8.GetString(
                data,
                1,
                data.Length - 1);

        return JsonUtility.FromJson<T>(json);
    }
}