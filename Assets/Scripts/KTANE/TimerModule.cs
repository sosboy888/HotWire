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

        // ================================================================
        // Unity lifecycle
        // ================================================================

        private void Start()
        {
            context       = NetworkScene.Register(this);
            TimeRemaining = startTime;

            if (timerDisplay != null)
            {
                // Create a per-instance material so emission changes don't
                // affect other objects using the same shared material.
                displayMat = timerDisplay.material;
                displayMat.EnableKeyword("_EMISSION");
            }
            else
            {
                Debug.LogWarning("[TimerModule] timerDisplay not assigned.", this);
            }
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

            IsRunning  = true;
            UpdateDisplay();

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
            if (displayMat == null) return;

            // Fraction of time remaining: 1 = full time, 0 = out of time
            float fraction = Mathf.Clamp01(TimeRemaining / startTime);

            // Green → Yellow → Red
            Color emissive;
            if (fraction > 0.5f)
                emissive = Color.Lerp(ColorYellow, ColorGreen, (fraction - 0.5f) * 2f);
            else
                emissive = Color.Lerp(ColorRed, ColorYellow, fraction * 2f);

            // Multiply by HDR intensity so it actually glows
            displayMat.SetColor("_EmissionColor", emissive * 2f);

            // Update the display texture name / text if using a TextMesh or
            // similar; for now we only drive emission colour.
            // To update a TMP/TextMesh, get the component here and set .text
        }
    }
}
