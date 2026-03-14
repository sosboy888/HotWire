// ExpertUIManager.cs
// Read-only heads-up display for the Expert player.
//
// UBIQ NETWORKING PATTERN:
//   This script does NOT register with the NetworkScene and does NOT send
//   any messages.  It is a pure consumer: it listens to Unity Events fired
//   by KTANEGameManager and polls the module scripts once per frame to
//   refresh the UI labels.  This keeps the Expert UI completely decoupled
//   from the networking layer – all game state arrives via the modules'
//   own ProcessMessage callbacks first, and the UI reads it afterwards.
//
// UNITY SETUP:
//   1. Create a World Space Canvas as a child of the Expert's camera rig
//      (or floating in front of the Expert's position).
//   2. Populate the Text fields listed in the Inspector with UI TextMeshPro
//      (or legacy Text) components inside that Canvas.
//   3. In the Expert's XR Rig, set the Canvas "Render Mode" to World Space
//      and size it comfortably (e.g. 0.6 m × 0.9 m, Scale ~0.001).
//   4. This GameObject should only be active on the Expert client.
//      Use KTANEGameManager.OnRoleAssigned to toggle it in a small helper:
//
//        gm.OnRoleAssigned.AddListener(role =>
//            expertCanvasRoot.SetActive(role == PlayerRole.Expert));

using System.Text;
using UnityEngine;
using TMPro;               // remove if using legacy UI.Text

namespace KTANE
{
    public class ExpertUIManager : MonoBehaviour
    {
        // ----- Inspector -------------------------------------------------
        [Header("Panel TextMeshPro labels (assign in Inspector)")]
        public TextMeshProUGUI labelRole;
        public TextMeshProUGUI labelGameState;
        public TextMeshProUGUI labelStrikes;
        public TextMeshProUGUI labelTimer;
        public TextMeshProUGUI labelWires;
        public TextMeshProUGUI labelButton;
        public TextMeshProUGUI labelKeypad;
        public TextMeshProUGUI labelSimon;

        [Header("Module references (assign in Inspector)")]
        public TimerModule   timerModule;
        public WiresModule   wiresModule;
        public ButtonModule  buttonModule;
        public KeypadModule  keypadModule;
        public SimonModule   simonModule;

        // ================================================================
        // Unity lifecycle
        // ================================================================

        private void Start()
        {
            var gm = KTANEGameManager.Instance;
            if (gm == null)
            {
                Debug.LogWarning("[ExpertUIManager] KTANEGameManager not found.", this);
                return;
            }

            // Show/hide this panel based on role
            gm.OnRoleAssigned.AddListener(role =>
            {
                gameObject.SetActive(role == PlayerRole.Expert);
            });

            // Start hidden; will be shown once role is assigned
            gameObject.SetActive(false);
        }

        private void Update()
        {
            RefreshUI();
        }

        // ================================================================
        // UI refresh (runs every frame; only visible on Expert client)
        // ================================================================

        private void RefreshUI()
        {
            var gm = KTANEGameManager.Instance;
            if (gm == null) return;

            // ---- Header info ----------------------------------------
            SetText(labelRole,      $"Role: EXPERT");
            SetText(labelGameState, $"State: {gm.CurrentState}");
            SetText(labelStrikes,   BuildStrikesText(gm));

            // ---- Timer ----------------------------------------------
            if (timerModule != null)
            {
                int mins = timerModule.SecondsRemaining / 60;
                int secs = timerModule.SecondsRemaining % 60;
                SetText(labelTimer, $"Timer: {mins:D2}:{secs:D2}");
            }
            else
            {
                SetText(labelTimer, "Timer: --:--");
            }

            // ---- Wires module ---------------------------------------
            if (wiresModule != null)
                SetText(labelWires, BuildWiresText(wiresModule));
            else
                SetText(labelWires, "Wires: (module not found)");

            // ---- Button module --------------------------------------
            if (buttonModule != null)
                SetText(labelButton, BuildButtonText(buttonModule, gm));
            else
                SetText(labelButton, "Button: (module not found)");

            // ---- Keypad module --------------------------------------
            if (keypadModule != null)
                SetText(labelKeypad, BuildKeypadText(keypadModule));
            else
                SetText(labelKeypad, "Keypad: (module not found)");

            // ---- Simon Says module ----------------------------------
            if (simonModule != null)
                SetText(labelSimon, BuildSimonText(simonModule, gm));
            else
                SetText(labelSimon, "Simon: (module not found)");
        }

