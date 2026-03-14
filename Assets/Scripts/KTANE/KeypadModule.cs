// KeypadModule.cs
// Four-key keypad module for the KTANE bomb.
//
// UBIQ NETWORKING PATTERN:
//   Defuser processes XR select events, advances the press sequence, and
//   broadcasts the full keypad state (symbols, current sequence, solved/strike)
//   with context.SendJson().  Expert applies state in ProcessMessage().
//
// XR INTERACTION:
//   Each key (Keypad_Key_0 … Keypad_Key_3) needs:
//     • Collider
//     • XRSimpleInteractable
//
// SOLVE RULES:
//   Six symbol columns are defined (matching the original KTANE keypad
//   lookup table).  On start, a random column is chosen and the 4 symbols
//   in that column form the correct press order (top-to-bottom as per the
//   column ordering).
//
// INSPECTOR SETUP:
//   Drag Keypad_Key_0 … Keypad_Key_3 into the keyObjects array.

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
    // Symbol names (arbitrary strings matching what you display in the Expert UI)
    // -------------------------------------------------------------------------
    public static class KeypadSymbols
    {
        // 6 columns × 4 symbols each, matching the original KTANE lookup table
        public static readonly string[][] Columns = new string[][]
        {
            new[] { "Q", "W", "E", "R" },   // Column 0
            new[] { "T", "Y", "U", "I" },   // Column 1
            new[] { "O", "P", "A", "S" },   // Column 2
            new[] { "D", "F", "G", "H" },   // Column 3
            new[] { "J", "K", "L", "Z" },   // Column 4
            new[] { "X", "C", "V", "B" },   // Column 5
        };
    }

    // -------------------------------------------------------------------------
    // Network message
    // -------------------------------------------------------------------------
    [Serializable]
    public struct KeypadStateMessage
    {
        public string   type;             // "init" | "press"
        public string[] symbols;          // symbols on keys [0..3]
        public int[]    correctOrder;     // key indices in correct press order
        public int[]    pressedOrder;     // keys pressed so far (by key index)
        public bool     isSolved;
        public bool     causedStrike;
        public int      lastPressedKey;
    }

    // -------------------------------------------------------------------------
    // KeypadModule
    // -------------------------------------------------------------------------
    public class KeypadModule : MonoBehaviour
    {
        // ----- Inspector -------------------------------------------------
        [Header("Key GameObjects (Keypad_Key_0 … Keypad_Key_3 from bomb model)")]
        public GameObject[] keyObjects = new GameObject[4];

        // Set to false for levels that don't include the keypad module.
        [HideInInspector] public bool isActive = true;

        // ----- Runtime ---------------------------------------------------
        public string[] Symbols      { get; private set; } = new string[4];
        public int[]    CorrectOrder { get; private set; } = new int[4];
        public bool     IsSolved     { get; private set; }

        private List<int> pressedOrder = new List<int>();
        private XRSimpleInteractable[] keyInteractables = new XRSimpleInteractable[4];

        // ----- Ubiq internals -------------------------------------------
        private NetworkContext context;

        // ================================================================
        // Unity lifecycle
        // ================================================================

        private void Start()
        {
            context = NetworkScene.Register(this);

            var gm = KTANEGameManager.Instance;
            if (gm != null && gm.IsLocalDefuser)
            {
                GenerateKeypad();
                BroadcastState("init", false, -1);
            }

            // Wire up XR interactables
            for (int i = 0; i < keyObjects.Length; i++)
            {
                if (keyObjects[i] == null) continue;
                var xi = keyObjects[i].GetComponent<XRSimpleInteractable>();
                if (xi == null)
                {
                    Debug.LogWarning($"[KeypadModule] Key {i} has no XRSimpleInteractable.", this);
                    continue;
                }

                keyInteractables[i] = xi;
                int capturedIndex = i;
                xi.selectEntered.AddListener(_ => OnKeyPressed(capturedIndex));
            }
        }

        private void OnDestroy()
        {
            for (int i = 0; i < keyInteractables.Length; i++)
                keyInteractables[i]?.selectEntered.RemoveAllListeners();
        }

        // ================================================================
        // Ubiq message handling
        // ================================================================

        public void ProcessMessage(ReferenceCountedSceneGraphMessage message)
        {
            var msg = message.FromJson<KeypadStateMessage>();

            Symbols      = msg.symbols;
            CorrectOrder = msg.correctOrder;
            IsSolved     = msg.isSolved;

            pressedOrder.Clear();
            if (msg.pressedOrder != null)
                pressedOrder.AddRange(msg.pressedOrder);

            // Animate the last pressed key on Expert client
            var gm = KTANEGameManager.Instance;
            if (gm != null && !gm.IsLocalDefuser && msg.lastPressedKey >= 0)
                StartCoroutine(AnimateKey(msg.lastPressedKey));
        }

        // ================================================================
        // XR interaction (Defuser only)
        // ================================================================

        private void OnKeyPressed(int keyIndex)
        {
            if (!isActive) return;
            var gm = KTANEGameManager.Instance;
            if (gm == null || !gm.IsLocalDefuser || gm.CurrentState != GameState.Active)
                return;
            if (IsSolved) return;

            // Check if this key is the correct next in sequence
            int step            = pressedOrder.Count;
            int expectedKey     = CorrectOrder[step];

            if (keyIndex == expectedKey)
            {
                pressedOrder.Add(keyIndex);
                StartCoroutine(AnimateKey(keyIndex));

                if (pressedOrder.Count == CorrectOrder.Length)
                {
                    IsSolved = true;
                    gm.SolveModule();
                    Debug.Log("[KeypadModule] Correct sequence – module solved.", this);
                    BroadcastState("press", false, keyIndex);
                }
                else
                {
                    BroadcastState("press", false, keyIndex);
                }
            }
            else
            {
                // Wrong key → strike and reset current sequence progress
                pressedOrder.Clear();
                gm.AddStrike();
                Debug.Log($"[KeypadModule] Wrong key pressed (pressed {keyIndex}, expected {expectedKey}) – strike!", this);
                BroadcastState("press", true, keyIndex);
            }
        }

        // ================================================================
        // Configuration (called by KTANELobbyManager before game start)
        // ================================================================

        public void Configure(bool active, int seed)
        {
            isActive = active;

            foreach (var key in keyObjects)
                if (key != null) key.SetActive(active);

            if (!active) return;

            IsSolved = false;
            pressedOrder.Clear();

            UnityEngine.Random.InitState(seed);
            GenerateKeypad();
            BroadcastState("init", false, -1);
            Debug.Log($"[KeypadModule] Configured: active={active} seed={seed}", this);
        }

        // ================================================================
        // Private helpers
        // ================================================================

        private void GenerateKeypad()
        {
            // Pick a random column and use its symbols as key labels
            int colIndex = UnityEngine.Random.Range(0, KeypadSymbols.Columns.Length);
            var col      = KeypadSymbols.Columns[colIndex];

            // Assign symbols to keys in random order (shuffle column symbols)
            var shuffled = new List<string>(col);
            Shuffle(shuffled);
            for (int i = 0; i < 4; i++)
                Symbols[i] = shuffled[i];

            // Build correct order: position within original column order
            // (the correct press order follows the column's top-to-bottom order)
            var colOrder = new List<string>(col); // original column order

            // Map: for each position in colOrder, find which key index has that symbol
            CorrectOrder = new int[4];
            for (int pos = 0; pos < 4; pos++)
            {
                string needed = colOrder[pos];
                for (int k = 0; k < 4; k++)
                {
                    if (Symbols[k] == needed)
                    {
                        CorrectOrder[pos] = k;
                        break;
                    }
                }
            }

            Debug.Log($"[KeypadModule] Symbols: {string.Join(",", Symbols)}  " +
                      $"CorrectOrder: {string.Join(",", CorrectOrder)}", this);
        }

        private void BroadcastState(string type, bool struck, int lastKey)
        {
            if (context.Scene == null) return;
            context.SendJson(new KeypadStateMessage
            {
                type          = type,
                symbols       = Symbols,
                correctOrder  = CorrectOrder,
                pressedOrder  = pressedOrder.ToArray(),
                isSolved      = IsSolved,
                causedStrike  = struck,
                lastPressedKey = lastKey
            });
        }

        private IEnumerator AnimateKey(int index)
        {
            if (index < 0 || index >= keyObjects.Length) yield break;
            var go = keyObjects[index];
            if (go == null) yield break;

            var t      = go.transform;
            var orig   = t.localScale;
            var pressed = orig * 0.85f;

            float dur = 0.1f;
            float e   = 0f;
            while (e < dur)  { e += Time.deltaTime; t.localScale = Vector3.Lerp(orig, pressed, e / dur); yield return null; }
            e = 0f;
            while (e < dur)  { e += Time.deltaTime; t.localScale = Vector3.Lerp(pressed, orig, e / dur); yield return null; }
            t.localScale = orig;
        }

        private static void Shuffle<T>(List<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = UnityEngine.Random.Range(0, i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }
    }
}
