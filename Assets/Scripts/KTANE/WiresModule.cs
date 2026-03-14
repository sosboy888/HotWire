// WiresModule.cs
// Six-wire module for the KTANE bomb.
//
// UBIQ NETWORKING PATTERN:
//   Only the Defuser client processes wire interactions.  On each cut the
//   Defuser evaluates the solve rule and broadcasts the full wire-cut state
//   plus result (solved / strike) via context.SendJson().  The Expert client
//   applies the received state in ProcessMessage() to update its read-only UI.
//
// XR INTERACTION:
//   Each wire child object (Wire_0 … Wire_5) should have:
//     • MeshCollider or CapsuleCollider
//     • XRGrabInteractable
//   When the Defuser grabs a wire and moves 8 cm away from the grab point
//   the wire is considered "cut".  Wrong wires add a strike.
//
// INSPECTOR SETUP:
//   Attach this script to the bomb root (or a dedicated WiresModule
//   GameObject).  Drag Wire_0 … Wire_5 into the wireObjects array in order.

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using Ubiq.Messaging;

namespace KTANE
{
    // -------------------------------------------------------------------------
    // Wire colours (must match the order of wireObjects in the Inspector)
    // -------------------------------------------------------------------------
    public enum WireColour { Red, Blue, Yellow, White, Black }

    // -------------------------------------------------------------------------
    // Network message
    // -------------------------------------------------------------------------
    [Serializable]
    public struct WiresCutMessage
    {
        public string type;           // "state"
        public bool[] wiresCut;       // element i = true if wire i is cut
        public int    correctWire;    // index of the wire that must be cut
        public bool   isSolved;
        public bool   causedStrike;
        public int    lastCutIndex;
    }

    // -------------------------------------------------------------------------
    // WiresModule
    // -------------------------------------------------------------------------
    public class WiresModule : MonoBehaviour
    {
        // ----- Inspector -------------------------------------------------
        [Header("Wire GameObjects (Wire_0 … Wire_5 from bomb model)")]
        public GameObject[] wireObjects = new GameObject[6];

        [Tooltip("Distance (metres) the Defuser must pull a wire to cut it.")]
        public float cutDistance = 0.08f;

        // Set to false for levels that don't include the wires module.
        [HideInInspector] public bool isActive = true;

        // ----- Wire colours in fixed order (Red, Blue, Yellow, White, Black, Red)
        private static readonly WireColour[] WireColours = new WireColour[]
        {
            WireColour.Red,    // Wire_0
            WireColour.Blue,   // Wire_1
            WireColour.Yellow, // Wire_2
            WireColour.White,  // Wire_3
            WireColour.Black,  // Wire_4
            WireColour.Red     // Wire_5
        };

        // ----- Runtime ---------------------------------------------------
        public bool   IsSolved    { get; private set; }
        public bool[] WiresCut    { get; private set; } = new bool[6];
        public int    CorrectWire { get; private set; } = -1;

        // Per-wire grab tracking (only used on Defuser client)
        private Vector3[] grabPositions = new Vector3[6];
        private XRGrabInteractable[] grabInteractables = new XRGrabInteractable[6];

        // Hard wire rules flag (set by Configure)
        private bool hardRules = false;

        // ----- Ubiq internals -------------------------------------------
        private NetworkContext context;

        // ================================================================
        // Unity lifecycle
        // ================================================================

        private void Start()
        {
            context = NetworkScene.Register(this);

            // Determine correct wire only on the Defuser (deterministic seed
            // based on object's scene path so both clients agree if we choose
            // to run solve-check on both; here only Defuser runs it).
            DetermineCorrectWire();

            // Hook up XR interaction for each wire
            for (int i = 0; i < wireObjects.Length; i++)
            {
                if (wireObjects[i] == null) continue;

                var grab = wireObjects[i].GetComponent<XRGrabInteractable>();
                if (grab == null)
                {
                    Debug.LogWarning(
                        $"[WiresModule] Wire_{ i } has no XRGrabInteractable.", this);
                    continue;
                }

                grabInteractables[i] = grab;
                int capturedIndex = i; // closure capture

                grab.selectEntered.AddListener(args =>
                    OnWireGrabbed(capturedIndex, args));
            }
        }

        private void Update()
        {
            // Check each currently-held wire for the pull distance
            if (!isActive) return;
            var gm = KTANEGameManager.Instance;
            if (gm == null || !gm.IsLocalDefuser || gm.CurrentState != GameState.Active)
                return;
            if (IsSolved) return;

            for (int i = 0; i < grabInteractables.Length; i++)
            {
                var grab = grabInteractables[i];
                if (grab == null || WiresCut[i]) continue;
                if (!grab.isSelected) continue;

                // Measure distance of the interactor from the grab origin
                var interactor = grab.interactorsSelecting[0];
                if (interactor == null) continue;

                float dist = Vector3.Distance(
                    interactor.transform.position,
                    grabPositions[i]);

                if (dist >= cutDistance)
                {
                    CutWire(i);
                }
            }
        }

        private void OnDestroy()
        {
            for (int i = 0; i < grabInteractables.Length; i++)
            {
                if (grabInteractables[i] == null) continue;
                int capturedIndex = i;
                grabInteractables[i].selectEntered.RemoveAllListeners();
            }
        }

        // ================================================================
        // Ubiq message handling
        // ================================================================

        public void ProcessMessage(ReferenceCountedSceneGraphMessage message)
        {
            var msg = message.FromJson<WiresCutMessage>();
            if (msg.type != "state") return;

            // Apply visual state on all clients
            for (int i = 0; i < msg.wiresCut.Length && i < wireObjects.Length; i++)
            {
                if (msg.wiresCut[i] && !WiresCut[i])
                    ApplyCutVisual(i);
            }

            WiresCut    = msg.wiresCut;
            CorrectWire = msg.correctWire;
            IsSolved    = msg.isSolved;
        }

