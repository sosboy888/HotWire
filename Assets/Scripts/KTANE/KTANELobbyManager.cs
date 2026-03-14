// KTANELobbyManager.cs
// Networked lobby that lets both players choose a difficulty level and ready up
// before the bomb appears.
//
// UBIQ PATTERN:
//   Every state change (level pick, ready toggle, countdown, game start) is
//   broadcast with context.SendJson().  The receiver applies the change in
//   ProcessMessage().  Both clients run identical code; the peer with the
//   lowest UUID is the "host" that actually triggers the countdown.
//
// SCENE SETUP:
//   Attach to a persistent GameObject.  The script builds its own World-Space
//   Canvas at runtime so nothing extra is needed in the Inspector.
//   Assign the five module scripts and the BombMount transform in the Inspector
//   so the lobby can show/hide the bomb and configure modules on game-start.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Ubiq.Messaging;
using Ubiq.Rooms;

namespace KTANE
{
    // ── network message ──────────────────────────────────────────────────────
    [Serializable]
    public struct LobbyMessage
    {
        public string type;          // "player_state" | "start_countdown" | "start_game"
        public string senderUuid;
        public bool   isReady;
        public int    selectedLevel; // 0-based index into sorted level list
        public int    randomSeed;    // only populated in "start_game"
        public float  countdown;     // only in "start_countdown"
    }

    // ─────────────────────────────────────────────────────────────────────────
    public class KTANELobbyManager : MonoBehaviour
    {
        // ── Inspector references ─────────────────────────────────────────────
        [Header("Module Scripts (drag from scene)")]
        public TimerModule   timerModule;
        public WiresModule   wiresModule;
        public ButtonModule  buttonModule;
        public KeypadModule  keypadModule;
        public SimonModule   simonModule;

        [Header("Scene Objects")]
        [Tooltip("The BombMount transform – hidden during lobby, shown on game start.")]
        public Transform bombMount;

        // ── runtime state ────────────────────────────────────────────────────
        private KTANELevelConfig[] levels;   // sorted by levelNumber
        private int     selectedLevelIndex   = 0;
        private bool    localReady           = false;
        private bool    remoteReady          = false;
        private string  remoteUuid           = string.Empty;
        private bool    gameStarted          = false;

        // ── Ubiq ─────────────────────────────────────────────────────────────
        private NetworkContext context;
        private RoomClient     roomClient;

        // ── UI references (built at runtime) ─────────────────────────────────
        private GameObject     lobbyCanvas;
        private TextMeshProUGUI txtLevel;
        private TextMeshProUGUI txtDescription;
        private TextMeshProUGUI txtPlayer1;
        private TextMeshProUGUI txtPlayer2;
        private TextMeshProUGUI txtStatus;
        private Button          btnReady;
        private TextMeshProUGUI txtReadyLabel;

        // =====================================================================
        // Unity lifecycle
        // =====================================================================

        private void Start()
        {
            // Load level configs from Resources/KTANE/Levels/
            levels = Resources.LoadAll<KTANELevelConfig>("KTANE/Levels")
                              .OrderBy(l => l.levelNumber)
                              .ToArray();

            if (levels.Length == 0)
            {
                Debug.LogError("[KTANELobbyManager] No KTANELevelConfig assets found in " +
                               "Resources/KTANE/Levels/. Run Tools ▶ KTANE ▶ Create Level Assets.");
            }

            context    = NetworkScene.Register(this);
            roomClient = RoomClient.Find(this);

            if (roomClient != null)
            {
                roomClient.OnPeerAdded.AddListener(_ =>
                {
                    UpdateRemotePeerInfo();
                    BroadcastMyState(); // re-sync state with new joiner
                });
                roomClient.OnPeerRemoved.AddListener(_ =>
                {
                    remoteReady = false;
                    UpdateRemotePeerInfo();
                    RefreshUI();
                });
                roomClient.OnJoinedRoom.AddListener(_ => UpdateRemotePeerInfo());
            }

            BuildLobbyUI();

            // Hide bomb until game starts
            if (bombMount != null)
                bombMount.gameObject.SetActive(false);
        }

        // =====================================================================
        // Ubiq message handling
        // =====================================================================

        public void ProcessMessage(ReferenceCountedSceneGraphMessage message)
        {
            var msg = message.FromJson<LobbyMessage>();

            if (msg.type == "player_state")
            {
                remoteUuid        = msg.senderUuid;
                remoteReady       = msg.isReady;
                selectedLevelIndex = msg.selectedLevel; // remote selection wins
                RefreshUI();
            }
            else if (msg.type == "start_countdown")
            {
                StartCoroutine(RunCountdown(msg.countdown, -1, 0)); // no seed yet
            }
            else if (msg.type == "start_game")
            {
                StartCoroutine(LaunchGame(msg.selectedLevel, msg.randomSeed));
            }
        }

