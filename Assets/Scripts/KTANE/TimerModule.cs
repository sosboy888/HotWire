// TimerModule.cs
// Countdown timer for the KTANE bomb.
//
// UBIQ NETWORKING PATTERN:
//   Only the Defuser client advances the timer (Update() is guarded by
//   IsLocalDefuser).  Every <syncInterval> seconds the current time is
//   broadcast to all peers with context.SendJson().  Peers apply the
//   received value in ProcessMessage(), keeping both displays in sync.
//
// INSPECTOR SETUP:
//   • Attach to a GameObject that is a child of the bomb (or the bomb root).
//   • Assign the "Timer Display" child (Timer_Display) to the timerDisplay field.
//   • The script discovers KTANEGameManager via KTANEGameManager.Instance.

using System;
using System.Collections;
using UnityEngine;
using TMPro;
using Ubiq.Messaging;

namespace KTANE
{
    // -------------------------------------------------------------------------
    // Network message
    // -------------------------------------------------------------------------
    [Serializable]
    public struct TimerMessage
    {
        public string type;          // "tick" | "pause" | "explode"
        public float  timeRemaining; // seconds
    }

    // -------------------------------------------------------------------------
    // TimerModule
    // -------------------------------------------------------------------------
    public class TimerModule : MonoBehaviour
    {
        // ----- Inspector -------------------------------------------------
        [Header("References")]
        [Tooltip("The Timer_Display child mesh that shows the countdown.")]
        public Renderer timerDisplay;

        [Tooltip("World-space TMP text that shows the MM:SS countdown. " +
                 "Auto-created above the bomb if left empty.")]
        public TextMeshPro timerText;

        [Header("Settings")]
        [Tooltip("Total countdown time in seconds (default = 5 minutes).")]
        public float startTime = 300f;

        [Tooltip("How often (seconds) the timer state is broadcast over the network.")]
        public float syncInterval = 0.5f;

        // ----- Runtime ---------------------------------------------------
        public float TimeRemaining { get; private set; }
        public bool  IsRunning     { get; private set; }

        // ----- Ubiq internals -------------------------------------------
        private NetworkContext context;

        // Colour lerp colours for timer display emission
        private static readonly Color ColorGreen  = new Color(0f, 1f, 0f);
        private static readonly Color ColorYellow = new Color(1f, 1f, 0f);
        private static readonly Color ColorRed    = new Color(1f, 0f, 0f);

        // Keep a private material instance so we don't dirty the shared asset
        private Material displayMat;

        private float syncTimer;

        // Tick sound: fire once per integer-second boundary
        private int _lastTickSecond = -1;

        // ================================================================
        // Unity lifecycle
        // ================================================================

        private void Start()
        {
            context       = NetworkScene.Register(this);
            TimeRemaining = startTime;

            if (timerDisplay != null)
            {
                displayMat = timerDisplay.material;
                displayMat.EnableKeyword("_EMISSION");
            }
            else
            {
                Debug.LogWarning("[TimerModule] timerDisplay not assigned.", this);
            }

            // Auto-create a world-space LCD-style display on the Timer_Display face.
            if (timerText == null)
            {
                // Parent to the bomb root (Timer_Display's parent) so the text follows
                // the bomb model wherever it is placed, not BombMount independently.
                // Falls back to BombMount if timerDisplay hasn't been wired yet.
                Transform bombRoot   = timerDisplay != null
                    ? timerDisplay.transform.parent
                    : (transform.parent ?? transform);

                // Position: same XY as Timer_Display, 7 mm in front of its face (−Z).
                Vector3 basePos = timerDisplay != null
                    ? timerDisplay.transform.localPosition
                    : new Vector3(0f, 0.048f, -0.124f);

                // Quaternion.identity is correct: TMP's readable face points in local −Z,
                // which equals world −Z — exactly where the Defuser stands.
                // Scale 0.001 → 1 canvas unit = 1 mm world.  sizeDelta (160 × 65) → 16 cm × 6.5 cm.
                // Scale 0.001 → 1 canvas unit = 1 mm.
                // sizeDelta (240 × 80) → 24 cm × 8 cm — fills the bomb face and
                // is large enough to read clearly in VR at arm's length.
                var root = new GameObject("TimerDisplay");
                root.transform.SetParent(bombRoot, false);
                root.transform.localPosition = new Vector3(basePos.x, basePos.y, basePos.z - 0.007f);
                root.transform.localRotation = Quaternion.identity;
                root.transform.localScale    = Vector3.one * 0.001f;

                var activeGO = new GameObject("Active");
                activeGO.transform.SetParent(root.transform, false);
                activeGO.transform.localPosition = Vector3.zero;

                timerText                  = activeGO.AddComponent<TextMeshPro>();
                timerText.enableAutoSizing = true;
                timerText.fontSizeMin      = 8f;
                timerText.fontSizeMax      = 240f;
                timerText.fontStyle        = FontStyles.Bold;
                timerText.color            = Color.black;
                timerText.alignment        = TextAlignmentOptions.Center;
                timerText.characterSpacing = 6f;
                timerText.GetComponent<RectTransform>().sizeDelta = new Vector2(720f, 240f);
            }

            UpdateDisplay();
        }

