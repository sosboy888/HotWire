// ButtonModule.cs
// The Big Button module for the KTANE bomb.
//
// UBIQ NETWORKING PATTERN:
//   The Defuser client is the authority.  It processes XR select events,
//   tracks hold duration, applies solve logic and then broadcasts the full
//   button state with context.SendJson().  The Expert receives the state in
//   ProcessMessage() for their read-only UI and LED colour update.
//
// XR INTERACTION:
//   Button_Main should have an XRSimpleInteractable.
//   onSelectEntered  → button pressed down
//   onSelectExited   → button released
//
// SOLVE RULES (matches original KTANE Button module):
//   Blue  button → hold; release when timer has a 4 in any position.
//   Red   button → hold; release when timer has a 1 in any position.
//   Any other    → tap immediately (release quickly; no hold needed).
//
// INSPECTOR SETUP:
//   • Assign buttonInteractable (Button_Main with XRSimpleInteractable).
//   • Assign buttonLED  (Button_LED Renderer whose emission colour changes).

using System;
using System.Collections;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using Ubiq.Messaging;

namespace KTANE
{
    // -------------------------------------------------------------------------
    // Button colour (randomised on start)
    // -------------------------------------------------------------------------
    public enum ButtonColour { Red, Blue, Yellow, White }

    // -------------------------------------------------------------------------
    // Network message
    // -------------------------------------------------------------------------
    [Serializable]
    public struct ButtonStateMessage
    {
        public string type;          // "init" | "press" | "release"
        public int    buttonColour;  // cast of ButtonColour
        public bool   isHeld;
        public bool   isSolved;
        public bool   causedStrike;
        public int    ledColour;     // 0=off, 1=blue, 2=yellow, 3=white
    }

    // -------------------------------------------------------------------------
    // ButtonModule
    // -------------------------------------------------------------------------
    public class ButtonModule : MonoBehaviour
    {
        // ----- Inspector -------------------------------------------------
        [Header("References")]
        public XRSimpleInteractable buttonInteractable; // Button_Main
        public Renderer             buttonLED;          // Button_LED

        // Set to false for levels that don't include the button module.
        [HideInInspector] public bool isActive = true;

        // ----- Runtime ---------------------------------------------------
        public ButtonColour Colour   { get; private set; }
        public bool         IsSolved { get; private set; }
        public bool         IsHeld   { get; private set; }

        [Header("Optional — button face renderer to colour-tint")]
        public Renderer buttonRenderer;

        // ----- Ubiq internals -------------------------------------------
        private NetworkContext context;

        // Solved indicator LED (wired via Inspector / BuildKTANEScene)
        [HideInInspector] public Renderer solvedLight;
        private Material _solvedMat;

        // LED material instance
        private Material ledMat;
        private Material buttonMat;

        // Tracks whether Configure() was called before Start()
        private bool _configured = false;

        // When the button was pressed (used to detect quick tap vs hold)
        private float pressTime;

        // Colour lookup for the LED strip (strip colour depends on button colour)
        private static readonly Color[] LedColours = new Color[]
        {
            Color.black,                        // 0 = off
            new Color(0f,   0.4f, 1f),          // 1 = blue
            new Color(1f,   1f,   0f),          // 2 = yellow
            Color.white                         // 3 = white
        };

        // ================================================================
        // Unity lifecycle
        // ================================================================

        private void Start()
        {
            context = NetworkScene.Register(this);

            // Set up LED material instance
            if (buttonLED != null)
            {
                ledMat = buttonLED.material;
                ledMat.EnableKeyword("_EMISSION");
                SetLED(0);
            }

            // Set up button face material for colour tinting
            if (buttonRenderer != null)
            {
                buttonMat = buttonRenderer.material;
            }

            // Wire up XR events
            if (buttonInteractable != null)
            {
                buttonInteractable.selectEntered.AddListener(OnButtonPressed);
                buttonInteractable.selectExited.AddListener(OnButtonReleased);
            }
            else
            {
                Debug.LogWarning("[ButtonModule] buttonInteractable not assigned.", this);
            }

            var gm = KTANEGameManager.Instance;
            if (gm != null && gm.IsLocalDefuser)
            {
                // Only randomise if Configure() hasn't already set the colour.
                if (!_configured)
                    Colour = (ButtonColour)UnityEngine.Random.Range(0, 4);

                ApplyButtonColour();
                BroadcastState("init", false, false);
            }
        }

