using System;
using System.Collections;
using System.Security.Cryptography;
using System.Text;
using Ubiq.Messaging;
using Ubiq.Rooms;
using UnityEngine;

public class AutoRoomJoiner : MonoBehaviour
{
    [SerializeField] private string roomName = "ucl-hw-teamindia";
    [SerializeField] private bool publish = false;
    [SerializeField] private float autoJoinDelaySeconds = 0.5f;
    [SerializeField] private bool requireConnection = true;

    private RoomClient roomClient;
    private NetworkScene networkScene;
    private bool joinRequested;

    private void OnEnable()
    {
        TryBindRoomClient();
    }

    private void OnDisable()
    {
        UnbindRoomClient();
    }

    private IEnumerator Start()
    {
        // Give Ubiq a frame to initialize its NetworkScene/RoomClient.
        yield return null;

        while (roomClient == null)
        {
            TryBindRoomClient();
            if (roomClient == null)
            {
                yield return null;
            }
        }

        networkScene = NetworkScene.Find(this);

        if (autoJoinDelaySeconds > 0f)
        {
            yield return new WaitForSeconds(autoJoinDelaySeconds);
        }

        if (requireConnection)
        {
            while (networkScene != null && networkScene.connectionCount == 0)
            {
                yield return null;
            }
        }

        if (roomClient.JoinedRoom)
        {
            Debug.Log($"[AutoRoomJoiner] Already in room '{roomClient.Room?.Name}'.");
            yield break;
        }

        JoinRoom();
    }

    private void TryBindRoomClient()
    {
        if (roomClient != null)
        {
            return;
        }

        roomClient = RoomClient.Find(this);
        if (roomClient != null)
        {
            roomClient.OnJoinedRoom.AddListener(OnJoinedRoom);
            roomClient.OnJoinRejected.AddListener(OnJoinRejected);
        }
    }

    private void UnbindRoomClient()
    {
        if (roomClient != null)
        {
            roomClient.OnJoinedRoom.RemoveListener(OnJoinedRoom);
            roomClient.OnJoinRejected.RemoveListener(OnJoinRejected);
        }
    }

    private void JoinRoom()
    {
        var guid = StableGuidFromString(roomName);
        Debug.Log($"[AutoRoomJoiner] Connecting and joining room '{roomName}' (guid {guid}).");
        joinRequested = true;
        roomClient.Join(guid);
    }

    private void OnJoinedRoom(IRoom room)
    {
        Debug.Log($"[AutoRoomJoiner] Joined room '{room?.Name}' (joincode {room?.JoinCode}).");
    }

    private void OnJoinRejected(Rejection rejection)
    {
        var msg = $"[AutoRoomJoiner] Join rejected. Reason: {rejection.reason}";
        if (joinRequested)
        {
            Debug.LogWarning(msg);
        }
        else
        {
            Debug.Log(msg);
        }
    }

    private static Guid StableGuidFromString(string input)
    {
        using (var md5 = MD5.Create())
        {
            var bytes = Encoding.UTF8.GetBytes(input ?? string.Empty);
            var hash = md5.ComputeHash(bytes);
            return new Guid(hash);
        }
    }
}