        private void Update()
        {
            var gm = KTANEGameManager.Instance;
            if (gm == null) return;

            // Only the Defuser advances the timer.
            if (gm.CurrentState != GameState.Active) return;
            if (!gm.IsLocalDefuser)               return;

            TimeRemaining -= Time.deltaTime;

            if (TimeRemaining <= 0f)
            {
                TimeRemaining = 0f;
                UpdateDisplay();
                BroadcastTick();
                gm.ExplodeBomb();
                IsRunning = false;
                return;
            }

            IsRunning = true;
            UpdateDisplay();

            // Tick sound: fire once each time the integer-second boundary crosses
            int nowSec = Mathf.FloorToInt(TimeRemaining);
            if (nowSec != _lastTickSecond)
            {
                _lastTickSecond = nowSec;
                KTANESoundManager.Instance?.PlayTick();
            }

            // Throttle network syncs to avoid flooding
            syncTimer += Time.deltaTime;
            if (syncTimer >= syncInterval)
            {
                syncTimer = 0f;
                BroadcastTick();
            }
        }

        // ================================================================
        // Public helpers (read-only for Expert UI / other modules)
        // ================================================================

        /// <summary>Returns the last digit of the integer seconds remaining.</summary>
        public int LastDigit => Mathf.FloorToInt(TimeRemaining) % 10;

        /// <summary>Returns the whole seconds remaining.</summary>
        public int SecondsRemaining => Mathf.FloorToInt(TimeRemaining);

        /// <summary>
        /// Configure the timer for a specific level.
        /// Called by KTANELobbyManager before the game starts.
        /// </summary>
        public void Configure(float durationSeconds)
        {
            startTime     = durationSeconds;
            TimeRemaining = durationSeconds;
            IsRunning     = false;
            syncTimer     = 0f;
            UpdateDisplay();
            Debug.Log($"[TimerModule] Configured: {durationSeconds:F0}s", this);
        }

        // ================================================================
        // Ubiq message handling
        // ================================================================

        public void ProcessMessage(ReferenceCountedSceneGraphMessage message)
        {
            var msg = message.FromJson<TimerMessage>();
            if (msg.type == "tick")
            {
                // Accept remote time only if we are NOT the Defuser (we own
                // the authoritative value); or if the remote value is
                // meaningfully different (> 1s drift, in case of late join).
                var gm = KTANEGameManager.Instance;
                if (gm != null && !gm.IsLocalDefuser)
                {
                    TimeRemaining = msg.timeRemaining;
                    UpdateDisplay();
                }
            }
        }

        // ================================================================
        // Private helpers
        // ================================================================

        private void BroadcastTick()
        {
            if (context.Scene == null) return;
            context.SendJson(new TimerMessage
            {
                type          = "tick",
                timeRemaining = TimeRemaining
            });
        }

        private void UpdateDisplay()
        {
            int totalSec = Mathf.CeilToInt(TimeRemaining);
            int mins = totalSec / 60;
            int secs = totalSec % 60;
            string timeStr = $"{mins:D2}:{secs:D2}";

            // Update world-space countdown text
            if (timerText != null)
            {
                timerText.text = timeStr;

                timerText.color = Color.black;
            }

            // Update emission glow on the physical display mesh
            if (displayMat != null)
            {
                float fraction = startTime > 0 ? Mathf.Clamp01(TimeRemaining / startTime) : 1f;
                Color emissive;
                if (fraction > 0.5f)
                    emissive = Color.Lerp(ColorYellow, ColorGreen, (fraction - 0.5f) * 2f);
                else
                    emissive = Color.Lerp(ColorRed, ColorYellow, fraction * 2f);
                displayMat.SetColor("_EmissionColor", emissive * 2f);
            }
        }
    }
}
