// SimonModule.cs
// Simon Says module for the KTANE bomb.
//
// UBIQ NETWORKING PATTERN:
//   The Defuser generates the colour sequence and owns all solve/strike logic.
//   After each player interaction the full Simon state is broadcast via
//   context.SendJson().  The Expert's ProcessMessage() updates the Expert UI
//   and mirrors the flash animation locally.
//
// XR INTERACTION:
//   Simon_Red / Blue / Green / Yellow each need:
//     • Collider
//     • XRSimpleInteractable
//
// FLASH MECHANIC:
//   After the Defuser presses a correct colour, the module flashes the
//   NEXT colour in the current sequence.  The flash drives the _EmissionColor
//   property on each pad's Renderer material.
//
// MAPPING TABLE (strikes → colour translation, no serial number variant):
//   0 strikes:  Red→Red,  Blue→Blue,  Green→Green, Yellow→Yellow (no remap)
//   1 strike:   Red→Blue, Blue→Yellow,Green→Green, Yellow→Red
//   2 strikes:  Red→Yellow,Blue→Green,Green→Red,   Yellow→Blue

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using Ubiq.Messaging;

namespace KTANE
{
    // -------------------------------------------------------------------------
    // Simon colour indices
    // -------------------------------------------------------------------------
    public enum SimonColour { Red = 0, Blue = 1, Green = 2, Yellow = 3 }

    // -------------------------------------------------------------------------
    // Network message
    // -------------------------------------------------------------------------
    [Serializable]
    public struct SimonStateMessage
    {
        public string type;          // "init" | "flash" | "press"
        public int[]  sequence;      // full generated sequence
        public int    currentRound;  // how many colours the player must press (1-based)
        public int[]  playerInput;   // player presses so far this round
        public bool   isSolved;
        public bool   causedStrike;
        public int    flashColour;   // -1 = none; otherwise SimonColour index to flash
    }

    // -------------------------------------------------------------------------
    // SimonModule
    // -------------------------------------------------------------------------
    public class SimonModule : MonoBehaviour
    {
        // ----- Inspector -------------------------------------------------
        [Header("Pad Renderers (Simon_Red, Simon_Blue, Simon_Green, Simon_Yellow)")]
        public Renderer[] padRenderers = new Renderer[4]; // index = SimonColour

        [Header("Pad Interactables (same order: Red, Blue, Green, Yellow)")]
        public XRSimpleInteractable[] padInteractables = new XRSimpleInteractable[4];

        [Header("Settings")]
        [Tooltip("Number of colour rounds before the module is solved.")]
        public int totalRounds = 5;

        [Tooltip("Duration (seconds) each colour flashes when showing the sequence.")]
        public float flashOnTime  = 0.4f;
        public float flashOffTime = 0.2f;

        // Set to false for levels that don't include Simon.
        [HideInInspector] public bool isActive = true;

        // ----- Pad emission colours -------------------------------------
        private static readonly Color[] PadColours = new Color[]
        {
            new Color(1f,  0.1f, 0.1f),   // Red
            new Color(0.1f,0.3f, 1f),     // Blue
            new Color(0.1f,1f,   0.1f),   // Green
            new Color(1f,  1f,   0.1f),   // Yellow
        };
        private static readonly Color PadOff = new Color(0.05f, 0.05f, 0.05f);

        // ----- Runtime ---------------------------------------------------
        public int[]  Sequence     { get; private set; }
        public int    CurrentRound { get; private set; } = 1;
        public bool   IsSolved     { get; private set; }

        private List<int> playerInput = new List<int>();

        // Per-pad material instances
        private Material[] padMats = new Material[4];

        // ----- Ubiq internals -------------------------------------------
        private NetworkContext context;

        // ================================================================
        // Unity lifecycle
        // ================================================================

        private void Start()
        {
            context = NetworkScene.Register(this);

            // Build per-instance materials so emission changes don't affect
            // the shared material asset.
            for (int i = 0; i < 4; i++)
            {
                if (padRenderers[i] == null) continue;
                padMats[i] = padRenderers[i].material;
                padMats[i].EnableKeyword("_EMISSION");
                SetPadEmission(i, false);
            }

            var gm = KTANEGameManager.Instance;
            if (gm != null && gm.IsLocalDefuser)
            {
                GenerateSequence();
                BroadcastState("init", -1);
                StartCoroutine(PlaySequence(CurrentRound));
            }

            // Wire up XR interactables
            for (int i = 0; i < padInteractables.Length; i++)
            {
                if (padInteractables[i] == null) continue;
                int captured = i;
                padInteractables[i].selectEntered.AddListener(_ => OnPadPressed(captured));
            }
        }

        private void OnDestroy()
        {
            for (int i = 0; i < padInteractables.Length; i++)
                padInteractables[i]?.selectEntered.RemoveAllListeners();
        }

        // ================================================================
        // Ubiq message handling
        // ================================================================

        public void ProcessMessage(ReferenceCountedSceneGraphMessage message)
        {
            var msg = message.FromJson<SimonStateMessage>();

            Sequence     = msg.sequence;
            CurrentRound = msg.currentRound;
            IsSolved     = msg.isSolved;

            playerInput.Clear();
            if (msg.playerInput != null)
                playerInput.AddRange(msg.playerInput);

            // Mirror flash on Expert (or replay for late joiners)
            if (msg.flashColour >= 0)
                StartCoroutine(FlashPad(msg.flashColour));

            // If it's the start of a round (Expert side), replay the sequence flash
            if (msg.type == "init" || (msg.type == "press" && playerInput.Count == 0 && !msg.isSolved))
            {
                var gm = KTANEGameManager.Instance;
                if (gm != null && !gm.IsLocalDefuser)
                    StartCoroutine(PlaySequence(CurrentRound));
            }
        }

