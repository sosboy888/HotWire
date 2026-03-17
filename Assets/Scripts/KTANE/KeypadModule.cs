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
using TMPro;
using Ubiq.Messaging;

namespace KTANE
{
    // -------------------------------------------------------------------------
    // Symbol names (arbitrary strings matching what you display in the Expert UI)
    // -------------------------------------------------------------------------
    public static class KeypadSymbols
    {
        // 6 columns × 4 symbols each.
        // Each symbol is a single Unicode character displayed on the physical key
        // and shown in the Expert UI / manual.
        //
        // Manual columns A–F map to code columns 0–5:
        //   Col A (0): ☆ ψ Θ Ω   Col D (3): Ʌ Ŋ Ɋ Ʃ
        //   Col B (1): Ж Ħ ⊕ ↩   Col E (4): Þ Ð ꝏ Δ
        //   Col C (2): ◈ ♛ ∂ ⊙   Col F (5): Ξ Φ ☊ ℵ
        public static readonly string[][] Columns = new string[][]
        {
            new[] { "☆", "ψ", "Θ", "Ω" },   // Column 0 — Manual Col A
            new[] { "Ж", "Ħ", "⊕", "↩" },   // Column 1 — Manual Col B
            new[] { "◈", "♛", "∂", "⊙" },   // Column 2 — Manual Col C
            new[] { "Ʌ", "Ŋ", "Ɋ", "Ʃ" },   // Column 3 — Manual Col D
            new[] { "Þ", "Ð", "ꝏ", "Δ" },   // Column 4 — Manual Col E
            new[] { "Ξ", "Φ", "☊", "ℵ" },   // Column 5 — Manual Col F
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

        // Solved indicator LED (wired via Inspector / BuildKTANEScene)
        [HideInInspector] public Renderer solvedLight;
        private Material _solvedMat;

        private List<int> pressedOrder = new List<int>();
        private XRSimpleInteractable[] keyInteractables = new XRSimpleInteractable[4];
        private TextMeshPro[] keyLabels = new TextMeshPro[4];

        // Tracks whether Configure() was called before Start()
        private bool _configured = false;

        // ----- Ubiq internals -------------------------------------------
        private NetworkContext context;

        // ================================================================
        // Unity lifecycle
        // ================================================================

        private void Start()
        {
            context = NetworkScene.Register(this);

            // Build per-key TMP labels so the player can see each symbol
            CreateKeyLabels();

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

            var gm = KTANEGameManager.Instance;
            if (gm != null && gm.IsLocalDefuser)
            {
                // Only generate if Configure() hasn't already set the symbols.
                if (!_configured)
                    GenerateKeypad();

                UpdateKeyLabels();
                BroadcastState("init", false, -1);
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
                    SetSolvedLight(true);
                    gm.SolveModule();
                    KTANESoundManager.Instance?.PlaySolve();
                    Debug.Log("[KeypadModule] Correct sequence – module solved.", this);
                    BroadcastState("press", false, keyIndex);
                }
                else
                {
                    KTANESoundManager.Instance?.PlayCorrect();
                    BroadcastState("press", false, keyIndex);
                }
            }
            else
            {
                // Wrong key → strike and reset current sequence progress
                pressedOrder.Clear();
                gm.AddStrike();
                KTANESoundManager.Instance?.PlayWrong();
                Debug.Log($"[KeypadModule] Wrong key pressed (pressed {keyIndex}, expected {expectedKey}) – strike!", this);
                BroadcastState("press", true, keyIndex);
            }
        }

        // ================================================================
        // Configuration (called by KTANELobbyManager before game start)
        // ================================================================

        public void Configure(bool active, int seed)
        {
            isActive    = active;
            _configured = true;

            foreach (var key in keyObjects)
                if (key != null) key.SetActive(active);
            if (solvedLight != null) solvedLight.gameObject.SetActive(active);

            if (!active) return;

            IsSolved = false;
            pressedOrder.Clear();
            SetSolvedLight(false);

            UnityEngine.Random.InitState(seed);
            GenerateKeypad();
            // Labels and broadcast happen from Start() once context is registered.
            Debug.Log($"[KeypadModule] Configured: active={active} seed={seed}", this);
        }

        // ================================================================
        // Private helpers
        // ================================================================

        // Create world-space TMP labels on each key face so the Defuser can see symbols.
        private void CreateKeyLabels()
        {
            for (int i = 0; i < keyObjects.Length; i++)
            {
                if (keyObjects[i] == null) continue;

                // Re-use existing label if already created
                var existing = keyObjects[i].transform.Find("SymbolLabel");
                if (existing != null)
                {
                    keyLabels[i] = existing.GetComponent<TextMeshPro>();
                    continue;
                }

                var go = new GameObject("SymbolLabel");
                go.transform.SetParent(keyObjects[i].transform, false);
                go.transform.localPosition = new Vector3(0f, 0f, -0.012f); // face of key
                go.transform.localRotation = Quaternion.identity;
                go.transform.localScale    = Vector3.one * 0.08f;

                var tmp       = go.AddComponent<TextMeshPro>();
                tmp.fontSize  = 8;
                tmp.fontStyle = FontStyles.Bold;
                tmp.color     = Color.white;
                tmp.alignment = TextAlignmentOptions.Center;
                tmp.GetComponent<RectTransform>().sizeDelta = new Vector2(2f, 2f);

                keyLabels[i] = tmp;
            }
        }

        private void UpdateKeyLabels()
        {
            for (int i = 0; i < keyLabels.Length; i++)
            {
                if (keyLabels[i] != null && Symbols != null && i < Symbols.Length)
                    keyLabels[i].text = Symbols[i];
            }
        }

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

        private void SetSolvedLight(bool on)
        {
            if (solvedLight == null) return;
            if (_solvedMat == null)
            {
                _solvedMat = solvedLight.material;
                _solvedMat.EnableKeyword("_EMISSION");
            }
            _solvedMat.SetColor("_EmissionColor", on ? new Color(0f, 2f, 0f) : Color.black);
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