        // ================================================================
        // Configuration (called by KTANELobbyManager before game start)
        // ================================================================

        /// <summary>Set active state, difficulty rules and deterministic seed.</summary>
        public void Configure(bool active, bool useHardRules, int seed)
        {
            isActive  = active;
            hardRules = useHardRules;

            // Show/hide wire GameObjects based on whether this module is in the level
            foreach (var wire in wireObjects)
                if (wire != null) wire.SetActive(active);

            if (!active) return;

            // Reset state
            WiresCut  = new bool[6];
            IsSolved  = false;

            // Re-enable grab interactables that may have been disabled by a previous play
            foreach (var grab in grabInteractables)
                if (grab != null) grab.enabled = true;

            // Restore wire scale (in case a previous round shrunk them)
            foreach (var wire in wireObjects)
            {
                if (wire == null) continue;
                var ls = wire.transform.localScale;
                wire.transform.localScale = new Vector3(0.013f, ls.y, 0.013f);
            }

            // Re-determine correct wire with shared seed
            UnityEngine.Random.InitState(seed);
            DetermineCorrectWire();
            Debug.Log($"[WiresModule] Configured: active={active} hard={hardRules} " +
                      $"seed={seed} correct={CorrectWire}", this);
        }

        // ================================================================
        // Private helpers
        // ================================================================

        private void OnWireGrabbed(int index, SelectEnterEventArgs args)
        {
            // Record grab world position for pull-distance checking
            grabPositions[index] = args.interactorObject.transform.position;
        }

        private void CutWire(int index)
        {
            if (WiresCut[index] || IsSolved) return;

            var gm = KTANEGameManager.Instance;
            if (gm == null || !gm.IsLocalDefuser) return;

            WiresCut[index] = true;
            ApplyCutVisual(index);

            bool solved      = (index == CorrectWire);
            bool causedStrike = !solved;

            if (solved)
            {
                IsSolved = true;
                gm.SolveModule();
                Debug.Log("[WiresModule] Correct wire cut – module solved.", this);
            }
            else
            {
                gm.AddStrike();
                Debug.Log($"[WiresModule] Wrong wire cut (index={index}). Strike!", this);
            }

            BroadcastState(causedStrike, index);
        }

        private void BroadcastState(bool causedStrike, int lastCut)
        {
            if (context.Scene == null) return;
            context.SendJson(new WiresCutMessage
            {
                type         = "state",
                wiresCut     = (bool[])WiresCut.Clone(),
                correctWire  = CorrectWire,
                isSolved     = IsSolved,
                causedStrike = causedStrike,
                lastCutIndex = lastCut
            });
        }

        // ----------------------------------------------------------------
        // Solve rule (deterministic – matches original KTANE rules subset)
        //
        //   • 6 wires: Red, Blue, Yellow, White, Black, Red  (indices 0-5)
        //   • Last digit of timer is obtained from TimerModule.
        //
        //   Rules (simplified):
        //     1. If last digit of timer is odd and there are two red wires
        //        → cut the last red wire.
        //     2. If last wire is black and last digit is even
        //        → cut the last wire.
        //     3. If there is exactly one blue wire
        //        → cut the second wire (index 1).
        //     4. Otherwise → cut the third wire (index 2).
        // ----------------------------------------------------------------
        private void DetermineCorrectWire()
        {
            // Count reds
            int redCount = 0;
            int lastRed  = -1;
            for (int i = 0; i < WireColours.Length; i++)
            {
                if (WireColours[i] == WireColour.Red)
                {
                    redCount++;
                    lastRed = i;
                }
            }

            // Count blues
            int blueCount = 0;
            for (int i = 0; i < WireColours.Length; i++)
                if (WireColours[i] == WireColour.Blue) blueCount++;

            // We need the timer for rule 1 & 2; default to 0 if not ready yet.
            var timer     = FindFirstObjectByType<TimerModule>();
            int lastDigit = timer != null ? timer.LastDigit : 0;

            bool lastIsBlack = (WireColours[^1] == WireColour.Black);

            // Hard rules: additional pre-checks (Level 5)
            if (hardRules)
            {
                // If the timer's last digit is 5 or more, cut the white wire (index 3)
                if (lastDigit >= 5)
                {
                    CorrectWire = 3;
                    return;
                }
                // If there are more than 2 red wires, cut the first wire
                if (redCount > 2)
                {
                    CorrectWire = 0;
                    return;
                }
            }

            // Standard rules (apply first matching)
            if (lastDigit % 2 == 1 && redCount >= 2 && lastRed >= 0)
                CorrectWire = lastRed;
            else if (lastIsBlack && lastDigit % 2 == 0)
                CorrectWire = 5;
            else if (blueCount == 1)
                CorrectWire = 1;
            else
                CorrectWire = 2;

            Debug.Log($"[WiresModule] Correct wire index: {CorrectWire}", this);
        }

        /// <summary>Visual feedback: hide or collapse the wire mesh to show it cut.</summary>
        private void ApplyCutVisual(int index)
        {
            if (wireObjects[index] == null) return;
            // Disable the interactable so it can't be grabbed again
            var grab = wireObjects[index].GetComponent<XRGrabInteractable>();
            if (grab != null) grab.enabled = false;
            // Scale it down to indicate cutting
            wireObjects[index].transform.localScale =
                new Vector3(0.01f, wireObjects[index].transform.localScale.y, 0.01f);
        }
    }
}