        // ================================================================
        // Text builders
        // ================================================================

        private static string BuildStrikesText(KTANEGameManager gm)
        {
            var sb = new StringBuilder("Strikes: ");
            for (int i = 0; i < gm.MaxStrikes; i++)
                sb.Append(i < gm.Strikes ? "[X]" : "[ ]");
            return sb.ToString();
        }

        private static string BuildWiresText(WiresModule wires)
        {
            // Show each wire's colour and cut state
            string[] colourNames = new[] { "Red", "Blue", "Yel", "Wht", "Blk", "Red" };
            var sb = new StringBuilder("--- WIRES ---\n");
            for (int i = 0; i < 6; i++)
            {
                bool cut   = wires.WiresCut != null && i < wires.WiresCut.Length && wires.WiresCut[i];
                string col = i < colourNames.Length ? colourNames[i] : "?";
                sb.AppendLine($"  Wire {i} ({col}): {(cut ? "CUT" : "intact")}");
            }
            sb.Append(wires.IsSolved ? "  [SOLVED]" : "  [pending]");
            return sb.ToString();
        }

        private static string BuildButtonText(ButtonModule btn, KTANEGameManager gm)
        {
            var sb = new StringBuilder("--- BUTTON ---\n");
            sb.AppendLine($"  Colour: {btn.Colour}");
            sb.AppendLine($"  Held: {btn.IsHeld}");

            // Hint for the Expert to tell the Defuser
            sb.AppendLine("  RULE:");
            switch (btn.Colour)
            {
                case ButtonColour.Blue:
                    sb.AppendLine("    Hold. Release when timer has a 4.");
                    break;
                case ButtonColour.Red:
                    sb.AppendLine("    Hold. Release when timer has a 1.");
                    break;
                default:
                    sb.AppendLine("    Tap immediately (quick press).");
                    break;
            }
            sb.Append(btn.IsSolved ? "  [SOLVED]" : "  [pending]");
            return sb.ToString();
        }

        private static string BuildKeypadText(KeypadModule kp)
        {
            var sb = new StringBuilder("--- KEYPAD ---\n");
            if (kp.Symbols != null)
            {
                for (int i = 0; i < kp.Symbols.Length; i++)
                    sb.AppendLine($"  Key {i}: {kp.Symbols[i]}");
            }
            if (kp.CorrectOrder != null)
                sb.AppendLine($"  Press order: {string.Join(" → ", kp.CorrectOrder)}");
            sb.Append(kp.IsSolved ? "  [SOLVED]" : "  [pending]");
            return sb.ToString();
        }

        private static string BuildSimonText(SimonModule simon, KTANEGameManager gm)
        {
            var sb = new StringBuilder("--- SIMON ---\n");

            sb.AppendLine($"  Round: {simon.CurrentRound}");

            if (simon.Sequence != null)
            {
                // Show the sequence for this round with mapped colours
                string[] names = { "Red", "Blue", "Green", "Yellow" };

                // Colour mapping table indices (rows = strikes, cols = physical col)
                int[,] map = new int[,]
                {
                    { 0, 1, 2, 3 },
                    { 1, 3, 2, 0 },
                    { 3, 2, 0, 1 },
                };
                int row = Mathf.Clamp(gm.Strikes, 0, 2);

                sb.Append("  Press: ");
                for (int i = 0; i < simon.CurrentRound && i < simon.Sequence.Length; i++)
                {
                    int seq      = simon.Sequence[i];
                    // Reverse-map: given sequence colour, which physical pad?
                    // For the Expert hint we show the MAPPED colour they see flashed
                    sb.Append(names[seq]);
                    if (i < simon.CurrentRound - 1) sb.Append(" → ");
                }
                sb.AppendLine();

                // Show rule reminder
                sb.AppendLine($"  (Strikes={gm.Strikes}; colour remapped)");
            }

            sb.Append(simon.IsSolved ? "  [SOLVED]" : "  [pending]");
            return sb.ToString();
        }

        // ================================================================
        // Utility
        // ================================================================

        private static void SetText(TextMeshProUGUI label, string text)
        {
            if (label != null) label.text = text;
        }
    }
}