        private void OnDestroy()
        {
            if (buttonInteractable != null)
            {
                buttonInteractable.selectEntered.RemoveListener(OnButtonPressed);
                buttonInteractable.selectExited.RemoveListener(OnButtonReleased);
            }
        }

        // ================================================================
        // Ubiq message handling
        // ================================================================

        public void ProcessMessage(ReferenceCountedSceneGraphMessage message)
        {
            var msg = message.FromJson<ButtonStateMessage>();

            Colour   = (ButtonColour)msg.buttonColour;
            IsHeld   = msg.isHeld;
            IsSolved = msg.isSolved;

            SetLED(msg.ledColour);

            // Animate the button position when pressed/released on non-Defuser
            var gm = KTANEGameManager.Instance;
            if (gm != null && !gm.IsLocalDefuser)
            {
                if (buttonInteractable != null)
                    StartCoroutine(AnimateButton(msg.isHeld));
            }
        }

        // ================================================================
        // XR event handlers (only meaningful on Defuser client)
        // ================================================================

        private void OnButtonPressed(SelectEnterEventArgs args)
        {
            if (!isActive) return;
            var gm = KTANEGameManager.Instance;
            if (gm == null || !gm.IsLocalDefuser || gm.CurrentState != GameState.Active)
                return;
            if (IsSolved) return;

            IsHeld    = true;
            pressTime = Time.time;

            // Visual + audio feedback on press
            int led = LedIndexForColour(Colour);
            SetLED(led);
            KTANESoundManager.Instance?.PlayCorrect();

            BroadcastState("press", true, false);
            StartCoroutine(AnimateButton(true));
        }

        private void OnButtonReleased(SelectEnterEventArgs args) { }  // keep signature
        private void OnButtonReleased(SelectExitEventArgs args)
        {
            if (!isActive) return;
            var gm = KTANEGameManager.Instance;
            if (gm == null || !gm.IsLocalDefuser || gm.CurrentState != GameState.Active)
                return;
            if (IsSolved || !IsHeld) return;

            IsHeld = false;
            float holdDuration = Time.time - pressTime;

            bool solved = EvaluateRelease(gm, holdDuration);
            bool struck = !solved;

            if (solved)
            {
                IsSolved = true;
                SetSolvedLight(true);
                gm.SolveModule();
                SetLED(0);
                KTANESoundManager.Instance?.PlaySolve();
                Debug.Log("[ButtonModule] Button released correctly – solved.", this);
            }
            else
            {
                gm.AddStrike();
                SetLED(0);
                KTANESoundManager.Instance?.PlayWrong();
                Debug.Log("[ButtonModule] Wrong release timing – strike!", this);
            }

            BroadcastState("release", false, struck);
            StartCoroutine(AnimateButton(false));
        }

        // ================================================================
        // Solve logic
        // ================================================================

        /// <summary>
        /// Returns true if the release is valid per the KTANE Button rules.
        /// </summary>
        private bool EvaluateRelease(KTANEGameManager gm, float holdDuration)
        {
            var timer = FindFirstObjectByType<TimerModule>();

            // "Tap" colours: any colour that is NOT Blue or Red → must be
            // a quick tap (hold duration < 0.5 s).
            if (Colour == ButtonColour.Yellow || Colour == ButtonColour.White)
                return holdDuration < 0.5f;

            // Blue → hold and release when a 4 appears in the timer
            if (Colour == ButtonColour.Blue)
            {
                if (timer == null) return false;
                return TimerContainsDigit(timer, 4);
            }

            // Red → hold and release when a 1 appears in the timer
            if (Colour == ButtonColour.Red)
            {
                if (timer == null) return false;
                return TimerContainsDigit(timer, 1);
            }

            return false;
        }

