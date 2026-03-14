// KTANEGameManager.cs
// Central game state machine for the VR KTANE recreation.
//
// UBIQ NETWORKING PATTERN:
//   This script registers with NetworkScene.Register(this) to receive a
//   NetworkContext.  All peers run identical code; authority is the peer
//   whose UUID is lexicographically smallest – that peer becomes the Defuser
//   and is the only one that drives solve / strike logic.  State changes are
//   broadcast with context.SendJson() and applied on every client (including
//   the sender) through ProcessMessage().
//
// INSPECTOR SETUP:
//   Attach to a persistent GameObject that also has a NetworkScene component
//   in its hierarchy (e.g. the NetworkScene root GameObject).
//   The "Modules Solved To Win" field controls how many modules must be solved.

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using Ubiq.Messaging;
using Ubiq.Rooms;

namespace KTANE
{
    // -------------------------------------------------------------------------
    // Game states
    // -------------------------------------------------------------------------
    public enum GameState { Waiting, Active, Defused, Exploded }

    // -------------------------------------------------------------------------
    // Player roles – derived from peer order so both clients agree
    // -------------------------------------------------------------------------
    public enum PlayerRole { Unassigned, Defuser, Expert }

    // -------------------------------------------------------------------------
    // Network message exchanged between clients
    // -------------------------------------------------------------------------
    [Serializable]
    public struct GameStateMessage
    {
        public string type;        // "state" | "role"
        public int    gameState;   // cast of GameState enum
        public int    strikes;
        public int    modulesSolved;
        // Role message fields
        public string defuserUuid;
    }

    // -------------------------------------------------------------------------
    // KTANEGameManager
    // -------------------------------------------------------------------------
    public class KTANEGameManager : MonoBehaviour
    {
        // ----- Inspector -------------------------------------------------
        [Header("Game Settings")]
        [Tooltip("Number of modules that must be solved to defuse the bomb.")]
        public int modulesToSolve = 5;

        [Header("Events – fired on every client")]
        public UnityEvent OnGameStarted;
        public UnityEvent OnBombDefused;
        public UnityEvent OnBombExploded;
        public UnityEvent<int> OnStrikeAdded;     // arg: new strike count
        public UnityEvent<PlayerRole> OnRoleAssigned; // arg: local player role

        // ----- Runtime state (authoritative on Defuser, replicated on Expert)
        public GameState   CurrentState   { get; private set; } = GameState.Waiting;
        public int         Strikes        { get; private set; } = 0;
        public int         ModulesSolved  { get; private set; } = 0;
        public PlayerRole  LocalRole      { get; private set; } = PlayerRole.Unassigned;
        public bool        IsLocalDefuser => LocalRole == PlayerRole.Defuser;

        // Maximum strikes before explosion (overridden by SetLevelConfig)
        public int MaxStrikes { get; private set; } = 3;

        // ----- Ubiq internals -------------------------------------------
        private NetworkContext context;
        private RoomClient     roomClient;
        private string         defuserUuid = string.Empty;

        // ----- Singleton ------------------------------------------------
        private static KTANEGameManager _instance;
        public  static KTANEGameManager Instance => _instance;

        // ================================================================
        // Unity lifecycle
        // ================================================================

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
        }

        private void Start()
        {
            // Register with Ubiq's NetworkScene so peers can exchange messages.
            context = NetworkScene.Register(this);

            roomClient = RoomClient.Find(this);
            if (roomClient != null)
            {
                roomClient.OnPeerAdded.AddListener(_   => ReevaluateRoles());
                roomClient.OnPeerRemoved.AddListener(_ => ReevaluateRoles());
                roomClient.OnJoinedRoom.AddListener(_  => ReevaluateRoles());
            }
            else
            {
                Debug.LogWarning("[KTANEGameManager] No RoomClient found in scene.", this);
            }
        }

        // ================================================================
        // Public API – called by TimerModule / WiresModule / etc.
        // ONLY the Defuser client should call these.
        // ================================================================

        /// <summary>
        /// Apply level settings (module count, max strikes).
        /// Called by KTANELobbyManager before StartGame().
        /// </summary>
        public void SetLevelConfig(KTANELevelConfig cfg)
        {
            if (cfg == null) return;
            modulesToSolve = cfg.ModulesToSolve;
            MaxStrikes     = cfg.maxStrikes;
            Debug.Log($"[KTANEGameManager] Level configured: {cfg.levelName} " +
                      $"– {modulesToSolve} modules, {MaxStrikes} strikes allowed.", this);
        }

        /// <summary>Start the game. Call from a lobby button on the Defuser client.</summary>
        public void StartGame()
        {
            if (!IsLocalDefuser || CurrentState != GameState.Waiting) return;
            ApplyState(GameState.Active, 0, 0);
            BroadcastState();
            OnGameStarted?.Invoke();
        }