        // =====================================================================
        // Button handlers
        // =====================================================================

        private void OnReadyClicked()
        {
            if (gameStarted) return;
            localReady = !localReady;
            BroadcastMyState();
            RefreshUI();
            CheckBothReady();
        }

        private void OnLevelLeft()
        {
            if (gameStarted || localReady) return;
            selectedLevelIndex = (selectedLevelIndex - 1 + levels.Length) % levels.Length;
            BroadcastMyState();
            RefreshUI();
        }

        private void OnLevelRight()
        {
            if (gameStarted || localReady) return;
            selectedLevelIndex = (selectedLevelIndex + 1) % levels.Length;
            BroadcastMyState();
            RefreshUI();
        }

        // =====================================================================
        // Private helpers
        // =====================================================================

        private void CheckBothReady()
        {
            // Only the host (lowest UUID peer) triggers the countdown.
            if (!IsHost()) return;
            if (!localReady || !remoteReady) return;
            if (gameStarted) return;

            // Kick off a 3-second countdown then start the game.
            int seed = UnityEngine.Random.Range(0, int.MaxValue);
            StartCoroutine(RunCountdown(3f, selectedLevelIndex, seed));

            // Tell the other client to start the same countdown.
            BroadcastMsg(new LobbyMessage
            {
                type          = "start_countdown",
                senderUuid    = MyUuid(),
                selectedLevel = selectedLevelIndex,
                countdown     = 3f
            });

            // Schedule the actual start message to arrive after the countdown.
            StartCoroutine(SendStartAfterDelay(3f, selectedLevelIndex, seed));
        }

        private IEnumerator SendStartAfterDelay(float delay, int levelIdx, int seed)
        {
            yield return new WaitForSeconds(delay);
            BroadcastMsg(new LobbyMessage
            {
                type          = "start_game",
                senderUuid    = MyUuid(),
                selectedLevel = levelIdx,
                randomSeed    = seed
            });
            StartCoroutine(LaunchGame(levelIdx, seed));
        }

        private IEnumerator RunCountdown(float duration, int levelIdx, int seed)
        {
            float remaining = duration;
            while (remaining > 0f)
            {
                SetStatus($"Starting in {Mathf.CeilToInt(remaining)}...");
                yield return new WaitForSeconds(1f);
                remaining -= 1f;
            }
        }

        private IEnumerator LaunchGame(int levelIdx, int seed)
        {
            if (gameStarted) yield break;
            gameStarted = true;

            if (levels.Length == 0) yield break;
            levelIdx = Mathf.Clamp(levelIdx, 0, levels.Length - 1);
            var cfg  = levels[levelIdx];

            // Configure every module with shared seed + level config
            timerModule? .Configure(cfg.timerSeconds);
            wiresModule? .Configure(cfg.wiresActive,  cfg.hardWireRules, seed + 1);
            buttonModule?.Configure(cfg.buttonActive, seed + 2);
            keypadModule?.Configure(cfg.keypadActive, seed + 3);
            simonModule? .Configure(cfg.simonActive,  cfg.simonRounds,
                                    cfg.simonFlashSeconds, seed + 4);

            // Tell the game manager how many modules need solving this level
            var gm = KTANEGameManager.Instance;
            if (gm != null)
            {
                gm.SetLevelConfig(cfg);
            }

            // Show the bomb, hide the lobby
            if (bombMount != null)
                bombMount.gameObject.SetActive(true);

            if (lobbyCanvas != null)
                lobbyCanvas.SetActive(false);

            // Start the actual game
            yield return new WaitForSeconds(0.3f);
            gm?.StartGame();

            Debug.Log($"[KTANELobbyManager] Game started: {cfg.levelName} seed={seed}");
        }

        private void BroadcastMyState()
        {
            BroadcastMsg(new LobbyMessage
            {
                type          = "player_state",
                senderUuid    = MyUuid(),
                isReady       = localReady,
                selectedLevel = selectedLevelIndex
            });
        }

        private void BroadcastMsg(LobbyMessage msg)
        {
            if (context.Scene != null)
                context.SendJson(msg);
        }

        private bool IsHost()
        {
            if (roomClient == null || roomClient.Me == null) return true;
            string myUuid = roomClient.Me.uuid;
            foreach (var peer in roomClient.Peers)
                if (string.CompareOrdinal(peer.uuid, myUuid) < 0)
                    return false;
            return true;
        }