        private static bool TimerContainsDigit(TimerModule timer, int digit)
        {
            int seconds = timer.SecondsRemaining;
            // Check every decimal position
            while (seconds > 0)
            {
                if (seconds % 10 == digit) return true;
                seconds /= 10;
            }
            return false;
        }

        // ================================================================
        // Configuration (called by KTANELobbyManager before game start)
        // ================================================================

        public void Configure(bool active, int seed)
        {
            isActive     = active;
            _configured  = true;

            // Show/hide button GameObjects
            if (buttonInteractable != null)
                buttonInteractable.gameObject.SetActive(active);
            if (buttonLED != null)
                buttonLED.gameObject.SetActive(active);
            if (solvedLight != null) solvedLight.gameObject.SetActive(active);

            if (!active) return;

            IsSolved = false;
            IsHeld   = false;
            SetSolvedLight(false);

            UnityEngine.Random.InitState(seed);
            Colour = (ButtonColour)UnityEngine.Random.Range(0, 4);

            ApplyButtonColour();
            // BroadcastState is a no-op here (context not registered yet);
            // Start() will broadcast after context is ready.
            Debug.Log($"[ButtonModule] Configured: active={active} colour={Colour} seed={seed}", this);
        }

        private void ApplyButtonColour()
        {
            if (buttonMat == null) return;
            Color col = Colour switch
            {
                ButtonColour.Red    => new Color(0.85f, 0.12f, 0.12f),
                ButtonColour.Blue   => new Color(0.12f, 0.30f, 0.90f),
                ButtonColour.Yellow => new Color(0.95f, 0.85f, 0.05f),
                _                  => new Color(0.90f, 0.90f, 0.90f),  // White
            };
            if (buttonMat.HasProperty("_BaseColor")) buttonMat.SetColor("_BaseColor", col);
            else                                     buttonMat.SetColor("_Color",     col);
        }

        // ================================================================
        // Helpers
        // ================================================================

        private void BroadcastState(string type, bool held, bool struck)
        {
            if (context.Scene == null) return;
            context.SendJson(new ButtonStateMessage
            {
                type         = type,
                buttonColour = (int)Colour,
                isHeld       = held,
                isSolved     = IsSolved,
                causedStrike = struck,
                ledColour    = held ? LedIndexForColour(Colour) : 0
            });
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

        private int LedIndexForColour(ButtonColour col)
        {
            return col switch
            {
                ButtonColour.Blue   => 1,
                ButtonColour.Yellow => 2,
                ButtonColour.White  => 3,
                _                  => 1   // Red → blue strip per KTANE rules
            };
        }

        private void SetLED(int index)
        {
            if (ledMat == null) return;
            Color c = (index >= 0 && index < LedColours.Length) ? LedColours[index] : Color.black;
            ledMat.SetColor("_EmissionColor", c * 3f);
        }

        private IEnumerator AnimateButton(bool pressed)
        {
            if (buttonInteractable == null) yield break;
            var t = buttonInteractable.transform;
            Vector3 target = pressed
                ? t.localPosition - new Vector3(0, 0.005f, 0)   // push in 5 mm
                : t.localPosition + new Vector3(0, 0.005f, 0);  // return

            float elapsed = 0f;
            Vector3 start  = t.localPosition;
            while (elapsed < 0.08f)
            {
                elapsed += Time.deltaTime;
                t.localPosition = Vector3.Lerp(start, target, elapsed / 0.08f);
                yield return null;
            }
            t.localPosition = target;
        }
    }
}