        /// <summary>Called by a module when the player cuts the wrong wire, presses wrong button, etc.</summary>
        public void AddStrike()
        {
            if (!IsLocalDefuser || CurrentState != GameState.Active) return;
            int newStrikes = Strikes + 1;
            if (newStrikes >= MaxStrikes)
            {
                ExplodeBomb();
            }
            else
            {
                ApplyState(GameState.Active, newStrikes, ModulesSolved);
                BroadcastState();
                OnStrikeAdded?.Invoke(newStrikes);
            }
        }

        /// <summary>Called by a module when it is correctly solved.</summary>
        public void SolveModule()
        {
            if (!IsLocalDefuser || CurrentState != GameState.Active) return;
            int newSolved = ModulesSolved + 1;
            if (newSolved >= modulesToSolve)
            {
                DefuseBomb();
            }
            else
            {
                ApplyState(GameState.Active, Strikes, newSolved);
                BroadcastState();
            }
        }

        /// <summary>Trigger explosion (timer reached 0 or max strikes).</summary>
        public void ExplodeBomb()
        {
            if (!IsLocalDefuser || CurrentState == GameState.Exploded) return;
            ApplyState(GameState.Exploded, Strikes, ModulesSolved);
            BroadcastState();
            OnBombExploded?.Invoke();
        }

        /// <summary>Trigger successful defusal.</summary>
        public void DefuseBomb()
        {
            if (!IsLocalDefuser || CurrentState == GameState.Defused) return;
            ApplyState(GameState.Defused, Strikes, ModulesSolved);
            BroadcastState();
            OnBombDefused?.Invoke();
        }

        // ================================================================
        // Ubiq message handling
        // ================================================================

        /// <summary>
        /// Called by Ubiq for every message sent to this object by OTHER peers.
        /// ProcessMessage is the conventional Ubiq callback name and must be public.
        /// </summary>
        public void ProcessMessage(ReferenceCountedSceneGraphMessage message)
        {
            var msg = message.FromJson<GameStateMessage>();

            if (msg.type == "state")
            {
                bool wasActive  = CurrentState == GameState.Active;
                bool wasWaiting = CurrentState == GameState.Waiting;

                int prevStrikes = Strikes;
                ApplyState((GameState)msg.gameState, msg.strikes, msg.modulesSolved);

                if (wasWaiting && CurrentState == GameState.Active)
                    OnGameStarted?.Invoke();

                if (prevStrikes != Strikes)
                    OnStrikeAdded?.Invoke(Strikes);

                if (CurrentState == GameState.Defused)
                    OnBombDefused?.Invoke();

                if (CurrentState == GameState.Exploded)
                    OnBombExploded?.Invoke();
            }
            else if (msg.type == "role")
            {
                defuserUuid = msg.defuserUuid;
                AssignLocalRole();
            }
        }

        // ================================================================
        // Private helpers
        // ================================================================

        private void ApplyState(GameState state, int strikes, int solved)
        {
            CurrentState  = state;
            Strikes       = strikes;
            ModulesSolved = solved;
        }

        private void BroadcastState()
        {
            if (context.Scene == null) return;
            context.SendJson(new GameStateMessage
            {
                type         = "state",
                gameState    = (int)CurrentState,
                strikes      = Strikes,
                modulesSolved = ModulesSolved
            });
        }

        // ----------------------------------------------------------------
        // Role assignment: lowest UUID peer = Defuser, other peer = Expert.
        // Both clients run this calculation independently and reach the same
        // conclusion, so no extra network message is needed for roles – but
        // we also broadcast for late-joining peers.
        // ----------------------------------------------------------------
        private void ReevaluateRoles()
        {
            if (roomClient == null || roomClient.Me == null) return;

            string myUuid  = roomClient.Me.uuid;
            string minUuid = myUuid;

            foreach (var peer in roomClient.Peers)
            {
                if (string.CompareOrdinal(peer.uuid, minUuid) < 0)
                    minUuid = peer.uuid;
            }

            defuserUuid = minUuid;
            AssignLocalRole();

            // Broadcast so any late-joiner also gets the assignment.
            if (context.Scene != null)
            {
                context.SendJson(new GameStateMessage
                {
                    type        = "role",
                    defuserUuid = defuserUuid
                });
            }
        }

        private void AssignLocalRole()
        {
            if (roomClient == null || roomClient.Me == null) return;
            var newRole = (roomClient.Me.uuid == defuserUuid)
                ? PlayerRole.Defuser
                : PlayerRole.Expert;

            if (newRole != LocalRole)
            {
                LocalRole = newRole;
                Debug.Log($"[KTANEGameManager] Local role assigned: {LocalRole}", this);
                OnRoleAssigned?.Invoke(LocalRole);
            }
        }
    }
}