        private string MyUuid() => roomClient?.Me?.uuid ?? "local";

        private void UpdateRemotePeerInfo()
        {
            foreach (var peer in roomClient.Peers)
                remoteUuid = peer.uuid;
        }

        private void SetStatus(string text)
        {
            if (txtStatus != null) txtStatus.text = text;
        }

        // =====================================================================
        // UI refresh
        // =====================================================================

        private void RefreshUI()
        {
            if (levels.Length == 0) return;

            var cfg = levels[selectedLevelIndex];

            if (txtLevel != null)
                txtLevel.text = $"◄  Level {cfg.levelNumber}: {cfg.levelName.ToUpper()}  ►";

            if (txtDescription != null)
                txtDescription.text = cfg.description;

            // Player rows
            string myName = $"You ({ShortUuid(MyUuid())})";
            string theirName = string.IsNullOrEmpty(remoteUuid)
                ? "Waiting for player 2..."
                : $"Player 2 ({ShortUuid(remoteUuid)})";

            if (txtPlayer1 != null)
                txtPlayer1.text = $"{myName}  —  {(localReady ? "✓ READY" : "Not ready")}";

            if (txtPlayer2 != null)
                txtPlayer2.text = $"{theirName}  —  {(remoteReady ? "✓ READY" : "Not ready")}";

            // Ready button label
            if (txtReadyLabel != null)
                txtReadyLabel.text = localReady ? "UNREADY" : "READY";

            if (txtStatus != null && !gameStarted)
            {
                bool bothConnected = !string.IsNullOrEmpty(remoteUuid);
                if (!bothConnected)
                    SetStatus("Waiting for second player to join...");
                else if (localReady && remoteReady)
                    SetStatus("Both ready! Starting...");
                else
                    SetStatus("Both players must press READY to begin.");
            }
        }

        private static string ShortUuid(string uuid)
        {
            if (string.IsNullOrEmpty(uuid)) return "?";
            return uuid.Length > 6 ? uuid.Substring(0, 6) : uuid;
        }

        // =====================================================================
        // Build Lobby UI (World-Space Canvas, 0.7m × 0.9m)
        // =====================================================================

        private void BuildLobbyUI()
        {
            // Root canvas -- floats above the table
            lobbyCanvas = new GameObject("LobbyCanvas");
            lobbyCanvas.transform.SetParent(null);
            lobbyCanvas.transform.position = new Vector3(0f, 1.85f, 0f);
            lobbyCanvas.transform.rotation = Quaternion.identity;

            var canvas       = lobbyCanvas.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            lobbyCanvas.AddComponent<CanvasScaler>();
            lobbyCanvas.AddComponent<GraphicRaycaster>();

            var rt       = lobbyCanvas.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(700f, 900f);
            lobbyCanvas.transform.localScale = Vector3.one * 0.001f;

            // Background
            var bg     = MakePanel(lobbyCanvas.transform, "BG",
                             new Color(0.05f, 0.05f, 0.08f, 0.97f));
            Stretch(bg);

            // Title
            var title = MakeTMP(lobbyCanvas.transform, "Title",
                "BOMB DEFUSAL SQUAD", 48, new Vector2(0, 390), new Vector2(660, 70));
            title.fontStyle = FontStyles.Bold;
            title.color     = new Color(1f, 0.85f, 0.2f);

            // Subtitle
            MakeTMP(lobbyCanvas.transform, "Subtitle",
                "Select a level then both press READY", 22,
                new Vector2(0, 330), new Vector2(660, 40));

            // Level strip
            MakeTMP(lobbyCanvas.transform, "LevelHeader",
                "DIFFICULTY", 18, new Vector2(0, 275), new Vector2(660, 30))
                .color = new Color(0.7f, 0.7f, 0.7f);

            var levelBg = MakePanel(lobbyCanvas.transform, "LevelBG",
                              new Color(0.12f, 0.12f, 0.18f));
            var levelBgRT = levelBg.GetComponent<RectTransform>();
            levelBgRT.anchoredPosition = new Vector2(0, 225);
            levelBgRT.sizeDelta        = new Vector2(660, 60);

            txtLevel = MakeTMP(lobbyCanvas.transform, "LevelName",
                "◄  Level 1: TRAINING  ►", 26, new Vector2(0, 225), new Vector2(620, 55));
            txtLevel.color = Color.white;

            // Level arrow buttons
            var btnLeft  = MakeButton(lobbyCanvas.transform, "BtnLeft",  "◄",
                               new Vector2(-290, 225), new Vector2(50, 55));
            var btnRight = MakeButton(lobbyCanvas.transform, "BtnRight", "►",
                               new Vector2(290, 225), new Vector2(50, 55));
            btnLeft.onClick.AddListener(OnLevelLeft);
            btnRight.onClick.AddListener(OnLevelRight);

            // Description
            txtDescription = MakeTMP(lobbyCanvas.transform, "Description",
                "", 20, new Vector2(0, 150), new Vector2(640, 80));
            txtDescription.color     = new Color(0.8f, 0.8f, 0.8f);
            txtDescription.alignment = TextAlignmentOptions.Center;

            // Divider
            MakePanel(lobbyCanvas.transform, "Div1",
                new Color(0.3f, 0.3f, 0.3f))
                .GetComponent<RectTransform>().sizeDelta = new Vector2(660, 2);

            // Player rows
            txtPlayer1 = MakeTMP(lobbyCanvas.transform, "Player1",
                "You — Not ready", 22, new Vector2(0, 60), new Vector2(640, 40));
            txtPlayer1.color = new Color(0.4f, 0.8f, 1f);

            txtPlayer2 = MakeTMP(lobbyCanvas.transform, "Player2",
                "Waiting for player 2...", 22, new Vector2(0, 10), new Vector2(640, 40));
            txtPlayer2.color = new Color(1f, 0.6f, 0.4f);

            // Status line
            txtStatus = MakeTMP(lobbyCanvas.transform, "Status",
                "Waiting for second player...", 20,
                new Vector2(0, -50), new Vector2(640, 40));
            txtStatus.color = new Color(0.6f, 0.6f, 0.6f);

            // Ready button
            var readyBtnObj = MakeButtonObject(lobbyCanvas.transform, "BtnReady",
                                  new Vector2(0, -140), new Vector2(320, 70));
            btnReady     = readyBtnObj.GetComponent<Button>();
            txtReadyLabel = readyBtnObj.GetComponentInChildren<TextMeshProUGUI>();
            if (txtReadyLabel != null) txtReadyLabel.text = "READY";
            btnReady.onClick.AddListener(OnReadyClicked);

            // Level info strip (time + strikes)
            MakeTMP(lobbyCanvas.transform, "InfoStrip",
                "Level info loads when you select a level", 16,
                new Vector2(0, -220), new Vector2(640, 30))
                .color = new Color(0.5f, 0.5f, 0.5f);

            RefreshUI();
        }

