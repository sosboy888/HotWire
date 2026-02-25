using System;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using Ubiq.Messaging;
using Ubiq.Rooms;

public class NetworkTestCube : MonoBehaviour
{
    [Serializable]
    private struct CubeMessage
    {
        public string type;
        public bool state;
        public string requester;
        public string authority;
    }

    [SerializeField] private Color offColor = Color.white;
    [SerializeField] private Color onColor = Color.red;

    private const string MsgState = "state";
    private const string MsgRequest = "request";

    private NetworkContext context;
    private RoomClient roomClient;
    private Renderer cachedRenderer;
    private XRSimpleInteractable interactable;

    private bool state;
    private string authorityUuid;

    private void Start()
    {
        context = NetworkScene.Register(this);
        roomClient = RoomClient.Find(this);
        cachedRenderer = GetComponent<Renderer>();
        interactable = GetComponent<XRSimpleInteractable>();

        EnsureShader();

        if (interactable != null)
        {
            interactable.selectEntered.AddListener(OnSelectEntered);
        }
        else
        {
            Debug.LogWarning("[NetworkTestCube] Missing XRSimpleInteractable.", this);
        }

        if (roomClient != null)
        {
            roomClient.OnPeerAdded.AddListener(_ => RecomputeAuthority());
            roomClient.OnPeerRemoved.AddListener(_ => RecomputeAuthority());
            roomClient.OnJoinedRoom.AddListener(_ => RecomputeAuthority());
        }
        else
        {
            Debug.LogWarning("[NetworkTestCube] RoomClient not found.", this);
        }

        ApplyState();
        RecomputeAuthority();
    }

    private void EnsureShader()
    {
        if (cachedRenderer == null)
        {
            return;
        }

        var material = cachedRenderer.material;
        if (material == null)
        {
            return;
        }

        var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        if (shader != null)
        {
            material.shader = shader;
        }
    }

    private void OnDestroy()
    {
        if (interactable != null)
        {
            interactable.selectEntered.RemoveListener(OnSelectEntered);
        }
    }

    private void OnSelectEntered(SelectEnterEventArgs args)
    {
        Debug.Log($"[NetworkTestCube] Select attempt by {roomClient?.Me?.uuid ?? "(unknown)"}.", this);

        if (context.Scene == null)
        {
            Debug.LogWarning("[NetworkTestCube] No NetworkScene, cannot send messages.", this);
            return;
        }

        if (IsAuthority())
        {
            Debug.Log("[NetworkTestCube] Local peer is authority. Toggling.", this);
            ToggleAndBroadcast();
        }
        else
        {
            Debug.Log($"[NetworkTestCube] Local peer is NOT authority (authority={authorityUuid}). Requesting toggle.", this);
            SendRequest();
        }
    }

    private void ToggleAndBroadcast()
    {
        state = !state;
        ApplyState();
        BroadcastState();
    }

    private void SendRequest()
    {
        var msg = new CubeMessage
        {
            type = MsgRequest,
            requester = roomClient?.Me?.uuid
        };
        context.SendJson(msg);
    }

    private void BroadcastState()
    {
        var msg = new CubeMessage
        {
            type = MsgState,
            state = state,
            authority = authorityUuid
        };
        context.SendJson(msg);
        Debug.Log($"[NetworkTestCube] Broadcast state={(state ? "ON" : "OFF")} authority={authorityUuid}.", this);
    }

    public void ProcessMessage(ReferenceCountedSceneGraphMessage message)
    {
        var msg = JsonUtility.FromJson<CubeMessage>(message.ToString());
        if (msg.type == MsgState)
        {
            state = msg.state;
            ApplyState();
            Debug.Log($"[NetworkTestCube] State update received: {(state ? "ON" : "OFF")} (authority={msg.authority}).", this);
        }
        else if (msg.type == MsgRequest)
        {
            Debug.Log($"[NetworkTestCube] Toggle request received from {msg.requester}.", this);
            if (IsAuthority())
            {
                Debug.Log("[NetworkTestCube] Authority handling request.", this);
                ToggleAndBroadcast();
            }
        }
    }

    private void ApplyState()
    {
        if (cachedRenderer != null)
        {
            cachedRenderer.material.color = state ? onColor : offColor;
        }
    }

    private void RecomputeAuthority()
    {
        var newAuthority = ComputeAuthorityUuid();
        if (authorityUuid != newAuthority)
        {
            authorityUuid = newAuthority;
            Debug.Log($"[NetworkTestCube] Authority is now {authorityUuid}.", this);

            if (IsAuthority() && context.Scene != null)
            {
                BroadcastState();
            }
        }
    }

    private bool IsAuthority()
    {
        return roomClient != null && authorityUuid == roomClient.Me.uuid;
    }

    private string ComputeAuthorityUuid()
    {
        if (roomClient == null || roomClient.Me == null)
        {
            return string.Empty;
        }

        var min = roomClient.Me.uuid;
        foreach (var peer in roomClient.Peers)
        {
            if (string.CompareOrdinal(peer.uuid, min) < 0)
            {
                min = peer.uuid;
            }
        }

        return min;
    }
}