        // ================================================================
        // XR interaction (Defuser only)
        // ================================================================

        private void OnPadPressed(int padIndex)
        {
            if (!isActive) return;
            var gm = KTANEGameManager.Instance;
            if (gm == null || !gm.IsLocalDefuser || gm.CurrentState != GameState.Active)
                return;
            if (IsSolved) return;

            // Map the physical pad colour through the strike-based rule table
            int mappedColour = MapColour(padIndex, gm.Strikes);

            // Flash the pressed pad briefly
            StartCoroutine(FlashPad(padIndex));
            BroadcastState("press", padIndex);

            int expectedMapped = Sequence[playerInput.Count];

            if (mappedColour == expectedMapped)
            {
                playerInput.Add(padIndex);

                if (playerInput.Count == CurrentRound)
                {
                    // Completed this round
                    if (CurrentRound >= totalRounds)
                    {
                        IsSolved = true;
                        gm.SolveModule();
                        Debug.Log("[SimonModule] All rounds complete – module solved.", this);
                        BroadcastState("press", -1);
                    }
                    else
                    {
                        CurrentRound++;
                        playerInput.Clear();
                        BroadcastState("init", -1);
                        // Play extended sequence for the new round
                        StartCoroutine(PlaySequence(CurrentRound));
                    }
                }
            }
            else
            {
                // Wrong colour → strike and repeat current round
                playerInput.Clear();
                gm.AddStrike();
                Debug.Log("[SimonModule] Wrong colour pressed – strike! Repeating round.", this);
                BroadcastState("press", -1);
                StartCoroutine(PlaySequence(CurrentRound));
            }
        }

        // ================================================================
        // Configuration (called by KTANELobbyManager before game start)
        // ================================================================

        public void Configure(bool active, int rounds, float flashSpeed, int seed)
        {
            isActive   = active;
            totalRounds = rounds;
            flashOnTime = flashSpeed;

            // Show/hide pads
            foreach (var pad in padInteractables)
                if (pad != null) pad.gameObject.SetActive(active);

            if (!active) return;

            IsSolved     = false;
            CurrentRound = 1;
            playerInput.Clear();

            UnityEngine.Random.InitState(seed);
            GenerateSequence();
            BroadcastState("init", -1);

            // Only the Defuser plays the opening flash
            var gm = KTANEGameManager.Instance;
            if (gm != null && gm.IsLocalDefuser)
                StartCoroutine(PlaySequence(CurrentRound));

            Debug.Log($"[SimonModule] Configured: active={active} rounds={rounds} " +
                      $"flash={flashSpeed:F2}s seed={seed}", this);
        }

        // ================================================================
        // Private helpers
        // ================================================================

        private void GenerateSequence()
        {
            Sequence = new int[totalRounds];
            for (int i = 0; i < totalRounds; i++)
                Sequence[i] = UnityEngine.Random.Range(0, 4);

            Debug.Log($"[SimonModule] Sequence: {string.Join(",", Sequence)}", this);
        }

        /// <summary>
        /// Play the flash animation for the first <paramref name="count"/>
        /// colours in the sequence.
        /// </summary>
        private IEnumerator PlaySequence(int count)
        {
            // Small lead-in pause
            yield return new WaitForSeconds(0.5f);

            for (int i = 0; i < count && i < Sequence.Length; i++)
            {
                int col = Sequence[i];
                yield return StartCoroutine(FlashPad(col));
                yield return new WaitForSeconds(flashOffTime);
            }
        }

        private IEnumerator FlashPad(int colIndex)
        {
            SetPadEmission(colIndex, true);
            yield return new WaitForSeconds(flashOnTime);
            SetPadEmission(colIndex, false);
        }

        private void SetPadEmission(int index, bool on)
        {
            if (index < 0 || index >= padMats.Length || padMats[index] == null) return;
            Color c = on ? PadColours[index] * 3f : PadOff;
            padMats[index].SetColor("_EmissionColor", c);
        }

        private void BroadcastState(string type, int flashColour)
        {
            if (context.Scene == null) return;
            context.SendJson(new SimonStateMessage
            {
                type         = type,
                sequence     = Sequence,
                currentRound = CurrentRound,
                playerInput  = playerInput.ToArray(),
                isSolved     = IsSolved,
                causedStrike = false,
                flashColour  = flashColour
            });
        }

        // ----------------------------------------------------------------
        // Colour mapping table (physical pad → expected sequence colour)
        //
        //   Rows   = strike count (0, 1, 2)
        //   Columns = physical pad colour (Red, Blue, Green, Yellow)
        //   Value  = expected sequence colour index
        // ----------------------------------------------------------------
        private static readonly int[,] ColourMap = new int[,]
        {
            // Red  Blue  Green  Yellow
            {  0,   1,    2,     3  },   // 0 strikes: no remap
            {  1,   3,    2,     0  },   // 1 strike
            {  3,   2,    0,     1  },   // 2 strikes
        };

        private static int MapColour(int physicalColour, int strikes)
        {
            int row = Mathf.Clamp(strikes, 0, 2);
            return ColourMap[row, physicalColour];
        }
    }
}