        // ── UI factory helpers ────────────────────────────────────────────────

        private static TextMeshProUGUI MakeTMP(Transform parent, string name,
            string text, float size, Vector2 pos, Vector2 sizeDelta)
        {
            var go  = new GameObject(name);
            go.transform.SetParent(parent, false);
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text      = text;
            tmp.fontSize  = size;
            tmp.color     = Color.white;
            tmp.alignment = TextAlignmentOptions.Center;
            var rt = go.GetComponent<RectTransform>();
            rt.anchoredPosition = pos;
            rt.sizeDelta        = sizeDelta;
            return tmp;
        }

        private static Button MakeButton(Transform parent, string name,
            string label, Vector2 pos, Vector2 size)
        {
            var go  = MakeButtonObject(parent, name, pos, size);
            var lbl = go.GetComponentInChildren<TextMeshProUGUI>();
            if (lbl != null) lbl.text = label;
            return go.GetComponent<Button>();
        }

        private static GameObject MakeButtonObject(Transform parent,
            string name, Vector2 pos, Vector2 size)
        {
            var go  = new GameObject(name);
            go.transform.SetParent(parent, false);
            var img  = go.AddComponent<Image>();
            img.color = new Color(0.2f, 0.4f, 0.8f);
            var btn  = go.AddComponent<Button>();
            var rt   = go.GetComponent<RectTransform>();
            rt.anchoredPosition = pos;
            rt.sizeDelta        = size;

            var lblGO = new GameObject("Label");
            lblGO.transform.SetParent(go.transform, false);
            var tmp       = lblGO.AddComponent<TextMeshProUGUI>();
            tmp.text      = name;
            tmp.fontSize  = 26;
            tmp.color     = Color.white;
            tmp.alignment = TextAlignmentOptions.Center;
            var lrt = lblGO.GetComponent<RectTransform>();
            lrt.anchorMin = Vector2.zero;
            lrt.anchorMax = Vector2.one;
            lrt.offsetMin = lrt.offsetMax = Vector2.zero;

            return go;
        }

        private static GameObject MakePanel(Transform parent,
            string name, Color colour)
        {
            var go  = new GameObject(name);
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>();
            img.color = colour;
            return go;
        }

        private static void Stretch(GameObject go)
        {
            var rt = go.GetComponent<RectTransform>();
            if (rt == null) rt = go.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }
    }
}
